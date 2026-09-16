using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The worker runs the same physics/skills as offline play. Players submit intent, never results.
public sealed class ObjectHeadDedicatedGameplay:MonoBehaviour
{
    private ObjectHeadNetworkManager network;
    private TurnManager turns;
    private TerrainManager terrain;
    private PlayerInventoryManager inventories;
    private readonly Dictionary<string,TurnCharacterController> characters=new Dictionary<string,TurnCharacterController>();
    private readonly Dictionary<string,CommonHeadItem> items=new Dictionary<string,CommonHeadItem>();
    private readonly HashSet<string> seen=new HashSet<string>();
    private readonly Queue<string> seenOrder=new Queue<string>();
    private long sequence,lastSequence;
    private int localSeat;
    private float nextTick;
    public bool IsReady {get;private set;}
    public int StatesReceived {get;private set;}
    public int FiresReceived {get;private set;}
    public int TerrainReceived {get;private set;}
    public int CommonUsesReceived {get;private set;}
    public int ImpactsReceived {get;private set;}
    private bool Worker=>network!=null && network.IsDedicatedWorker;
    private static string Id(TurnCharacterController character)
    {var member=character.GetComponent<ObjectHeadTeamMember>();return $"p{member.PlayerIndex}-s{member.TeamSlotIndex}";}
    private IEnumerator Start()
    {
        network=ObjectHeadNetworkManager.Instance;
        float deadline=Time.realtimeSinceStartup+15;
        while((turns=FindAnyObjectByType<TurnManager>())==null || turns.Characters.Length==0)
        {if(Time.realtimeSinceStartup>deadline){Debug.LogError("[Authority] Scene initialization timeout");yield break;}yield return null;}
        terrain=FindAnyObjectByType<TerrainManager>();inventories=FindAnyObjectByType<PlayerInventoryManager>();
        localSeat=GameStartData.Instance.players.FirstOrDefault(p=>p.userId==network.LocalUserId)?.playerIndex??0;
        if(!Worker && localSeat==0){Debug.LogError("[Authority] Missing player assignment");yield break;}
        foreach(var character in turns.Characters)
        {
            characters.Add(Id(character),character);
            character.UseNetworkInput=Worker;
            character.GetComponent<CharacterCombat>().UseExternalHealth=!Worker;
        }
        turns.ConfigureNetworkControl(localSeat,!Worker);
        network.GameplayMessageReceived+=Receive;
        if(Worker){terrain.OperationApplied+=TerrainChanged;CommonHeadUseController.UseCommitted+=CommonCommitted;ObjectHeadPresentation.ImpactCommitted+=ImpactCommitted;}
        IsReady=true;
        if(Worker && Environment.GetCommandLineArgs().Contains("-objectHeadAuthorityTestSupply"))
        {
            foreach(var p in GameStartData.Instance.players)inventories.GetInventory(p.playerIndex).TryAdd(CommonHeadType.IronHelmet,out _);
        }
        if(Worker)Send(121,new ObjectHeadGameplayMessage());
        Debug.Log("[Authority] Dedicated gameplay ready, worker="+Worker);
    }
    private void OnDestroy()
    {
        if(network!=null)network.GameplayMessageReceived-=Receive;
        if(terrain!=null)terrain.OperationApplied-=TerrainChanged;
        CommonHeadUseController.UseCommitted-=CommonCommitted;
        ObjectHeadPresentation.ImpactCommitted-=ImpactCommitted;
    }
    private string MessageId()=>network.LocalUserId+":"+(++sequence);
    private bool First(string id)
    {
        if(string.IsNullOrEmpty(id) || id.Length>160 || !seen.Add(id))return false;
        seenOrder.Enqueue(id);while(seenOrder.Count>2048)seen.Remove(seenOrder.Dequeue());return true;
    }
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if(IsReady && !Worker && UnityEngine.InputSystem.Keyboard.current?.tabKey.wasPressedThisFrame==true)RequestEndTurn();
#endif
        if(!IsReady || !network.IsInMatch || Time.unscaledTime<nextTick)return;
        nextTick=Time.unscaledTime+.1f;
        if(Worker){BroadcastState();return;}
        var current=turns.CurrentCharacter;
        if(current==null || turns.CurrentPlayerIndex!=localSeat)return;
        var aim=current.GetComponent<AimController>();
        Send(100,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.MovementInput,messageId=MessageId(),
            turnSerial=turns.TurnSerial,characterId=Id(current),moveX=current.InputMoveX,jumpHeld=current.InputJumpHeld,
            aimX=aim.AimDirection.x,aimY=aim.AimDirection.y});
    }
    public void RequestFire(SkillFireController fire,float power)
    {
        var character=fire.GetComponent<TurnCharacterController>();
        if(!CanRequest(character))return;
        var aim=character.GetComponent<AimController>();
        Send(100,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.FireCommand,messageId=MessageId(),
            characterId=Id(character),turnSerial=turns.TurnSerial,normalizedPower=power,
            selectedSkillIndex=character.GetComponent<DemoSkillSelector>().SelectedSkillIndex,aimX=aim.AimDirection.x,aimY=aim.AimDirection.y});
    }
    public bool RequestCommon(CommonHeadUseController use,int slot,CommonHeadType type,float power)
    {
        var character=use.GetComponent<TurnCharacterController>();if(!CanRequest(character))return false;
        var aim=character.GetComponent<AimController>();
        Send(100,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.CommonUseRequest,messageId=MessageId(),
            characterId=Id(character),turnSerial=turns.TurnSerial,commonSlot=slot,commonType=type,
            normalizedPower=power,aimX=aim.AimDirection.x,aimY=aim.AimDirection.y});return true;
    }
    private bool CanRequest(TurnCharacterController c)=>IsReady && !Worker && turns.CurrentPlayerIndex==localSeat && c==turns.CurrentCharacter && turns.CanCharacterFire(c);
    public void RequestEndTurn()
    {
        if(!IsReady || Worker || turns.CurrentPlayerIndex!=localSeat)return;
        Send(100,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.EndTurnRequest,messageId=MessageId(),turnSerial=turns.TurnSerial});
    }
    private void Receive(long op,string sender,string json)
    {
        if(!IsReady)return;
        if(Worker)
        {
            if(op!=100)return;
            var message=JsonUtility.FromJson<ObjectHeadGameplayMessage>(json);
            var player=GameStartData.Instance.players.FirstOrDefault(p=>p.userId==sender);
            if(message==null || player==null || player.playerIndex!=turns.CurrentPlayerIndex || message.turnSerial!=turns.TurnSerial || !First(sender+":"+message.messageId))return;
            if(message.kind==ObjectHeadGameplayMessageKind.EndTurnRequest){turns.EndCurrentTurn();return;}
            if(!characters.TryGetValue(message.characterId??"",out var target) || target!=turns.CurrentCharacter)return;
            if(message.kind==ObjectHeadGameplayMessageKind.MovementInput)
            {
                target.SetNetworkInput(message.moveX,message.jumpHeld);
                if(Finite(message.aimX) && Finite(message.aimY))target.GetComponent<AimController>().SetAimDirection(new Vector2(message.aimX,message.aimY));
                return;
            }
            if(!turns.CanCharacterFire(target) || !Finite(message.normalizedPower) || message.normalizedPower<0 || message.normalizedPower>1)return;
            var direction=new Vector2(message.aimX,message.aimY);
            if(!Finite(direction.x)||!Finite(direction.y)||direction.sqrMagnitude<.0001f)return;
            target.GetComponent<AimController>().SetAimDirection(direction.normalized);
            if(message.kind==ObjectHeadGameplayMessageKind.FireCommand)
            {
                if(message.selectedSkillIndex<0 || message.selectedSkillIndex>2)return;
                target.GetComponent<DemoSkillSelector>().SetSkillIndex(message.selectedSkillIndex);
                if(target.GetComponent<SkillFireController>().FireReplicated(message.normalizedPower))
                {message.kind=ObjectHeadGameplayMessageKind.FireAccepted;message.messageId=MessageId();Send(101,message);}
            }
            else if(message.kind==ObjectHeadGameplayMessageKind.CommonUseRequest)
                target.GetComponent<CommonHeadUseController>().UseAuthoritative(message.commonSlot,message.commonType,message.normalizedPower,direction.normalized);
            return;
        }
        if(!string.IsNullOrEmpty(sender))return;
        if(op==110)
        {
            var edit=JsonUtility.FromJson<ObjectHeadTerrainOperationMessage>(json);
            if(edit!=null && First(edit.messageId)){terrain.ApplyOperation(edit.operation);TerrainReceived++;}return;
        }
        var data=JsonUtility.FromJson<ObjectHeadGameplayMessage>(json);
        if(data==null)return;
        if(op==120 && data.kind==ObjectHeadGameplayMessageKind.TurnState)
        {if(data.stateSequence>lastSequence){lastSequence=data.stateSequence;ApplyState(data);StatesReceived++;}return;}
        if(op!=101 || !First(data.messageId))return;
        if(data.kind==ObjectHeadGameplayMessageKind.ImpactPresentation)
        {ObjectHeadPresentation.PresentConfirmedImpact(data.skillId,new Vector2(data.positionX,data.positionY),data.radius);ImpactsReceived++;return;}
        if(!characters.TryGetValue(data.characterId??"",out var c) || data.turnSerial!=turns.TurnSerial)return;
        c.GetComponent<AimController>().SetAimDirection(new Vector2(data.aimX,data.aimY));
        if(data.kind==ObjectHeadGameplayMessageKind.FireAccepted)
        {c.GetComponent<DemoSkillSelector>().SetSkillIndex(data.selectedSkillIndex);c.GetComponent<SkillFireController>().FireReplicated(data.normalizedPower);FiresReceived++;}
        else if(data.kind==ObjectHeadGameplayMessageKind.CommonUseAccepted)
        {c.GetComponent<CommonHeadUseController>().UseReplicated(data.commonType,data.normalizedPower,new Vector2(data.aimX,data.aimY),new Vector2(data.positionX,data.positionY));CommonUsesReceived++;}
    }
    private void BroadcastState()
    {
        Send(120,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.TurnState,stateSequence=++sequence,
            characterId=turns.CurrentCharacter!=null?Id(turns.CurrentCharacter):"",currentTurnIndex=turns.CurrentTurnIndex,
            currentPlayerIndex=turns.CurrentPlayerIndex,turnSerial=turns.TurnSerial,roundSerial=turns.RoundSerial,
            phase=(int)turns.CurrentPhase,remainingTurnSeconds=turns.RemainingTurnSeconds,
            remainingResidualSeconds=turns.RemainingResidualSeconds,residualTimeActive=turns.IsResidualTimeActive,
            actionUsed=turns.ActionUsedThisTurn,matchOver=turns.IsMatchOver,winner=turns.WinningPlayerIndex,
            combatStates=characters.Select(pair=>{var c=pair.Value.GetComponent<CharacterCombat>();var b=pair.Value.GetComponent<Rigidbody2D>();
                return new ObjectHeadCombatState{characterId=pair.Key,hp=c.CurrentHp,pending=c.PendingDamage,shield=c.ShieldAbsorption,
                    x=pair.Value.transform.position.x,y=pair.Value.transform.position.y,vx=b.linearVelocity.x,vy=b.linearVelocity.y};}).ToArray(),
            inventoryStates=GameStartData.Instance.players.Select(p=>new ObjectHeadInventoryState{playerIndex=p.playerIndex,slots=inventories.GetInventory(p.playerIndex).CaptureSlots()}).ToArray(),
            worldItems=FindObjectsByType<CommonHeadItem>(FindObjectsSortMode.None).Select(i=>new ObjectHeadWorldItemState{id=i.NetworkId,type=i.ItemType,x=i.transform.position.x,y=i.transform.position.y}).ToArray()});
    }
    private void ApplyState(ObjectHeadGameplayMessage data)
    {
        foreach(var state in data.combatStates??Array.Empty<ObjectHeadCombatState>())if(characters.TryGetValue(state.characterId,out var c))
        {
            c.GetComponent<CharacterCombat>().ApplyNetworkHealth(state.hp,state.pending,state.shield);
            c.transform.position=new Vector3(state.x,state.y,c.transform.position.z);c.GetComponent<Rigidbody2D>().linearVelocity=new Vector2(state.vx,state.vy);
        }
        foreach(var inv in data.inventoryStates??Array.Empty<ObjectHeadInventoryState>())inventories.GetInventory(inv.playerIndex).ApplyAuthoritativeSlots(inv.slots);
        var present=new HashSet<string>();
        foreach(var state in data.worldItems??Array.Empty<ObjectHeadWorldItemState>())
        {
            if(string.IsNullOrEmpty(state.id)||!present.Add(state.id))continue;
            if(!items.TryGetValue(state.id,out var item)||item==null)items[state.id]=item=CommonHeadItem.Create(state.type,new Vector2(state.x,state.y),CommonHeadItem.GetDefaultSprite(state.type));
            item.ApplyReplica(state);
        }
        foreach(var id in items.Keys.Where(id=>!present.Contains(id)).ToArray()){if(items[id]!=null)items[id].Retire();items.Remove(id);}
        turns.ApplyAuthoritativeTurnState(data.currentTurnIndex,data.turnSerial,data.roundSerial,(TurnPhase)data.phase,
            data.remainingTurnSeconds,data.remainingResidualSeconds,data.residualTimeActive,data.actionUsed);
        if(data.matchOver)turns.ApplyNetworkMatchResult(data.winner);
    }
    private void CommonCommitted(CommonHeadUseController use,int slot,CommonHeadType type,float power,Vector2 aim,Vector2 origin)
    {
        Send(101,new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.CommonUseAccepted,messageId=MessageId(),
            characterId=Id(use.GetComponent<TurnCharacterController>()),turnSerial=turns.TurnSerial,commonSlot=slot,commonType=type,
            normalizedPower=power,aimX=aim.x,aimY=aim.y,positionX=origin.x,positionY=origin.y});
    }
    private void TerrainChanged(TerrainEditOperation operation)=>Send(110,new ObjectHeadTerrainOperationMessage{messageId=MessageId(),turnSerial=turns.TurnSerial,operation=operation});
    private void ImpactCommitted(int id,Vector2 point,float radius)=>Send(101,new ObjectHeadGameplayMessage{
        kind=ObjectHeadGameplayMessageKind.ImpactPresentation,messageId=MessageId(),turnSerial=turns.TurnSerial,
        skillId=id,positionX=point.x,positionY=point.y,radius=radius});
    private async void Send(long opcode,object value)
    {try{await network.SendGameplayMessageAsync(opcode,value);}catch(Exception e){Debug.LogWarning("[Authority] Send failed: "+e.Message);}}
    private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
}

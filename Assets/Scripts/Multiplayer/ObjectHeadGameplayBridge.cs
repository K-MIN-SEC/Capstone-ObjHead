using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(1000)]
public sealed class ObjectHeadGameplayBridge : MonoBehaviour
{
    private const float SnapshotIntervalSeconds = 0.1f;
    private const float TurnStateIntervalSeconds = 0.25f;

    private readonly HashSet<string> processedMessages = new HashSet<string>();
    private readonly Dictionary<string, TurnCharacterController> charactersById =
        new Dictionary<string, TurnCharacterController>();

    private ObjectHeadNetworkManager network;
    private TurnManager turnManager;
    private TerrainManager terrainManager;
    private GameStartData startData;
    private int localPlayerIndex;
    private float nextSnapshotTime;
    private float nextTurnStateTime;
    private int endTurnRequestSerial = -1;
    private long localMessageSequence;
    private long hostStateSequence,lastHostStateSequence;
    private PlayerInventoryManager inventoryManager;
    private readonly Dictionary<string,CommonHeadItem> replicaItems=new Dictionary<string,CommonHeadItem>();
    public int AcceptedCommonUseCount {get;private set;}
    public int ReceivedCommonUseCount {get;private set;}
    public int ReceivedInventoryStateCount {get;private set;}

    public bool IsReady { get; private set; }
    public int ReceivedRemoteSnapshotCount { get; private set; }
    public int ReceivedTurnStateCount { get; private set; }
    public int ReceivedFireCommandCount { get; private set; }
    public int ReceivedTerrainOperationCount { get; private set; }
    public int SentTerrainOperationCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene unused, LoadSceneMode unusedMode)
    {
        if(ObjectHeadCommonAuthority.IsDedicatedMatch)
        {
            if(FindAny<ObjectHeadDedicatedGameplay>()==null)new GameObject("DedicatedGameplay").AddComponent<ObjectHeadDedicatedGameplay>();
            return;
        }
        if (GameStartData.Instance == null || GameStartData.Instance.localMatch || FindAny<ObjectHeadGameplayBridge>() != null)
        {
            return;
        }

        new GameObject("ObjectHeadGameplayBridge").AddComponent<ObjectHeadGameplayBridge>();
    }

    private void Start()
    {
        StartCoroutine(InitializeWhenSceneReady());
    }

    private void OnDestroy()
    {
        if (network != null)
        {
            network.GameplayMessageReceived -= HandleGameplayMessage;
        }

        if (turnManager != null)
        {
            turnManager.TurnStarted -= HandleTurnStateChanged;
            turnManager.TurnPhaseChanged -= HandleTurnPhaseChanged;
        }

        if (terrainManager != null)
        {
            terrainManager.OperationApplied -= HandleAuthoritativeTerrainOperation;
        }

        SkillFireController.FireCommitted -= HandleLocalFireCommitted;
        CommonHeadUseController.UseCommitted -= HandleCommonUseCommitted;
    }

    private IEnumerator InitializeWhenSceneReady()
    {
        startData = GameStartData.Instance;
        network = ObjectHeadNetworkManager.Instance;

        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            turnManager = FindAny<TurnManager>();
            if (turnManager != null && turnManager.Characters != null && turnManager.Characters.Length > 0)
            {
                break;
            }

            yield return null;
        }

        if (network == null || turnManager == null || startData == null)
        {
            Debug.LogError("[ObjectHead Network] Gameplay bridge could not find its runtime dependencies.");
            Destroy(gameObject);
            yield break;
        }

        ObjectHeadPlayerAssignment localAssignment = (startData.players ?? Array.Empty<ObjectHeadPlayerAssignment>())
            .FirstOrDefault(player => player.userId == network.LocalUserId);
        if (localAssignment == null)
        {
            Debug.LogError("[ObjectHead Network] Local user has no GameStartData assignment.");
            Destroy(gameObject);
            yield break;
        }

        localPlayerIndex = localAssignment.playerIndex;
        IndexCharacters();
        foreach (var character in charactersById.Values)
            character.GetComponent<CharacterCombat>().UseExternalHealth = !network.IsHost;
        terrainManager = FindAny<TerrainManager>();
        inventoryManager = FindAny<PlayerInventoryManager>();
        turnManager.ConfigureNetworkControl(localPlayerIndex, !network.IsHost);
        network.GameplayMessageReceived += HandleGameplayMessage;
        turnManager.TurnStarted += HandleTurnStateChanged;
        turnManager.TurnPhaseChanged += HandleTurnPhaseChanged;
        SkillFireController.FireCommitted += HandleLocalFireCommitted;
        CommonHeadUseController.UseCommitted += HandleCommonUseCommitted;

        if (network.IsHost && terrainManager != null)
        {
            terrainManager.OperationApplied += HandleAuthoritativeTerrainOperation;
        }

        if (network.IsHost)
        {
            SendTurnState();
        }

        IsReady = true;
        Debug.Log($"[ObjectHead Network] Gameplay bridge ready. Local player=P{localPlayerIndex}, host={network.IsHost}.");
    }

    private void Update()
    {
        if (network == null || turnManager == null || !network.IsInMatch)
        {
            return;
        }

        if (Time.unscaledTime >= nextSnapshotTime)
        {
            nextSnapshotTime = Time.unscaledTime + SnapshotIntervalSeconds;
            SendOwnedCharacterSnapshot();
        }

        if (network.IsHost && Time.unscaledTime >= nextTurnStateTime)
        {
            nextTurnStateTime = Time.unscaledTime + TurnStateIntervalSeconds;
            SendTurnState();
        }

        if (!network.IsHost &&
            turnManager.CurrentPlayerIndex == localPlayerIndex &&
            endTurnRequestSerial != turnManager.TurnSerial &&
            WasEndTurnPressed())
        {
            endTurnRequestSerial = turnManager.TurnSerial;
            Send(new ObjectHeadGameplayMessage
            {
                kind = ObjectHeadGameplayMessageKind.EndTurnRequest,
                messageId = NextMessageId(),
                turnSerial = turnManager.TurnSerial,
                currentPlayerIndex = localPlayerIndex
            }, ObjectHeadNetworkProtocol.GameplayCommand);
        }
    }

    public void RequestEndTurn()
    {
        if (!IsReady || network.IsHost || turnManager.CurrentPlayerIndex != localPlayerIndex || endTurnRequestSerial == turnManager.TurnSerial) return;
        endTurnRequestSerial = turnManager.TurnSerial;
        Send(new ObjectHeadGameplayMessage { kind = ObjectHeadGameplayMessageKind.EndTurnRequest,
            messageId = NextMessageId(), turnSerial = turnManager.TurnSerial, currentPlayerIndex = localPlayerIndex }, ObjectHeadNetworkProtocol.GameplayCommand);
    }

    private void IndexCharacters()
    {
        charactersById.Clear();
        foreach (TurnCharacterController character in turnManager.Characters)
        {
            string characterId = GetCharacterId(character);
            if (!string.IsNullOrEmpty(characterId))
            {
                charactersById[characterId] = character;
            }
        }
    }

    private void SendOwnedCharacterSnapshot()
    {
        TurnCharacterController character = turnManager.CurrentCharacter;
        if (character == null || GetPlayerIndex(character) != localPlayerIndex)
        {
            return;
        }

        Rigidbody2D body = character.GetComponent<Rigidbody2D>();
        AimController aim = character.GetComponent<AimController>();
        DemoSkillSelector selector = character.GetComponent<DemoSkillSelector>();
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        Vector2 direction = aim != null ? aim.AimDirection : Vector2.right;

        Send(new ObjectHeadGameplayMessage
        {
            kind = ObjectHeadGameplayMessageKind.CharacterSnapshot,
            messageId = NextMessageId(),
            characterId = GetCharacterId(character),
            turnSerial = turnManager.TurnSerial,
            positionX = character.transform.position.x,
            positionY = character.transform.position.y,
            velocityX = velocity.x,
            velocityY = velocity.y,
            aimX = direction.x,
            aimY = direction.y,
            selectedSkillIndex = selector != null ? selector.SelectedSkillIndex : 0
            ,commonSlot=character.GetComponent<CommonHeadUseController>()?.SelectedSlotIndex ?? -1
        }, ObjectHeadNetworkProtocol.GameplayCommand);
    }

    private void HandleLocalFireCommitted(
        SkillFireController fireController,
        float normalizedPower,
        Vector2 aimDirection,
        int skillIndex)
    {
        if (fireController == null || network == null || turnManager == null)
        {
            return;
        }

        TurnCharacterController character = fireController.GetComponent<TurnCharacterController>();
        if (GetPlayerIndex(character) != localPlayerIndex)
        {
            return;
        }

        Send(new ObjectHeadGameplayMessage
        {
            kind = ObjectHeadGameplayMessageKind.FireCommand,
            messageId = NextMessageId(),
            characterId = GetCharacterId(character),
            turnSerial = turnManager.TurnSerial,
            aimX = aimDirection.x,
            aimY = aimDirection.y,
            selectedSkillIndex = skillIndex,
            gourdChoiceId = character.GetComponent<ObjectHeadGourd>()?.LastResolvedId,
            normalizedPower = normalizedPower
        }, ObjectHeadNetworkProtocol.GameplayCommand);
    }

    private void HandleGameplayMessage(long opCode, string senderUserId, string json)
    {
        if (opCode == ObjectHeadNetworkProtocol.TerrainOperation)
        {
            ApplyTerrainOperation(senderUserId, json);
            return;
        }

        ObjectHeadGameplayMessage message = JsonUtility.FromJson<ObjectHeadGameplayMessage>(json);
        if (message == null || message.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion)
        {
            return;
        }

        if (!string.IsNullOrEmpty(message.messageId) && !processedMessages.Add(message.messageId))
        {
            return;
        }

        int senderPlayerIndex = GetPlayerIndex(senderUserId);
        switch (message.kind)
        {
            case ObjectHeadGameplayMessageKind.CharacterSnapshot:
                ApplyCharacterSnapshot(senderPlayerIndex, message);
                break;

            case ObjectHeadGameplayMessageKind.FireCommand:
                if(message.gourdRequest){if(network.IsHost)AcceptGourdFire(senderPlayerIndex,message);}
                else ApplyFireCommand(senderPlayerIndex, message);
                break;

            case ObjectHeadGameplayMessageKind.FireAccepted:
                if(!network.IsHost && senderUserId==network.HostUserId && message.turnSerial==turnManager.TurnSerial &&
                    charactersById.TryGetValue(message.characterId??"",out var borrowedActor) && borrowedActor==turnManager.CurrentCharacter &&
                    message.selectedSkillIndex>=0 && message.selectedSkillIndex<3)
                {
                    var gourd=borrowedActor.GetComponent<ObjectHeadGourd>();if(gourd==null)break;
                    borrowedActor.GetComponent<DemoSkillSelector>().SetSkillIndex(message.selectedSkillIndex,false);
                    borrowedActor.GetComponent<AimController>().SetAimDirection(new Vector2(message.aimX,message.aimY));
                    gourd.ConfirmedId=message.gourdChoiceId;
                    if(borrowedActor.GetComponent<SkillFireController>().FireReplicated(message.normalizedPower))ReceivedFireCommandCount++;
                }
                break;

            case ObjectHeadGameplayMessageKind.CommonUseRequest:
                if(network.IsHost)AcceptCommonUse(senderPlayerIndex,message);
                break;

            case ObjectHeadGameplayMessageKind.CommonUseAccepted:
                if(!network.IsHost && senderUserId==network.HostUserId &&
                    message.turnSerial==turnManager.TurnSerial && charactersById.TryGetValue(message.characterId,out var commonCharacter) &&
                    commonCharacter==turnManager.CurrentCharacter && ObjectHeadContent.Load()?.Common(message.commonType)!=null)
                {
                    commonCharacter.GetComponent<CommonHeadUseController>()?.UseReplicated(message.commonType,message.normalizedPower,
                        new Vector2(message.aimX,message.aimY),new Vector2(message.positionX,message.positionY));
                    ReceivedCommonUseCount++;
                }
                break;

            case ObjectHeadGameplayMessageKind.TurnState:
                if (senderUserId == network.HostUserId && !network.IsHost && message.stateSequence>lastHostStateSequence)
                {
                    lastHostStateSequence=message.stateSequence;
                    ReceivedTurnStateCount++;
                    ApplyCommonState(message);
                    foreach (var state in message.combatStates ?? Array.Empty<ObjectHeadCombatState>())
                        if (charactersById.TryGetValue(state.characterId, out var target))
                        {
                            target.GetComponent<CharacterCombat>().ApplyNetworkHealth(state.hp, state.pending,state.shield);
                            target.GetComponent<ObjectHeadGourd>()?.Apply(state.gourdCharges);
                            ObjectHeadCaptivity.ApplyReplica(target,state.captiveTurns);
                        }
                    if (message.matchOver) { turnManager.ApplyNetworkMatchResult(message.winner); break; }
                    turnManager.ApplyAuthoritativeTurnState(
                        message.currentTurnIndex,
                        message.turnSerial,
                        message.roundSerial,
                        (TurnPhase)message.phase,
                        message.remainingTurnSeconds,
                        message.remainingResidualSeconds,
                        message.residualTimeActive,
                        message.actionUsed,
                        message.settlementPending);
                }
                break;

            case ObjectHeadGameplayMessageKind.EndTurnRequest:
                if (network.IsHost &&
                    message.turnSerial == turnManager.TurnSerial &&
                    senderPlayerIndex == turnManager.CurrentPlayerIndex)
                {
                    turnManager.EndCurrentTurn();
                }
                break;
        }
    }

    private void HandleAuthoritativeTerrainOperation(TerrainEditOperation operation)
    {
        if (network == null || turnManager == null || !network.IsHost)
        {
            return;
        }

        Send(new ObjectHeadTerrainOperationMessage
        {
            messageId = NextMessageId(),
            turnSerial = turnManager.TurnSerial,
            operation = operation
        }, ObjectHeadNetworkProtocol.TerrainOperation);
        SentTerrainOperationCount++;
    }

    private void ApplyTerrainOperation(string senderUserId, string json)
    {
        if (network == null || network.IsHost || senderUserId != network.HostUserId)
        {
            return;
        }

        ObjectHeadTerrainOperationMessage message =
            JsonUtility.FromJson<ObjectHeadTerrainOperationMessage>(json);
        if (message == null ||
            message.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion ||
            string.IsNullOrEmpty(message.messageId) ||
            !processedMessages.Add(message.messageId))
        {
            return;
        }

        if (terrainManager == null)
        {
            terrainManager = FindAny<TerrainManager>();
        }

        if (terrainManager == null)
        {
            Debug.LogWarning("[ObjectHead Network] Terrain operation arrived before TerrainManager was ready.");
            return;
        }

        terrainManager.ApplyOperation(message.operation);
        ReceivedTerrainOperationCount++;
    }

    private void ApplyCharacterSnapshot(int senderPlayerIndex, ObjectHeadGameplayMessage message)
    {
        if (!charactersById.TryGetValue(message.characterId, out TurnCharacterController character) ||
            GetPlayerIndex(character) != senderPlayerIndex ||
            senderPlayerIndex == localPlayerIndex ||
            message.turnSerial != turnManager.TurnSerial)
        {
            return;
        }

        ObjectHeadNetworkSmoother smoother=character.GetComponent<ObjectHeadNetworkSmoother>()??character.gameObject.AddComponent<ObjectHeadNetworkSmoother>();
        smoother.Push(new Vector2(message.positionX,message.positionY),new Vector2(message.velocityX,message.velocityY));

        AimController aim = character.GetComponent<AimController>();
        aim?.SetAimDirection(new Vector2(message.aimX, message.aimY));
        character.GetComponent<DemoSkillSelector>()?.SetSkillIndex(message.selectedSkillIndex,false);
        if(message.commonSlot>=0)character.GetComponent<CommonHeadUseController>()?.TrySelectCommonHeadSlot(message.commonSlot);
        ReceivedRemoteSnapshotCount++;
    }

    private void ApplyFireCommand(int senderPlayerIndex, ObjectHeadGameplayMessage message)
    {
        if (!charactersById.TryGetValue(message.characterId, out TurnCharacterController character) ||
            GetPlayerIndex(character) != senderPlayerIndex ||
            senderPlayerIndex == localPlayerIndex ||
            message.turnSerial != turnManager.TurnSerial ||
            character != turnManager.CurrentCharacter)
        {
            return;
        }

        character.GetComponent<DemoSkillSelector>()?.SetSkillIndex(message.selectedSkillIndex,false);
        var gourd=character.GetComponent<ObjectHeadGourd>();
        if(gourd!=null && GetPlayerIndex(network.HostUserId)!=senderPlayerIndex)return;
        if(gourd!=null){gourd.ConfirmedId=message.gourdChoiceId;gourd.Select(message.gourdChoiceId);}
        character.GetComponent<AimController>()?.SetAimDirection(new Vector2(message.aimX, message.aimY));
        SkillFireController fire = character.GetComponent<SkillFireController>();
        if (fire != null && fire.FireReplicated(message.normalizedPower))
        {
            ReceivedFireCommandCount++;
        }
    }

    private void HandleTurnStateChanged(TurnCharacterController unused)
    {
        if (network != null && network.IsHost)
        {
            SendTurnState();
        }
    }

    private void HandleTurnPhaseChanged(TurnPhase unused)
    {
        if (network != null && network.IsHost)
        {
            SendTurnState();
        }
    }

    private void SendTurnState()
    {
        if (turnManager == null || !network.IsHost)
        {
            return;
        }

        Send(new ObjectHeadGameplayMessage
        {
            kind = ObjectHeadGameplayMessageKind.TurnState,
            stateSequence=++hostStateSequence,
            inventoryStates=(startData.players ?? Array.Empty<ObjectHeadPlayerAssignment>()).Select(p=>new ObjectHeadInventoryState{
                playerIndex=p.playerIndex,slots=inventoryManager.GetInventory(p.playerIndex).CaptureSlots()}).ToArray(),
            worldItems=FindObjectsByType<CommonHeadItem>(FindObjectsSortMode.None).Select(item=>new ObjectHeadWorldItemState{
                id=item.NetworkId,type=item.ItemType,x=item.transform.position.x,y=item.transform.position.y}).ToArray(),
            matchOver = turnManager.IsMatchOver,
            winner = turnManager.WinningPlayerIndex,
            combatStates = charactersById.Select(pair => new ObjectHeadCombatState {
                characterId=pair.Key, hp=pair.Value.GetComponent<CharacterCombat>().CurrentHp,
                gourdCharges=pair.Value.GetComponent<ObjectHeadGourd>()?.Capture(),
                captiveTurns=pair.Value.GetComponent<ObjectHeadCaptivity>()?.RemainingOwnTurns??0,
                pending=pair.Value.GetComponent<CharacterCombat>().PendingDamage,shield=pair.Value.GetComponent<CharacterCombat>().ShieldAbsorption }).ToArray(),
            messageId = NextMessageId(),
            turnSerial = turnManager.TurnSerial,
            roundSerial = turnManager.RoundSerial,
            currentTurnIndex = turnManager.CurrentTurnIndex,
            currentPlayerIndex = turnManager.CurrentPlayerIndex,
            phase = (int)turnManager.CurrentPhase,
            actionUsed = turnManager.ActionUsedThisTurn,
            residualTimeActive = turnManager.IsResidualTimeActive,
            settlementPending = turnManager.IsTurnEndResolving || turnManager.IsSettlementTimeActive,
            remainingTurnSeconds = turnManager.RemainingTurnSeconds,
            remainingResidualSeconds = turnManager.RemainingResidualSeconds
        }, ObjectHeadNetworkProtocol.GameplayEvent);
    }

    public void RequestGourdFire(SkillFireController fire,float power)
    {
        var actor=fire!=null?fire.GetComponent<TurnCharacterController>():null;
        if(!IsReady || actor==null || GetPlayerIndex(actor)!=localPlayerIndex || !turnManager.CanCharacterFire(actor))return;
        var aim=actor.GetComponent<AimController>().AimDirection;
        Send(new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.FireCommand,gourdRequest=true,
            messageId=NextMessageId(),characterId=GetCharacterId(actor),turnSerial=turnManager.TurnSerial,
            selectedSkillIndex=actor.GetComponent<DemoSkillSelector>().SelectedSkillIndex,gourdChoiceId=actor.GetComponent<ObjectHeadGourd>()?.SelectedId,
            normalizedPower=power,aimX=aim.x,aimY=aim.y},ObjectHeadNetworkProtocol.GameplayCommand);
    }
    private void AcceptGourdFire(int senderPlayerIndex,ObjectHeadGameplayMessage message)
    {
        if(!network.IsHost || message.turnSerial!=turnManager.TurnSerial || !charactersById.TryGetValue(message.characterId??"",out var actor) ||
            GetPlayerIndex(actor)!=senderPlayerIndex || !turnManager.CanCharacterFire(actor) || message.selectedSkillIndex<0 || message.selectedSkillIndex>2 ||
            !Finite(message.normalizedPower) || message.normalizedPower<0 || message.normalizedPower>1 || !Finite(message.aimX) || !Finite(message.aimY))return;
        var gourd=actor.GetComponent<ObjectHeadGourd>();var aim=new Vector2(message.aimX,message.aimY);
        if(gourd==null || aim.sqrMagnitude<.0001f || (message.selectedSkillIndex==2 && !gourd.Select(message.gourdChoiceId)))return;
        actor.GetComponent<DemoSkillSelector>().SetSkillIndex(message.selectedSkillIndex,false);actor.GetComponent<AimController>().SetAimDirection(aim.normalized);
        if(!actor.GetComponent<SkillFireController>().FireReplicated(message.normalizedPower))return;
        message.gourdRequest=false;message.kind=ObjectHeadGameplayMessageKind.FireAccepted;message.messageId=NextMessageId();message.gourdChoiceId=gourd.LastResolvedId;
        Send(message,ObjectHeadNetworkProtocol.GameplayEvent);
    }

    public bool RequestCommonUse(CommonHeadUseController use,int slot,CommonHeadType type,float power)
    {
        var character=use!=null?use.GetComponent<TurnCharacterController>():null;
        if(!IsReady || character==null || GetPlayerIndex(character)!=localPlayerIndex || !turnManager.CanCharacterFire(character))return false;
        Vector2 aim=character.GetComponent<AimController>().AimDirection;
        var message=new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.CommonUseRequest,
            messageId=NextMessageId(),characterId=GetCharacterId(character),turnSerial=turnManager.TurnSerial,
            commonSlot=slot,commonType=type,normalizedPower=power,aimX=aim.x,aimY=aim.y};
        if(network.IsHost)return AcceptCommonUse(localPlayerIndex,message);
        Send(message,ObjectHeadNetworkProtocol.GameplayCommand);return true;
    }

    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private bool AcceptCommonUse(int senderPlayerIndex,ObjectHeadGameplayMessage message)
    {
        if(!network.IsHost || !charactersById.TryGetValue(message.characterId ?? "",out var character) ||
            senderPlayerIndex!=GetPlayerIndex(character) || character!=turnManager.CurrentCharacter ||
            message.turnSerial!=turnManager.TurnSerial || !turnManager.CanCharacterFire(character) ||
            ObjectHeadContent.Load()?.Common(message.commonType)==null ||
            !Finite(message.normalizedPower) || message.normalizedPower<0 || message.normalizedPower>1 ||
            !Finite(message.aimX) || !Finite(message.aimY))return false;
        var direction=new Vector2(message.aimX,message.aimY);
        if(!Finite(direction.sqrMagnitude)||direction.sqrMagnitude<.0001f)return false;
        bool accepted=character.GetComponent<CommonHeadUseController>().UseAuthoritative(
            message.commonSlot,message.commonType,message.normalizedPower,direction.normalized);
        if(accepted){AcceptedCommonUseCount++;SendTurnState();}
        return accepted;
    }

    private void HandleCommonUseCommitted(CommonHeadUseController use,int slot,CommonHeadType type,float power,Vector2 aim,Vector2 origin)
    {
        if(network==null || !network.IsHost)return;
        Send(new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.CommonUseAccepted,messageId=NextMessageId(),
            characterId=GetCharacterId(use.GetComponent<TurnCharacterController>()),turnSerial=turnManager.TurnSerial,
            commonSlot=slot,commonType=type,normalizedPower=power,aimX=aim.x,aimY=aim.y,positionX=origin.x,positionY=origin.y},
            ObjectHeadNetworkProtocol.GameplayEvent);
    }

    private void ApplyCommonState(ObjectHeadGameplayMessage message)
    {
        foreach(var state in message.inventoryStates ?? Array.Empty<ObjectHeadInventoryState>())
            if(state!=null && startData.players.Any(p=>p.playerIndex==state.playerIndex))
                inventoryManager.GetInventory(state.playerIndex).ApplyAuthoritativeSlots(state.slots);
        ReceivedInventoryStateCount++;
        if(message.worldItems==null)return;
        var present=new HashSet<string>();
        foreach(var state in message.worldItems)
        {
            if(state==null || string.IsNullOrEmpty(state.id) || !present.Add(state.id) || !Finite(state.x)||!Finite(state.y) ||
                ObjectHeadContent.Load()?.Common(state.type)==null)continue;
            if(!replicaItems.TryGetValue(state.id,out var item) || item==null)
            {
                item=CommonHeadItem.Create(state.type,new Vector2(state.x,state.y),CommonHeadItem.GetDefaultSprite(state.type));
                replicaItems[state.id]=item;
            }
            item.ApplyReplica(state);
        }
        foreach(var id in replicaItems.Keys.Where(id=>!present.Contains(id)).ToArray())
        {
            if(replicaItems[id]!=null)replicaItems[id].Retire();replicaItems.Remove(id);
        }
    }

    private async void Send(object message, long opCode)
    {
        try
        {
            await network.SendGameplayMessageAsync(opCode, message);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ObjectHead Network] Gameplay message failed: " + exception.Message);
        }
    }

    private string NextMessageId()
    {
        localMessageSequence++;
        return $"{network.LocalUserId}:{localMessageSequence}";
    }

    private int GetPlayerIndex(string userId)
    {
        ObjectHeadPlayerAssignment assignment = (startData.players ?? Array.Empty<ObjectHeadPlayerAssignment>())
            .FirstOrDefault(player => player.userId == userId);
        return assignment != null ? assignment.playerIndex : -1;
    }

    private static int GetPlayerIndex(TurnCharacterController character)
    {
        ObjectHeadTeamMember member = character != null ? character.GetComponent<ObjectHeadTeamMember>() : null;
        return member != null ? member.PlayerIndex : -1;
    }

    private static string GetCharacterId(TurnCharacterController character)
    {
        ObjectHeadTeamMember member = character != null ? character.GetComponent<ObjectHeadTeamMember>() : null;
        return member != null ? $"p{member.PlayerIndex}-s{member.TeamSlotIndex}" : string.Empty;
    }

    private static bool WasEndTurnPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private static T FindAny<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindAnyObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }
}

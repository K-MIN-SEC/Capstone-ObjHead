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
    private const float InputIntervalSeconds = 0.1f;
    private const float TurnStateIntervalSeconds = 0.1f;

    private readonly HashSet<string> processedMessages = new HashSet<string>();
    private readonly Queue<string> processedMessageOrder = new Queue<string>();
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
    private ObjectHeadTerrainSync terrainSync;
    private bool networkPending = true;
    private float lastHostStateAt;
    private bool hostStartPending = true;
    private readonly Dictionary<string,CommonHeadItem> replicaItems=new Dictionary<string,CommonHeadItem>();
    public int AcceptedCommonUseCount {get;private set;}
    public int ReceivedCommonUseCount {get;private set;}
    public int ReceivedInventoryStateCount {get;private set;}

    public bool IsReady { get; private set; }
    public int ReceivedRemoteSnapshotCount { get; private set; }
    public int ReceivedTurnStateCount { get; private set; }
    public int ReceivedFireCommandCount { get; private set; }
    public int ReceivedTerrainOperationCount => terrainSync != null ? terrainSync.AppliedOperations : 0;
    public int SentTerrainOperationCount => terrainSync != null ? terrainSync.SentOperations : 0;

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
            ObjectHeadPresentation.ImpactCommitted -= HandleConfirmedImpact;
        }

        SkillFireController.FireCommitted -= HandleLocalFireCommitted;
        SkillProjectile.LandingCommitted -= HandleProjectileLanding;
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
        {
            character.GetComponent<CharacterCombat>().UseExternalHealth = !network.IsHost;
            if (!network.IsHost) character.ConfigureAuthoritativeMotion();
            if (network.IsHost && GetPlayerIndex(character) != localPlayerIndex)
                character.UseNetworkInput = true;
        }
        terrainManager = FindAny<TerrainManager>();
        inventoryManager = FindAny<PlayerInventoryManager>();
        turnManager.ConfigureNetworkControl(localPlayerIndex, !network.IsHost, network.IsHost);
        turnManager.SetNetworkStartPending(true);
        network.GameplayMessageReceived += HandleGameplayMessage;
        turnManager.TurnStarted += HandleTurnStateChanged;
        turnManager.TurnPhaseChanged += HandleTurnPhaseChanged;
        SkillFireController.FireCommitted += HandleLocalFireCommitted;
        CommonHeadUseController.UseCommitted += HandleCommonUseCommitted;

        if (terrainManager != null)
        {
            terrainSync = gameObject.AddComponent<ObjectHeadTerrainSync>();
            terrainSync.Initialize(network, terrainManager, startData);
        }
        ObjectHeadPresentation.ImpactCommitted += HandleConfirmedImpact;
        SkillProjectile.LandingCommitted += HandleProjectileLanding;

        if (network.IsHost)
        {
            SendTurnState();
        }

        IsReady = true;
        Debug.Log($"[ObjectHead Network] Gameplay bridge ready. Local player=P{localPlayerIndex}, host={network.IsHost}.");
    }

    private void Update()
    {
        if (!IsReady || network == null || turnManager == null || !network.IsInMatch)
        {
            return;
        }

        bool pending = terrainSync == null || !terrainSync.InitialBarrierPassed ||
            (!network.IsHost && (hostStartPending || lastHostStateSequence == 0 || Time.unscaledTime-lastHostStateAt > 3f));
        if (pending != networkPending)
        {
            networkPending = pending;
            turnManager.SetNetworkStartPending(pending);
        }

        if (Time.unscaledTime >= nextSnapshotTime)
        {
            nextSnapshotTime = Time.unscaledTime + InputIntervalSeconds;
            SendOwnedMovementInput();
        }

        if (network.IsHost && Time.unscaledTime >= nextTurnStateTime)
        {
            nextTurnStateTime = Time.unscaledTime + TurnStateIntervalSeconds;
            SendTurnState();
        }

        if (!network.IsHost && !networkPending &&
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
        if (!IsReady || networkPending || network.IsHost || turnManager.CurrentPlayerIndex != localPlayerIndex || endTurnRequestSerial == turnManager.TurnSerial) return;
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

    private void SendOwnedMovementInput()
    {
        if (network.IsHost) return;
        TurnCharacterController character = turnManager.CurrentCharacter;
        if (character == null || GetPlayerIndex(character) != localPlayerIndex)
        {
            return;
        }

        AimController aim = character.GetComponent<AimController>();
        Vector2 direction = aim != null ? aim.AimDirection : Vector2.right;

        Send(new ObjectHeadGameplayMessage
        {
            kind = ObjectHeadGameplayMessageKind.MovementInput,
            messageId = NextMessageId(),
            characterId = GetCharacterId(character),
            turnSerial = turnManager.TurnSerial,
            moveX = character.InputMoveX,
            jumpHeld = character.InputJumpHeld,
            jumpPressed = character.ConsumeJumpPress(),
            commonSlot = character.GetComponent<CommonHeadUseController>()?.SelectedSlotIndex ?? -1,
            aimX = direction.x,
            aimY = direction.y,
            selectedSkillIndex = character.GetComponent<DemoSkillSelector>().SelectedSkillIndex
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
        if (!network.IsHost || !CanSubmitLocalAction(startData,GetPlayerIndex(character),localPlayerIndex,true))
        {
            return;
        }

        Send(new ObjectHeadGameplayMessage
        {
            kind = ObjectHeadGameplayMessageKind.FireAccepted,
            messageId = NextMessageId(),
            characterId = GetCharacterId(character),
            turnSerial = turnManager.TurnSerial,
            aimX = aimDirection.x,
            aimY = aimDirection.y,
            positionX = character.transform.position.x,
            positionY = character.transform.position.y,
            selectedSkillIndex = skillIndex,
            gourdChoiceId = character.GetComponent<ObjectHeadGourd>()?.LastResolvedId,
            normalizedPower = normalizedPower
        }, ObjectHeadNetworkProtocol.GameplayEvent);
    }

    private void HandleGameplayMessage(long opCode, string senderUserId, string json)
    {
        if (opCode == ObjectHeadNetworkProtocol.TerrainOperation || opCode == ObjectHeadNetworkProtocol.TerrainSync) return;

        ObjectHeadGameplayMessage message = JsonUtility.FromJson<ObjectHeadGameplayMessage>(json);
        if (message == null || message.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion ||
            message.matchEpoch != startData.startedAtUnixMilliseconds)
        {
            return;
        }

        if (string.IsNullOrEmpty(message.messageId) || message.messageId.Length > 160) return;
        string deduplicationKey = senderUserId + "/" + message.messageId;
        if (!processedMessages.Add(deduplicationKey))
        {
            return;
        }

        int senderPlayerIndex = GetPlayerIndex(senderUserId);
        if (!string.IsNullOrEmpty(message.messageId))
        {
            processedMessageOrder.Enqueue(deduplicationKey);
            while (processedMessageOrder.Count > 4096) processedMessages.Remove(processedMessageOrder.Dequeue());
        }
        switch (message.kind)
        {
            case ObjectHeadGameplayMessageKind.MovementInput:
                ApplyMovementInput(senderPlayerIndex, message);
                break;

            case ObjectHeadGameplayMessageKind.FireCommand:
                if (!network.IsHost) break;
                if(message.gourdRequest){if(network.IsHost)AcceptGourdFire(senderPlayerIndex,message);}
                else ApplyFireCommand(senderPlayerIndex, message);
                break;

            case ObjectHeadGameplayMessageKind.FireAccepted:
                if(!network.IsHost && senderUserId==network.HostUserId && message.turnSerial==turnManager.TurnSerial &&
                    charactersById.TryGetValue(message.characterId??"",out var borrowedActor) && borrowedActor==turnManager.CurrentCharacter &&
                    message.selectedSkillIndex>=0 && message.selectedSkillIndex<3)
                {
                    // This branch is guest-only and accepts the host's position, never a client's claim.
                    if (!Finite(message.positionX) || !Finite(message.positionY) || !Finite(message.normalizedPower) ||
                        !Finite(message.aimX) || !Finite(message.aimY)) break;
                    borrowedActor.transform.position=new Vector3(message.positionX,message.positionY,borrowedActor.transform.position.z);
                    borrowedActor.GetComponent<Rigidbody2D>().position=new Vector2(message.positionX,message.positionY);
                    borrowedActor.GetComponent<DemoSkillSelector>().SetSkillIndex(message.selectedSkillIndex,false);
                    borrowedActor.GetComponent<AimController>().SetAimDirection(new Vector2(message.aimX,message.aimY));
                    var gourd=borrowedActor.GetComponent<ObjectHeadGourd>();
                    if(gourd!=null)gourd.ConfirmedId=message.gourdChoiceId;
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
                    lastHostStateAt=Time.unscaledTime;
                    hostStartPending=message.networkStartPending;
                    ReceivedTurnStateCount++;
                    ApplyCommonState(message);
                    foreach (var state in message.combatStates ?? Array.Empty<ObjectHeadCombatState>())
                        if (charactersById.TryGetValue(state.characterId, out var target))
                        {
                            target.GetComponent<CharacterCombat>().ApplyNetworkHealth(state.hp, state.pending,state.shield);
                            target.GetComponent<ObjectHeadGourd>()?.Apply(state.gourdCharges);
                            ObjectHeadCaptivity.ApplyReplica(target,state.captiveTurns);
                        }
                    if (message.matchOver) turnManager.ApplyNetworkMatchResult(message.winner);
                    else turnManager.ApplyAuthoritativeTurnState(
                        message.currentTurnIndex,
                        message.turnSerial,
                        message.roundSerial,
                        (TurnPhase)message.phase,
                        message.remainingTurnSeconds,
                        message.remainingResidualSeconds,
                        message.residualTimeActive,
                        message.actionUsed,
                        message.settlementPending);
                    ApplyAuthoritativeCharacterPositions(message.combatStates);
                    var projectiles=FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None);
                    foreach (var state in message.projectiles ?? Array.Empty<ObjectHeadProjectileState>())
                        if (state!=null)
                            foreach (var projectile in projectiles)
                                if (projectile.NetworkId==state.id) projectile.ApplyNetworkState(state);
                }
                break;

            case ObjectHeadGameplayMessageKind.ProjectileLanded:
                if (!network.IsHost && senderUserId==network.HostUserId && Finite(message.positionX) && Finite(message.positionY))
                    foreach (var projectile in FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None))
                        if (projectile.NetworkId==message.projectileId)
                            projectile.ConfirmNetworkLanding(new Vector2(message.positionX,message.positionY),message.projectileCulled);
                break;

            case ObjectHeadGameplayMessageKind.ImpactPresentation:
                if (!network.IsHost && senderUserId == network.HostUserId &&
                    Finite(message.positionX) && Finite(message.positionY) && Finite(message.radius) && message.radius >= 0)
                    ObjectHeadPresentation.PresentConfirmedImpact(message.skillId,
                        new Vector2(message.positionX,message.positionY),message.radius);
                break;

            case ObjectHeadGameplayMessageKind.EndTurnRequest:
                if (network.IsHost &&
                    !networkPending &&
                    message.turnSerial == turnManager.TurnSerial &&
                    senderPlayerIndex == turnManager.CurrentPlayerIndex)
                {
                    turnManager.EndCurrentTurn();
                }
                break;
        }
    }

    private void HandleConfirmedImpact(int skillId, Vector2 point, float radius)
    {
        if (network == null || !network.IsHost) return;
        Send(new ObjectHeadGameplayMessage
        {
            kind=ObjectHeadGameplayMessageKind.ImpactPresentation, messageId=NextMessageId(),
            skillId=skillId,positionX=point.x,positionY=point.y,radius=radius
        },ObjectHeadNetworkProtocol.GameplayEvent);
    }

    private void HandleProjectileLanding(string projectileId, Vector2 point, bool culled)
    {
        if (network == null || !network.IsHost) return;
        Send(new ObjectHeadGameplayMessage { kind=ObjectHeadGameplayMessageKind.ProjectileLanded,
            messageId=NextMessageId(),projectileId=projectileId,positionX=point.x,positionY=point.y,projectileCulled=culled },
            ObjectHeadNetworkProtocol.GameplayEvent);
    }

    private void ApplyMovementInput(int senderPlayerIndex, ObjectHeadGameplayMessage message)
    {
        if (!network.IsHost || !charactersById.TryGetValue(message.characterId ?? string.Empty, out TurnCharacterController character) ||
            GetPlayerIndex(character) != senderPlayerIndex ||
            senderPlayerIndex == localPlayerIndex ||
            message.turnSerial != turnManager.TurnSerial ||
            senderPlayerIndex != turnManager.CurrentPlayerIndex ||
            character != turnManager.CurrentCharacter ||
            !Finite(message.moveX) || !Finite(message.aimX) || !Finite(message.aimY))
        {
            return;
        }

        character.SetNetworkInput(message.moveX, message.jumpHeld, message.jumpPressed);
        AimController aim = character.GetComponent<AimController>();
        Vector2 direction = new Vector2(message.aimX, message.aimY);
        if (direction.sqrMagnitude > 0.0001f) aim?.SetAimDirection(direction.normalized);
        if (message.selectedSkillIndex >= 0 && message.selectedSkillIndex < 3)
        {
            var selector=character.GetComponent<DemoSkillSelector>();
            if(selector.SelectedSkillIndex!=message.selectedSkillIndex)selector.SetSkillIndex(message.selectedSkillIndex,false);
        }
        character.GetComponent<CommonHeadUseController>()?.ApplyNetworkSelection(message.commonSlot);
        ReceivedRemoteSnapshotCount++;
    }

    private void ApplyAuthoritativeCharacterPositions(ObjectHeadCombatState[] states)
    {
        if (states != null && states.Length > 0) ReceivedRemoteSnapshotCount++;
        foreach (var state in states ?? Array.Empty<ObjectHeadCombatState>())
        {
            if (state == null || !charactersById.TryGetValue(state.characterId ?? string.Empty, out var character) ||
                !Finite(state.x) || !Finite(state.y) || !Finite(state.vx) || !Finite(state.vy)) continue;

            var body = character.GetComponent<Rigidbody2D>();
            if (body == null) continue;
            character.ApplyNetworkGrounded(state.grounded);
            var selector = character.GetComponent<DemoSkillSelector>();
            selector?.ApplyNetworkCooldowns(state.cooldowns);
            if (GetPlayerIndex(character) != localPlayerIndex || turnManager.CurrentCharacter != character)
            {
                if(selector!=null && selector.SelectedSkillIndex!=state.selectedSkill)selector.SetSkillIndex(state.selectedSkill,false);
                character.GetComponent<AimController>()?.SetAimDirection(new Vector2(state.aimX,state.aimY));
                character.GetComponent<CharacterVisual>()?.SetFacingRight(state.facingRight);
                character.GetComponent<CommonHeadUseController>()?.ApplyNetworkSelection(state.commonSlot);
            }
            Vector2 position = new Vector2(state.x, state.y);
            Vector2 velocity = new Vector2(state.vx, state.vy);
            if (!body.simulated) { body.position=position; character.transform.position=new Vector3(position.x,position.y,character.transform.position.z); continue; }
            var smoother = character.GetComponent<ObjectHeadNetworkSmoother>() ??
                           character.gameObject.AddComponent<ObjectHeadNetworkSmoother>();
            smoother.enabled = true;
            smoother.Push(position, velocity);
        }
    }

    private void ApplyFireCommand(int senderPlayerIndex, ObjectHeadGameplayMessage message)
    {
        if (!charactersById.TryGetValue(message.characterId, out TurnCharacterController character) ||
            GetPlayerIndex(character) != senderPlayerIndex ||
            senderPlayerIndex == localPlayerIndex ||
            message.turnSerial != turnManager.TurnSerial ||
            character != turnManager.CurrentCharacter ||
            message.selectedSkillIndex < 0 || message.selectedSkillIndex >= 3 ||
            !Finite(message.normalizedPower) || message.normalizedPower < 0 || message.normalizedPower > 1 ||
            !Finite(message.aimX) || !Finite(message.aimY))
        {
            return;
        }

        Vector2 direction = new Vector2(message.aimX, message.aimY);
        if (direction.sqrMagnitude < 0.0001f || (network.IsHost && !turnManager.CanCharacterFire(character))) return;
        character.GetComponent<DemoSkillSelector>()?.SetSkillIndex(message.selectedSkillIndex,false);
        var gourd=character.GetComponent<ObjectHeadGourd>();
        if(gourd!=null && GetPlayerIndex(network.HostUserId)!=senderPlayerIndex)return;
        if(gourd!=null){gourd.ConfirmedId=message.gourdChoiceId;gourd.Select(message.gourdChoiceId);}
        character.GetComponent<AimController>()?.SetAimDirection(direction.normalized);
        SkillFireController fire = character.GetComponent<SkillFireController>();
        if (fire != null && fire.FireReplicated(message.normalizedPower))
        {
            ReceivedFireCommandCount++;
            if (network.IsHost)
            {
                message.kind = ObjectHeadGameplayMessageKind.FireAccepted;
                message.positionX = character.transform.position.x;
                message.positionY = character.transform.position.y;
                message.messageId = NextMessageId();
                Send(message, ObjectHeadNetworkProtocol.GameplayEvent);
            }
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
            projectiles=FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Where(p=>p.IsFlying).Select(p=>p.CaptureNetworkState()).ToArray(),
            inventoryStates=(startData.players ?? Array.Empty<ObjectHeadPlayerAssignment>()).Select(p=>new ObjectHeadInventoryState{
                playerIndex=p.playerIndex,slots=inventoryManager.GetInventory(p.playerIndex).CaptureSlots()}).ToArray(),
            worldItems=FindObjectsByType<CommonHeadItem>(FindObjectsSortMode.None).Select(item=>new ObjectHeadWorldItemState{
                id=item.NetworkId,type=item.ItemType,x=item.transform.position.x,y=item.transform.position.y}).ToArray(),
            matchOver = turnManager.IsMatchOver,
            winner = turnManager.WinningPlayerIndex,
            combatStates = charactersById.Select(pair => new ObjectHeadCombatState {
                characterId=pair.Key, hp=pair.Value.GetComponent<CharacterCombat>().CurrentHp,
                grounded=pair.Value.IsGrounded,
                facingRight=pair.Value.GetComponent<CharacterVisual>().IsFacingRight,
                selectedSkill=pair.Value.GetComponent<DemoSkillSelector>().SelectedSkillIndex,
                commonSlot=pair.Value.GetComponent<CommonHeadUseController>()?.SelectedSlotIndex ?? -1,
                cooldowns=Enumerable.Range(0,3).Select(i=>pair.Value.GetComponent<DemoSkillSelector>().GetRemainingCooldown(i)).ToArray(),
                aimX=pair.Value.GetComponent<AimController>().AimDirection.x,
                aimY=pair.Value.GetComponent<AimController>().AimDirection.y,
                gourdCharges=pair.Value.GetComponent<ObjectHeadGourd>()?.Capture(),
                captiveTurns=pair.Value.GetComponent<ObjectHeadCaptivity>()?.RemainingOwnTurns??0,
                pending=pair.Value.GetComponent<CharacterCombat>().PendingDamage,shield=pair.Value.GetComponent<CharacterCombat>().ShieldAbsorption,
                x=pair.Value.transform.position.x,y=pair.Value.transform.position.y,
                vx=pair.Value.GetComponent<Rigidbody2D>().linearVelocity.x,
                vy=pair.Value.GetComponent<Rigidbody2D>().linearVelocity.y }).ToArray(),
            messageId = NextMessageId(),
            turnSerial = turnManager.TurnSerial,
            roundSerial = turnManager.RoundSerial,
            currentTurnIndex = turnManager.CurrentTurnIndex,
            currentPlayerIndex = turnManager.CurrentPlayerIndex,
            phase = (int)turnManager.CurrentPhase,
            actionUsed = turnManager.ActionUsedThisTurn,
            residualTimeActive = turnManager.IsResidualTimeActive,
            settlementPending = turnManager.IsTurnEndResolving || turnManager.IsSettlementTimeActive,
            networkStartPending = turnManager.IsNetworkStartPending,
            remainingTurnSeconds = turnManager.RemainingTurnSeconds,
            remainingResidualSeconds = turnManager.RemainingResidualSeconds
        }, ObjectHeadNetworkProtocol.GameplayEvent);
    }

    public void RequestFire(SkillFireController fire,float power)
    {
        var actor=fire!=null?fire.GetComponent<TurnCharacterController>():null;
        if(!IsReady || actor==null || GetPlayerIndex(actor)!=localPlayerIndex || !turnManager.CanCharacterFire(actor))return;
        var aim=actor.GetComponent<AimController>().AimDirection;
        var selector=actor.GetComponent<DemoSkillSelector>();
        bool gourdRequest=ObjectHeadGourd.IsMeta(selector.GetCurrentSkillSettings().effectType);
        Send(new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.FireCommand,gourdRequest=gourdRequest,
            messageId=NextMessageId(),characterId=GetCharacterId(actor),turnSerial=turnManager.TurnSerial,
            selectedSkillIndex=selector.SelectedSkillIndex,gourdChoiceId=actor.GetComponent<ObjectHeadGourd>()?.SelectedId,
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
        message.positionX=actor.transform.position.x;message.positionY=actor.transform.position.y;
        Send(message,ObjectHeadNetworkProtocol.GameplayEvent);
    }

    public bool RequestCommonUse(CommonHeadUseController use,int slot,CommonHeadType type,float power)
    {
        var character=use!=null?use.GetComponent<TurnCharacterController>():null;
        if(!IsReady || character==null || !CanSubmitLocalAction(startData,GetPlayerIndex(character),localPlayerIndex,network.IsHost) || !turnManager.CanCharacterFire(character))return false;
        Vector2 aim=character.GetComponent<AimController>().AimDirection;
        var message=new ObjectHeadGameplayMessage{kind=ObjectHeadGameplayMessageKind.CommonUseRequest,
            messageId=NextMessageId(),characterId=GetCharacterId(character),turnSerial=turnManager.TurnSerial,
            commonSlot=slot,commonType=type,normalizedPower=power,aimX=aim.x,aimY=aim.y};
        if(network.IsHost)return AcceptCommonUse(GetPlayerIndex(character),message);
        Send(message,ObjectHeadNetworkProtocol.GameplayCommand);return true;
    }

    public static bool CanSubmitLocalAction(GameStartData match,int actorPlayerIndex,int localPlayerIndex,bool isHost) =>
        actorPlayerIndex>0 && (actorPlayerIndex==localPlayerIndex || (isHost && match?.IsAiPlayer(actorPlayerIndex)==true));

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
        if (message is ObjectHeadGameplayMessage gameplay) gameplay.matchEpoch=startData.startedAtUnixMilliseconds;
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

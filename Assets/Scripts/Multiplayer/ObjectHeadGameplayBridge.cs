using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
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
    private GameStartData startData;
    private int localPlayerIndex;
    private float nextSnapshotTime;
    private float nextTurnStateTime;
    private int endTurnRequestSerial = -1;
    private long localMessageSequence;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForNetworkMatch()
    {
        if (GameStartData.Instance == null || FindAny<ObjectHeadGameplayBridge>() != null)
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

        SkillFireController.FireCommitted -= HandleLocalFireCommitted;
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
        turnManager.ConfigureNetworkControl(localPlayerIndex, !network.IsHost);
        network.GameplayMessageReceived += HandleGameplayMessage;
        turnManager.TurnStarted += HandleTurnStateChanged;
        turnManager.TurnPhaseChanged += HandleTurnPhaseChanged;
        SkillFireController.FireCommitted += HandleLocalFireCommitted;

        if (network.IsHost)
        {
            SendTurnState();
        }

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
            normalizedPower = normalizedPower
        }, ObjectHeadNetworkProtocol.GameplayCommand);
    }

    private void HandleGameplayMessage(long opCode, string senderUserId, string json)
    {
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
                ApplyFireCommand(senderPlayerIndex, message);
                break;

            case ObjectHeadGameplayMessageKind.TurnState:
                if (senderUserId == network.HostUserId && !network.IsHost)
                {
                    turnManager.ApplyAuthoritativeTurnState(
                        message.currentTurnIndex,
                        message.turnSerial,
                        message.roundSerial,
                        (TurnPhase)message.phase,
                        message.remainingTurnSeconds,
                        message.remainingResidualSeconds,
                        message.residualTimeActive,
                        message.actionUsed);
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

    private void ApplyCharacterSnapshot(int senderPlayerIndex, ObjectHeadGameplayMessage message)
    {
        if (!charactersById.TryGetValue(message.characterId, out TurnCharacterController character) ||
            GetPlayerIndex(character) != senderPlayerIndex ||
            senderPlayerIndex == localPlayerIndex ||
            message.turnSerial != turnManager.TurnSerial)
        {
            return;
        }

        character.transform.position = new Vector3(
            message.positionX,
            message.positionY,
            character.transform.position.z);
        Rigidbody2D body = character.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = new Vector2(message.velocityX, message.velocityY);
        }

        AimController aim = character.GetComponent<AimController>();
        aim?.SetAimDirection(new Vector2(message.aimX, message.aimY));
        character.GetComponent<DemoSkillSelector>()?.SetSkillIndex(message.selectedSkillIndex);
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

        character.GetComponent<DemoSkillSelector>()?.SetSkillIndex(message.selectedSkillIndex);
        character.GetComponent<AimController>()?.SetAimDirection(new Vector2(message.aimX, message.aimY));
        character.GetComponent<SkillFireController>()?.FireReplicated(message.normalizedPower);
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
            messageId = NextMessageId(),
            turnSerial = turnManager.TurnSerial,
            roundSerial = turnManager.RoundSerial,
            currentTurnIndex = turnManager.CurrentTurnIndex,
            currentPlayerIndex = turnManager.CurrentPlayerIndex,
            phase = (int)turnManager.CurrentPhase,
            actionUsed = turnManager.ActionUsedThisTurn,
            residualTimeActive = turnManager.IsResidualTimeActive,
            remainingTurnSeconds = turnManager.RemainingTurnSeconds,
            remainingResidualSeconds = turnManager.RemainingResidualSeconds
        }, ObjectHeadNetworkProtocol.GameplayEvent);
    }

    private async void Send(ObjectHeadGameplayMessage message, long opCode)
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

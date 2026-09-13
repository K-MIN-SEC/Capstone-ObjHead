using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ObjectHeadNetworkSmokeRunner : MonoBehaviour
{
    private const string ProfileArgument = "-objectHeadSmokeProfile";
    private const int TimeoutSeconds = 30;
    private bool gameStartObserved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        string profile = GetArgument(ProfileArgument);
        if (string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        GameObject root = new GameObject("ObjectHeadNetworkSmokeRunner");
        DontDestroyOnLoad(root);
        root.AddComponent<ObjectHeadNetworkSmokeRunner>();
    }

    private async void Start()
    {
        Application.runInBackground = true;
        string profile = GetArgument(ProfileArgument);
        try
        {
            ObjectHeadNetworkManager network = await WaitForNetworkManager();
            network.MatchStarting += HandleMatchStarting;

            Debug.Log($"[OBJECT_HEAD_SMOKE] {profile}: connecting");
            await network.ConnectAsync("Smoke" + profile, "smoke-" + profile);
            await network.StartQuickMatchAsync(2);

            await WaitUntil(
                () => network.IsInMatch && network.LobbyState?.players?.Length == 2,
                "two-player lobby synchronization");

            ObjectHeadLobbyState lobby = network.LobbyState;
            if (string.IsNullOrWhiteSpace(lobby.hostUserId) || lobby.players.Select(player => player.userId).Distinct().Count() != 2)
            {
                throw new InvalidOperationException("Lobby has an invalid host or duplicate players.");
            }

            Debug.Log($"[OBJECT_HEAD_SMOKE] {profile}: lobby ready, role={(network.IsHost ? "HOST" : "CLIENT")}, host={lobby.hostUserId}");
            await network.SetReadyAsync(true);
            await WaitUntil(
                () => network.LobbyState?.players?.Length == 2 && network.LobbyState.players.All(player => player.ready),
                "all players ready");

            if (network.IsHost)
            {
                if (!network.CanStartMatch(out string reason))
                {
                    throw new InvalidOperationException("Host cannot start: " + reason);
                }

                await network.StartGameAsync();
            }

            await WaitUntil(() => gameStartObserved || GameStartData.Instance != null, "game start broadcast");
            await WaitUntil(
                () => FindAny<ObjectHeadGameplayBridge>()?.IsReady == true,
                "gameplay bridge initialization");

            ObjectHeadGameplayBridge bridge = FindAny<ObjectHeadGameplayBridge>();
            if (network.IsHost)
            {
                await Task.Delay(1000);
                TurnManager turnManager = FindAny<TurnManager>();
                SkillFireController fire = turnManager?.CurrentCharacter?.GetComponent<SkillFireController>();
                DemoSkillSelector selector =
                    turnManager?.CurrentCharacter?.GetComponent<DemoSkillSelector>();
                if (fire == null || selector == null)
                {
                    throw new InvalidOperationException("Host fire controller or skill selector was not found.");
                }

                selector.SetSkillIndex(
                    selector.CharacterKind == ObjectHeadCharacterKind.Bulb ? 2 : 0);
                fire.Fire(0.65f);
                await WaitUntil(
                    () => bridge.SentTerrainOperationCount > 0,
                    "host terrain-changing projectile impact");
                await Task.Delay(1000);
            }
            else
            {
                await WaitUntil(
                    () => bridge.ReceivedRemoteSnapshotCount > 0 &&
                          bridge.ReceivedTurnStateCount > 0 &&
                          bridge.ReceivedFireCommandCount > 0 &&
                          bridge.ReceivedTerrainOperationCount > 0,
                    "gameplay snapshot, turn-state, fire-command, and terrain-operation synchronization");
            }

            Debug.Log($"[OBJECT_HEAD_SMOKE_PASS] {profile}: role={(network.IsHost ? "HOST" : "CLIENT")}, players={GameStartData.Instance?.playerCount ?? 0}, snapshots={bridge.ReceivedRemoteSnapshotCount}, turnStates={bridge.ReceivedTurnStateCount}, fireCommands={bridge.ReceivedFireCommandCount}, sentTerrainOperations={bridge.SentTerrainOperationCount}, receivedTerrainOperations={bridge.ReceivedTerrainOperationCount}, terrainSync=True");
            Application.Quit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[OBJECT_HEAD_SMOKE_FAIL] {profile}: {exception}");
            Application.Quit(2);
        }
    }

    private void HandleMatchStarting(GameStartData unused)
    {
        gameStartObserved = true;
    }

    private static async Task<ObjectHeadNetworkManager> WaitForNetworkManager()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        while (ObjectHeadNetworkManager.Instance == null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        return ObjectHeadNetworkManager.Instance != null
            ? ObjectHeadNetworkManager.Instance
            : throw new TimeoutException("Network manager creation timed out.");
    }

    private static async Task WaitUntil(Func<bool> condition, string operation)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        if (!condition())
        {
            throw new TimeoutException(operation + " timed out.");
        }
    }

    private static string GetArgument(string key)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return string.Empty;
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

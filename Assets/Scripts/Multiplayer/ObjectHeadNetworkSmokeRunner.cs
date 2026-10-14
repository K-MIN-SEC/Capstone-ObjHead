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
        if (string.IsNullOrWhiteSpace(profile) && !Environment.GetCommandLineArgs().Contains("-objectHeadEosLifecycleSmoke") &&
            !Environment.GetCommandLineArgs().Contains("-objectHeadLobbyAISmoke"))
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
            if (Environment.GetCommandLineArgs().Contains("-objectHeadLobbyAISmoke"))
            {
                await RunLobbyAISmoke(network);
                Application.Quit(0);
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("-objectHeadEosLifecycleSmoke"))
            {
                await network.ConnectAsync("ConnectionTest", "lifecycle-smoke");
                var settings=network.Config.DefaultRoomSettings;
                settings.mode=ObjectHeadMatchMode.Duel;settings.minPlayers=settings.maxPlayers=2;
                settings.fixedMapId="sky_terraces";settings.mapSelectionMode=ObjectHeadMapSelectionMode.Fixed;
                await network.CreateRoomAsync(settings,true,"TemporaryTest4931");
                if (!network.IsHost || network.LobbyState?.players?.Length!=1) throw new Exception("Private room host state missing");
                if (network.LobbyState.settings.fixedMapId!="sky_terraces") throw new Exception("Selected map lost");
                await network.DisconnectAsync();
                if(network.IsInMatch || network.IsConnected)throw new Exception("Disconnect did not finish");
                Debug.Log("[EOS_LIFECYCLE_PASS] Connected, created a temporary private room with the selected map, left and disconnected. This does not verify a second PC joining.");
                Application.Quit(0);
                return;
            }
            network.MatchStarting += HandleMatchStarting;

            Debug.Log($"[OBJECT_HEAD_SMOKE] {profile}: connecting");
            string nickname = GetArgument("-objectHeadSmokeNickname");
            await network.ConnectAsync(string.IsNullOrEmpty(nickname) ? "Smoke" + profile : nickname, "smoke-" + profile);
            var mode = Enum.TryParse(GetArgument("-objectHeadSmokeMode"), out ObjectHeadMatchMode requested) ? requested : ObjectHeadMatchMode.Duel;
            int count = ObjectHeadContent.Load().Mode(mode).players;
            await network.StartQuickMatchAsync(mode);

            await WaitUntil(
                () => network.IsInMatch && network.LobbyState?.players?.Length == count,
                "two-player lobby synchronization");

            ObjectHeadLobbyState lobby = network.LobbyState;
            if (string.IsNullOrWhiteSpace(lobby.hostUserId) || lobby.players.Select(player => player.userId).Distinct().Count() != count)
            {
                throw new InvalidOperationException("Lobby has an invalid host or duplicate players.");
            }

            Debug.Log($"[OBJECT_HEAD_SMOKE] {profile}: lobby ready, role={(network.IsHost ? "HOST" : "CLIENT")}, host={lobby.hostUserId}");
            var selection=ObjectHeadContent.Load().DefaultSelection(count);
            bool expanded=GetArgument("-objectHeadSmokeRoster")=="expanded";
            if(expanded)
            {
                var kinds=new[]{ObjectHeadCharacterKind.Revolver,ObjectHeadCharacterKind.Magnet,ObjectHeadCharacterKind.Kettle};
                selection=Enumerable.Range(0,selection.Length).Select(i=>kinds[i%kinds.Length]).ToArray();
            }
            await network.SetSelectionAsync(selection);
            await WaitUntil(() => network.LobbyState.players.Any(p=>p.userId==network.LocalUserId && ObjectHeadContent.Load().ValidSelection(p.characters,count)), "selection acknowledgement");
            await network.SetReadyAsync(true);
            await WaitUntil(
                () => network.LobbyState?.players?.Length == count && network.LobbyState.players.All(player => player.ready),
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
            await WaitUntil(() => FindAny<TurnManager>()?.IsNetworkStartPending == false,
                "all peers loaded and terrain checksums matched");
            if (network.IsHost)
            {
                await Task.Delay(1000);
                TurnManager turnManager = FindAny<TurnManager>();
                int hostSeat = GameStartData.Instance.players.First(p=>p.userId==network.LocalUserId).playerIndex;
                for(int i=0;i<turnManager.Characters.Length && (turnManager.CurrentPlayerIndex!=hostSeat || (expanded && turnManager.CurrentCharacter.GetComponent<DemoSkillSelector>().CharacterKind!=ObjectHeadCharacterKind.Revolver));i++) turnManager.EndCurrentTurn();
                await Task.Delay(300);
                SkillFireController fire = turnManager?.CurrentCharacter?.GetComponent<SkillFireController>();
                DemoSkillSelector selector =
                    turnManager?.CurrentCharacter?.GetComponent<DemoSkillSelector>();
                if (fire == null || selector == null)
                {
                    throw new InvalidOperationException("Host fire controller or skill selector was not found.");
                }

                selector.SetSkillIndex(
                    selector.CharacterKind == ObjectHeadCharacterKind.Bulb || selector.CharacterKind == ObjectHeadCharacterKind.Revolver ? 2 : 0);
                turnManager.CurrentCharacter.GetComponent<AimController>().SetAimDirection(Vector2.down);
                fire.Fire(0.15f);
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
                          bridge.ReceivedTerrainOperationCount > 0 &&
                          FindAny<ObjectHeadTerrainSync>()?.IsSynchronized == true,
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

    private static async Task RunLobbyAISmoke(ObjectHeadNetworkManager network)
    {
        // A real EOS host + bots, not a substitute for a three-PC internet test.
        await network.ConnectAsync("LobbyValidation", "lobby-ai-validation");
        try
        {
            var settings = network.Config.DefaultRoomSettings;
            settings.mode = ObjectHeadMatchMode.FreeForAll;
            settings.fixedMapId = "twin_citadels";
            settings.mapSelectionMode = ObjectHeadMapSelectionMode.Fixed;
            await network.CreateRoomAsync(settings, true, "LobbyValidation1014");
            await network.AddAIAsync(); await network.AddAIAsync(); await network.AddAIAsync();
            if (network.LobbyState.players.Length != 4 || network.LobbyState.players.Count(p => p.isAi) != 3)
                throw new Exception("Four-seat AI lobby did not fill");
            var duel = network.LobbyState.settings.Copy(); duel.mode = ObjectHeadMatchMode.Duel;
            bool rejected = false;
            try { await network.UpdateRoomSettingsAsync(duel); }
            catch (InvalidOperationException e) when (e.Message == "room_capacity_too_small") { rejected = true; }
            if (!rejected || network.IsRoomUpdating || network.LobbyState.settings.mode != ObjectHeadMatchMode.FreeForAll)
                throw new Exception("Capacity change was not rejected cleanly");
            await network.RemoveAIAsync();
            await network.SetAIDifficultyAsync(ObjectHeadAIDifficulty.Normal);
            await network.SetSelectionAsync(ObjectHeadContent.Load().DefaultSelection(4));
            await network.SetReadyAsync(true);
            if (!network.CanStartMatch(out string reason)) throw new Exception("Three-seat start blocked: " + reason);
            var title = FindAny<ObjectHeadTitleScreen>();
            string capture = GetArgument("-objectHeadCapture");
            if (title != null && !string.IsNullOrEmpty(capture))
            {
                System.IO.Directory.CreateDirectory(capture);
                await Task.Delay(250);
                title.StartCoroutine(ObjectHeadReleaseSmoke.Capture(System.IO.Path.Combine(capture, "online_three_seats.png")));
                await Task.Delay(800);
            }
            await network.StartGameAsync();
            await WaitUntil(() => FindAny<ObjectHeadGameplayBridge>()?.IsReady == true &&
                FindAny<TurnManager>()?.IsNetworkStartPending == false, "online AI start barrier");
            var turns = FindAny<TurnManager>();
            if (GameStartData.Instance.playerCount != 3 || turns.Characters.Length != 6)
                throw new Exception("Three-seat mode did not spawn six characters");
            int hostSeat = GameStartData.Instance.players.First(p => !p.isAi).playerIndex;
            if (turns.CurrentPlayerIndex == hostSeat) turns.EndCurrentTurn();
            await WaitUntil(() => ObjectHeadAIDirector.ActionsTaken > 0, "online host AI attack");
            if (ObjectHeadAIDirector.CandidatesEvaluated == 0) throw new Exception("AI did not plan a shot");
            Debug.Log("[LOBBY_AI_EOS_PASS] EOS private room; add/remove AI; full-room duel rejected; three-seat start; six characters; terrain barrier; host AI attack. Remote human joins NOT verified.");
        }
        finally { await network.DisconnectAsync(); }
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class ObjectHeadNetworkManager : MonoBehaviour
{
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private IClient client;
    private ISession session;
    private ISocket socket;
    private IMatch currentMatch;
    private IMatchmakerTicket matchmakerTicket;
    private ObjectHeadLobbyState lobbyState;
    private string displayName = "Player";
    private string localProfileId = "A";
    private ObjectHeadNetworkConfig config;
    private string scheme;
    private string host;
    private int port;
    private string serverKey;
    private string status = "Offline";
    private bool gameStarting;

    public static ObjectHeadNetworkManager Instance { get; private set; }

    public event Action StateChanged;
    public event Action<ObjectHeadLobbyState> LobbyChanged;
    public event Action<GameStartData> MatchStarting;
    public event Action<string> LogMessage;
    public event Action<long, string, string> GameplayMessageReceived;

    public string Status => status;
    public string MatchId => currentMatch != null ? currentMatch.Id : string.Empty;
    public string LocalUserId => session != null ? session.UserId : string.Empty;
    public string HostUserId => lobbyState != null ? lobbyState.hostUserId : string.Empty;
    public ObjectHeadLobbyState LobbyState => lobbyState;
    public bool IsConnected => socket != null && socket.IsConnected;
    public bool IsInMatch => currentMatch != null;
    public bool IsHost => !string.IsNullOrEmpty(LocalUserId) && LocalUserId == HostUserId;
    public bool IsMatchmaking => matchmakerTicket != null;
    public bool IsGameStarting => gameStarting;
    public ObjectHeadNetworkConfig Config => config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject root = new GameObject("ObjectHeadNetworkRuntime");
        DontDestroyOnLoad(root);
        root.AddComponent<ObjectHeadNetworkManager>();
        root.AddComponent<ObjectHeadNetworkDemoPanel>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        config = ObjectHeadNetworkConfig.LoadOrCreateRuntimeDefault();
        ObjectHeadServerProfile defaultProfile = config.GetProfile();
        scheme = defaultProfile.scheme;
        host = defaultProfile.host;
        port = defaultProfile.port;
        serverKey = defaultProfile.serverKey;
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private async void OnApplicationQuit()
    {
        if (socket != null && socket.IsConnected)
        {
            await socket.CloseAsync();
        }
    }

    public void ConfigureServer(string newScheme, string newHost, int newPort, string newServerKey)
    {
        if (IsConnected)
        {
            throw new InvalidOperationException("Disconnect before changing the server profile.");
        }

        ObjectHeadServerProfile fallback = config.GetProfile();
        scheme = string.IsNullOrWhiteSpace(newScheme) ? fallback.scheme : newScheme.Trim().ToLowerInvariant();
        host = string.IsNullOrWhiteSpace(newHost) ? fallback.host : newHost.Trim();
        port = Mathf.Clamp(newPort, 1, 65535);
        serverKey = string.IsNullOrWhiteSpace(newServerKey) ? fallback.serverKey : newServerKey.Trim();
        NotifyStateChanged();
    }

    public async Task ConnectAsync(string requestedDisplayName, string requestedProfileId)
    {
        if (IsConnected)
        {
            return;
        }

        displayName = SanitizeDisplayName(requestedDisplayName);
        localProfileId = string.IsNullOrWhiteSpace(requestedProfileId) ? "A" : requestedProfileId.Trim();
        SetStatus($"Connecting to {scheme}://{host}:{port}...");

        try
        {
            client = new Client(scheme, host, port, serverKey);
            string authId = BuildStableTestIdentity(localProfileId);
            session = await client.AuthenticateCustomAsync(authId, displayName, true);
            socket = Nakama.Socket.From(client);
            SubscribeSocket(socket);
            await socket.ConnectAsync(session, true);
            SetStatus($"Connected as {session.Username}");
        }
        catch (Exception exception)
        {
            SetStatus($"Connection failed: {exception.Message}");
            Log(exception.ToString());
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (matchmakerTicket != null && socket != null && socket.IsConnected)
            {
                await socket.RemoveMatchmakerAsync(matchmakerTicket);
            }

            if (currentMatch != null && socket != null && socket.IsConnected)
            {
                await socket.LeaveMatchAsync(currentMatch);
            }

            if (socket != null && socket.IsConnected)
            {
                await socket.CloseAsync();
            }
        }
        finally
        {
            matchmakerTicket = null;
            currentMatch = null;
            lobbyState = null;
            gameStarting = false;
            SetStatus("Offline");
            LobbyChanged?.Invoke(null);
        }
    }

    public async Task CreateRoomAsync(ObjectHeadRoomSettings requestedSettings)
    {
        EnsureConnected();
        await LeaveCurrentMatchAsync();

        currentMatch = await socket.CreateMatchAsync();
        lobbyState = new ObjectHeadLobbyState
        {
            revision = 1,
            matchId = currentMatch.Id,
            hostUserId = LocalUserId,
            settings = NormalizeSettings(requestedSettings),
            players = new[] { CreateLocalLobbyPlayer(false) }
        };

        SetStatus("Room created. Share the match ID.");
        NotifyLobbyChanged();
        await BroadcastLobbyStateAsync();
    }

    public async Task JoinRoomAsync(string matchId)
    {
        EnsureConnected();
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("A match ID is required.", nameof(matchId));
        }

        await LeaveCurrentMatchAsync();
        currentMatch = await socket.JoinMatchAsync(matchId.Trim());
        lobbyState = null;
        SetStatus("Joined room. Waiting for the host state...");
        NotifyStateChanged();
        await SendAsync(ObjectHeadNetworkProtocol.PlayerHello, new ObjectHeadPlayerHello { username = displayName });
    }

    public async Task StartQuickMatchAsync(int playerCount = 2)
    {
        EnsureConnected();
        await LeaveCurrentMatchAsync();

        int count = Mathf.Clamp(playerCount, 2, 4);
        Dictionary<string, string> stringProperties = new Dictionary<string, string>
        {
            { "game", "object_head_battle" },
            { "ruleset", ObjectHeadRoomSettings.CurrentRulesetVersion }
        };
        Dictionary<string, double> numericProperties = new Dictionary<string, double>
        {
            { "player_count", count }
        };
        string query = "+properties.game:object_head_battle +properties.ruleset:" + ObjectHeadRoomSettings.CurrentRulesetVersion;

        matchmakerTicket = await socket.AddMatchmakerAsync(query, count, count, stringProperties, numericProperties);
        SetStatus($"Matchmaking for {count} players...");
    }

    public async Task CancelQuickMatchAsync()
    {
        if (matchmakerTicket == null || socket == null || !socket.IsConnected)
        {
            return;
        }

        await socket.RemoveMatchmakerAsync(matchmakerTicket);
        matchmakerTicket = null;
        SetStatus("Matchmaking cancelled.");
    }

    public async Task SetReadyAsync(bool ready)
    {
        EnsureInMatch();
        if (IsHost)
        {
            UpsertPlayer(LocalUserId, displayName, ready);
            IncrementLobbyRevision();
            NotifyLobbyChanged();
            await BroadcastLobbyStateAsync();
            return;
        }

        await SendAsync(ObjectHeadNetworkProtocol.ReadyRequest, new ObjectHeadReadyRequest { ready = ready });
    }

    public async Task UpdateRoomSettingsAsync(ObjectHeadRoomSettings settings)
    {
        EnsureInMatch();
        ObjectHeadRoomSettings normalized = NormalizeSettings(settings);
        if (IsHost)
        {
            lobbyState.settings = normalized;
            IncrementLobbyRevision();
            NotifyLobbyChanged();
            await BroadcastLobbyStateAsync();
            return;
        }

        await SendAsync(ObjectHeadNetworkProtocol.SettingsRequest, new ObjectHeadSettingsRequest { settings = normalized });
    }

    public async Task StartGameAsync()
    {
        EnsureInMatch();
        if (!IsHost)
        {
            throw new InvalidOperationException("Only the room host can start the match.");
        }

        if (!CanStartMatch(out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        GameStartData startData = BuildGameStartData();
        await SendAsync(ObjectHeadNetworkProtocol.GameStart, startData);
        ApplyGameStart(startData);
    }

    public Task SendGameplayMessageAsync(long opCode, object payload)
    {
        if (opCode < ObjectHeadNetworkProtocol.GameplayCommand)
        {
            throw new ArgumentOutOfRangeException(nameof(opCode), "Gameplay opcodes must be 100 or greater.");
        }

        return SendAsync(opCode, payload);
    }

    public bool CanStartMatch(out string reason)
    {
        reason = string.Empty;
        if (lobbyState == null || lobbyState.settings == null)
        {
            reason = "Lobby state is not ready.";
            return false;
        }

        ObjectHeadLobbyPlayer[] players = lobbyState.players ?? Array.Empty<ObjectHeadLobbyPlayer>();
        if (players.Length < lobbyState.settings.minPlayers)
        {
            reason = $"Need at least {lobbyState.settings.minPlayers} players.";
            return false;
        }

        if (players.Any(player => !player.ready))
        {
            reason = "Every player must be ready.";
            return false;
        }

        return true;
    }

    private void SubscribeSocket(ISocket targetSocket)
    {
        targetSocket.ReceivedError += exception => Enqueue(() =>
        {
            SetStatus("Network error: " + exception.Message);
            Log(exception.ToString());
        });
        targetSocket.Closed += reason => Enqueue(() =>
        {
            currentMatch = null;
            matchmakerTicket = null;
            lobbyState = null;
            SetStatus("Disconnected: " + reason);
            NotifyLobbyChanged();
        });
        targetSocket.ReceivedMatchPresence += presenceEvent => Enqueue(() => HandlePresenceChanged(presenceEvent));
        targetSocket.ReceivedMatchState += matchState => Enqueue(() => HandleMatchState(matchState));
        targetSocket.ReceivedMatchmakerMatched += matched => Enqueue(() => JoinMatchedAsync(matched));
    }

    private async void JoinMatchedAsync(IMatchmakerMatched matched)
    {
        try
        {
            matchmakerTicket = null;
            currentMatch = await socket.JoinMatchAsync(matched);

            List<IUserPresence> participants = GetAllPresences();
            string electedHost = participants
                .Select(presence => presence.UserId)
                .Where(userId => !string.IsNullOrEmpty(userId))
                .OrderBy(userId => userId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (electedHost == LocalUserId)
            {
                lobbyState = new ObjectHeadLobbyState
                {
                    revision = 1,
                    matchId = currentMatch.Id,
                    hostUserId = electedHost,
                    settings = config.DefaultRoomSettings,
                    players = participants.Select(CreateLobbyPlayer).ToArray()
                };
                SetStatus("Quick match found. You are the host.");
                NotifyLobbyChanged();
                await BroadcastLobbyStateAsync();
            }
            else
            {
                lobbyState = null;
                SetStatus("Quick match found. Synchronizing lobby...");
                await SendAsync(ObjectHeadNetworkProtocol.PlayerHello, new ObjectHeadPlayerHello { username = displayName });
            }
        }
        catch (Exception exception)
        {
            SetStatus("Could not join matched game: " + exception.Message);
            Log(exception.ToString());
        }
    }

    private async void HandlePresenceChanged(IMatchPresenceEvent presenceEvent)
    {
        if (currentMatch == null || presenceEvent.MatchId != currentMatch.Id)
        {
            return;
        }

        currentMatch.UpdatePresences(presenceEvent);
        if (lobbyState == null || !IsHost)
        {
            return;
        }

        foreach (IUserPresence joined in presenceEvent.Joins)
        {
            UpsertPlayer(joined.UserId, joined.Username, false);
        }

        foreach (IUserPresence left in presenceEvent.Leaves)
        {
            RemovePlayer(left.UserId);
        }

        IncrementLobbyRevision();
        NotifyLobbyChanged();
        await BroadcastLobbyStateAsync();
    }

    private async void HandleMatchState(IMatchState matchState)
    {
        if (currentMatch == null || matchState.MatchId != currentMatch.Id)
        {
            return;
        }

        try
        {
            string json = Encoding.UTF8.GetString(matchState.State);
            string senderUserId = matchState.UserPresence != null ? matchState.UserPresence.UserId : string.Empty;

            switch (matchState.OpCode)
            {
                case ObjectHeadNetworkProtocol.PlayerHello:
                    if (IsHost)
                    {
                        ObjectHeadPlayerHello hello = JsonUtility.FromJson<ObjectHeadPlayerHello>(json);
                        UpsertPlayer(senderUserId, hello.username, false);
                        IncrementLobbyRevision();
                        NotifyLobbyChanged();
                        await BroadcastLobbyStateAsync();
                    }
                    break;

                case ObjectHeadNetworkProtocol.LobbyState:
                    ObjectHeadLobbyState receivedLobby = JsonUtility.FromJson<ObjectHeadLobbyState>(json);
                    if (receivedLobby == null || receivedLobby.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion)
                    {
                        return;
                    }

                    if (lobbyState != null && !string.IsNullOrEmpty(lobbyState.hostUserId) &&
                        senderUserId != lobbyState.hostUserId)
                    {
                        return;
                    }

                    lobbyState = receivedLobby;
                    SetStatus("Lobby synchronized.");
                    NotifyLobbyChanged();
                    break;

                case ObjectHeadNetworkProtocol.ReadyRequest:
                    if (IsHost)
                    {
                        ObjectHeadReadyRequest readyRequest = JsonUtility.FromJson<ObjectHeadReadyRequest>(json);
                        UpsertPlayer(senderUserId, FindPresenceUsername(senderUserId), readyRequest.ready);
                        IncrementLobbyRevision();
                        NotifyLobbyChanged();
                        await BroadcastLobbyStateAsync();
                    }
                    break;

                case ObjectHeadNetworkProtocol.SettingsRequest:
                    // Reserved for a later host-permission flow. Non-hosts cannot mutate demo rooms.
                    break;

                case ObjectHeadNetworkProtocol.GameStart:
                    if (lobbyState == null || senderUserId != lobbyState.hostUserId)
                    {
                        return;
                    }

                    GameStartData startData = JsonUtility.FromJson<GameStartData>(json);
                    ApplyGameStart(startData);
                    break;

                default:
                    if (matchState.OpCode >= ObjectHeadNetworkProtocol.GameplayCommand)
                    {
                        GameplayMessageReceived?.Invoke(matchState.OpCode, senderUserId, json);
                    }
                    break;
            }
        }
        catch (Exception exception)
        {
            SetStatus("Invalid network message: " + exception.Message);
            Log(exception.ToString());
        }
    }

    private async Task BroadcastLobbyStateAsync()
    {
        if (lobbyState == null)
        {
            return;
        }

        await SendAsync(ObjectHeadNetworkProtocol.LobbyState, lobbyState);
    }

    private async Task SendAsync(long opCode, object payload)
    {
        EnsureInMatch();
        string json = JsonUtility.ToJson(payload);
        await socket.SendMatchStateAsync(currentMatch.Id, opCode, json);
    }

    private GameStartData BuildGameStartData()
    {
        ObjectHeadRoomSettings settings = lobbyState.settings;
        int seed = new System.Random(unchecked(Environment.TickCount ^ lobbyState.revision)).Next(1, int.MaxValue);
        string mapId = settings.fixedMapId;
        if (settings.mapSelectionMode == ObjectHeadMapSelectionMode.Random)
        {
            string[] pool = settings.randomMapPool != null
                ? settings.randomMapPool.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
                : Array.Empty<string>();
            mapId = pool.Length > 0 ? pool[seed % pool.Length] : config.FallbackMapId;
        }

        ObjectHeadLobbyPlayer[] orderedPlayers = (lobbyState.players ?? Array.Empty<ObjectHeadLobbyPlayer>())
            .OrderBy(player => player.playerIndex)
            .ThenBy(player => player.userId, StringComparer.Ordinal)
            .ToArray();

        return new GameStartData
        {
            matchId = currentMatch.Id,
            rulesetVersion = settings.rulesetVersion,
            playerCount = orderedPlayers.Length,
            mapSelectionMode = settings.mapSelectionMode,
            mapId = mapId,
            mapSeed = seed,
            characterSpawnSeed = seed,
            startingPlayerIndex = 1,
            startedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            players = orderedPlayers.Select(player => new ObjectHeadPlayerAssignment
            {
                userId = player.userId,
                username = player.username,
                playerIndex = player.playerIndex
            }).ToArray()
        };
    }

    private void ApplyGameStart(GameStartData startData)
    {
        if (startData == null || startData.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion || gameStarting)
        {
            return;
        }

        gameStarting = true;
        GameStartData.Apply(startData);
        SetStatus($"Starting {startData.mapId} (seed {startData.mapSeed})...");
        MatchStarting?.Invoke(startData);
        StartCoroutine(ReloadPlayableScene());
    }

    private System.Collections.IEnumerator ReloadPlayableScene()
    {
        yield return null;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
        gameStarting = false;
    }

    private async Task LeaveCurrentMatchAsync()
    {
        if (matchmakerTicket != null)
        {
            await socket.RemoveMatchmakerAsync(matchmakerTicket);
            matchmakerTicket = null;
        }

        if (currentMatch != null)
        {
            await socket.LeaveMatchAsync(currentMatch);
            currentMatch = null;
            lobbyState = null;
            NotifyLobbyChanged();
        }
    }

    private List<IUserPresence> GetAllPresences()
    {
        List<IUserPresence> result = new List<IUserPresence>();
        if (currentMatch == null)
        {
            return result;
        }

        if (currentMatch.Self != null)
        {
            result.Add(currentMatch.Self);
        }

        foreach (IUserPresence presence in currentMatch.Presences ?? Array.Empty<IUserPresence>())
        {
            if (result.All(existing => existing.UserId != presence.UserId))
            {
                result.Add(presence);
            }
        }

        return result;
    }

    private ObjectHeadLobbyPlayer CreateLobbyPlayer(IUserPresence presence)
    {
        return new ObjectHeadLobbyPlayer
        {
            userId = presence.UserId,
            username = string.IsNullOrWhiteSpace(presence.Username) ? "Player" : presence.Username,
            ready = false,
            playerIndex = 0
        };
    }

    private ObjectHeadLobbyPlayer CreateLocalLobbyPlayer(bool ready)
    {
        return new ObjectHeadLobbyPlayer
        {
            userId = LocalUserId,
            username = displayName,
            ready = ready,
            playerIndex = 1
        };
    }

    private void UpsertPlayer(string userId, string username, bool ready)
    {
        if (lobbyState == null || string.IsNullOrEmpty(userId))
        {
            return;
        }

        List<ObjectHeadLobbyPlayer> players = (lobbyState.players ?? Array.Empty<ObjectHeadLobbyPlayer>()).ToList();
        ObjectHeadLobbyPlayer existing = players.FirstOrDefault(player => player.userId == userId);
        if (existing == null)
        {
            players.Add(new ObjectHeadLobbyPlayer
            {
                userId = userId,
                username = string.IsNullOrWhiteSpace(username) ? "Player" : username,
                ready = ready
            });
        }
        else
        {
            existing.username = string.IsNullOrWhiteSpace(username) ? existing.username : username;
            existing.ready = ready;
        }

        lobbyState.players = players
            .OrderBy(player => player.userId, StringComparer.Ordinal)
            .Select((player, index) =>
            {
                player.playerIndex = index + 1;
                return player;
            })
            .ToArray();
    }

    private void RemovePlayer(string userId)
    {
        if (lobbyState == null)
        {
            return;
        }

        lobbyState.players = (lobbyState.players ?? Array.Empty<ObjectHeadLobbyPlayer>())
            .Where(player => player.userId != userId)
            .OrderBy(player => player.userId, StringComparer.Ordinal)
            .Select((player, index) =>
            {
                player.playerIndex = index + 1;
                return player;
            })
            .ToArray();
    }

    private string FindPresenceUsername(string userId)
    {
        IUserPresence presence = GetAllPresences().FirstOrDefault(item => item.UserId == userId);
        return presence != null ? presence.Username : "Player";
    }

    private void IncrementLobbyRevision()
    {
        if (lobbyState != null)
        {
            lobbyState.revision++;
        }
    }

    private ObjectHeadRoomSettings NormalizeSettings(ObjectHeadRoomSettings source)
    {
        ObjectHeadRoomSettings result = source != null ? source.Copy() : config.DefaultRoomSettings;
        result.rulesetVersion = string.IsNullOrWhiteSpace(result.rulesetVersion)
            ? ObjectHeadRoomSettings.CurrentRulesetVersion
            : result.rulesetVersion.Trim();
        result.minPlayers = Mathf.Clamp(result.minPlayers, 2, 4);
        result.maxPlayers = Mathf.Clamp(result.maxPlayers, result.minPlayers, 4);
        result.fixedMapId = string.IsNullOrWhiteSpace(result.fixedMapId) ? config.FallbackMapId : result.fixedMapId.Trim();
        result.randomMapPool = result.randomMapPool != null && result.randomMapPool.Length > 0
            ? result.randomMapPool.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct().ToArray()
            : new[] { result.fixedMapId };
        return result;
    }

    private static string SanitizeDisplayName(string value)
    {
        string trimmed = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        return trimmed.Length <= 20 ? trimmed : trimmed.Substring(0, 20);
    }

    private static string BuildStableTestIdentity(string profileId)
    {
        string source = $"object-head|{SystemInfo.deviceUniqueIdentifier}|{Application.dataPath}|{profileId}";
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
            StringBuilder builder = new StringBuilder("ohb-");
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect to Nakama first.");
        }
    }

    private void EnsureInMatch()
    {
        EnsureConnected();
        if (currentMatch == null)
        {
            throw new InvalidOperationException("Join a room first.");
        }
    }

    private void Enqueue(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

    private void SetStatus(string value)
    {
        status = value;
        Log(value);
        NotifyStateChanged();
    }

    private void Log(string value)
    {
        Debug.Log("[ObjectHead Network] " + value);
        LogMessage?.Invoke(value);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void NotifyLobbyChanged()
    {
        LobbyChanged?.Invoke(lobbyState);
        NotifyStateChanged();
    }
}

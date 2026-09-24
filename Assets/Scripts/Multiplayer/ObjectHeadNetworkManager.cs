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
public sealed partial class ObjectHeadNetworkManager : MonoBehaviour
{
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private IClient client;
    private ISession session;
    private ISocket socket;
    private IMatch currentMatch;
    private IMatchmakerTicket matchmakerTicket;
    private ObjectHeadEosTransport eos;
    private bool eosMatchmaking;
    private int pendingMatchPlayerCount = 2;
    private ObjectHeadLobbyState lobbyState;
    private string displayName = "Player";
    private string localProfileId = "A";
    private ObjectHeadNetworkConfig config;
    private string scheme;
    private string host;
    private int port;
    private string serverKey;
    private string roomCode;
    private string status = "Offline";
    private bool gameStarting;

    public static ObjectHeadNetworkManager Instance { get; private set; }

    public event Action StateChanged;
    public event Action<ObjectHeadLobbyState> LobbyChanged;
    public event Action<GameStartData> MatchStarting;
    public event Action<string> LogMessage;
    public event Action<long, string, string> GameplayMessageReceived;
    public event Action<string> SessionInterrupted;

    public string Status => status;
    public bool UseEpicOnlineServices => config != null && config.UseEpicOnlineServices && !UseDedicatedAuthority;
    public string MatchId => UseEpicOnlineServices ? eos != null ? eos.LobbyId : string.Empty : currentMatch != null ? currentMatch.Id : string.Empty;
    public string RoomCode => !string.IsNullOrWhiteSpace(roomCode)
        ? roomCode
        : lobbyState != null && !string.IsNullOrWhiteSpace(lobbyState.roomCode)
            ? lobbyState.roomCode
            : MatchId;
    public string LocalUserId => UseEpicOnlineServices ? eos != null ? eos.LocalUserId : string.Empty : session != null ? session.UserId : string.Empty;
    public string HostUserId => lobbyState != null ? lobbyState.hostUserId : string.Empty;
    public ObjectHeadLobbyState LobbyState => lobbyState;
    public bool IsConnected => UseEpicOnlineServices ? eos != null && eos.Connected : socket != null && socket.IsConnected;
    public bool IsInMatch => UseEpicOnlineServices ? eos != null && eos.InRoom : currentMatch != null;
    public bool IsHost => !string.IsNullOrEmpty(LocalUserId) && LocalUserId == HostUserId;
    public bool IsMatchmaking => UseEpicOnlineServices ? eosMatchmaking : matchmakerTicket != null;
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
        if (HasCommandLineFlag("-objectHeadDebugPanel"))
        {
            root.AddComponent<ObjectHeadNetworkDemoPanel>();
        }
    }

    private static bool HasCommandLineFlag(string flag)
    {
        return Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
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
        // Optional deployment overrides. The checked-in profile remains editable in the Inspector.
        string overrideHost=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_HOST");
        string overridePort=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_PORT");
        string overrideScheme=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_SCHEME");
        string overrideKey=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_KEY");
        if(!string.IsNullOrEmpty(overrideHost) || !string.IsNullOrEmpty(overridePort) ||
           !string.IsNullOrEmpty(overrideScheme) || !string.IsNullOrEmpty(overrideKey))
        {
            if(!string.IsNullOrEmpty(overridePort) && (!int.TryParse(overridePort,out int candidate) || candidate<1 || candidate>65535))
                throw new InvalidOperationException("Invalid OBJECT_HEAD_SERVER_PORT");
            if(!string.IsNullOrEmpty(overrideScheme) && overrideScheme!="http" && overrideScheme!="https")
                throw new InvalidOperationException("Invalid OBJECT_HEAD_SERVER_SCHEME");
            ConfigureServer(overrideScheme,overrideHost,string.IsNullOrEmpty(overridePort)?port:int.Parse(overridePort),overrideKey);
        }
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
        if (UseEpicOnlineServices)
        {
            SetStatus("Connecting to Epic Online Services...");
            eos = GetComponent<ObjectHeadEosTransport>();
            if (eos == null) eos = gameObject.AddComponent<ObjectHeadEosTransport>();
            eos.MessageReceived -= HandleEosMessage;
            eos.MessageReceived += HandleEosMessage;
            eos.MemberLeft -= HandleEosMemberLeft;
            eos.MemberLeft += HandleEosMemberLeft;
            eos.Disconnected -= HandleEosDisconnected;
            eos.Disconnected += HandleEosDisconnected;
            try { await eos.ConnectAsync(displayName); SetStatus("Connected to Epic Online Services."); }
            catch (Exception exception) { SetStatus("EOS connection failed: " + exception.Message); throw; }
            return;
        }
        SetStatus($"Connecting to {scheme}://{host}:{port}...");

        try
        {
            client = new Client(scheme, host, port, serverKey);
            string authId = BuildStableTestIdentity(localProfileId);
            // Nakama usernames are globally unique; the lobby nickname is not an account identifier.
            session = await client.AuthenticateCustomAsync(authId, null, true);
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
        if (UseEpicOnlineServices)
        {
            if (eos != null) await eos.DisconnectAsync();
            eosMatchmaking = false;
            lobbyState = null;
            roomCode = string.Empty;
            gameStarting = false;
            GameStartData.Clear();
            SetStatus("Offline");
            LobbyChanged?.Invoke(null);
            return;
        }
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
            roomCode = string.Empty;
            gameStarting = false;
            GameStartData.Clear();
            SetStatus("Offline");
            LobbyChanged?.Invoke(null);
        }
    }

    public async Task CreateRoomAsync(ObjectHeadRoomSettings requestedSettings,bool isPrivate=false,string password=null)
    {
        if (UseEpicOnlineServices)
        {
            EnsureConnected();
            ObjectHeadRoomSettings normalized = NormalizeSettings(requestedSettings);
            ObjectHeadEosTransport.Room room = await eos.CreateRoomAsync(normalized, isPrivate, password);
            roomCode = room.code;
            lobbyState = new ObjectHeadLobbyState
            {
                revision = 1,
                matchId = room.lobbyId,
                roomCode = room.code,
                hostUserId = LocalUserId,
                settings = normalized,
                players = new[] { CreateLocalLobbyPlayer(false) }
            };
            SetStatus("EOS room created: " + room.code);
            NotifyLobbyChanged();
            return;
        }
        if (UseDedicatedAuthority) { await CreateAuthoritativeRoomAsync(requestedSettings,isPrivate,password); return; }
        if(isPrivate)throw new InvalidOperationException("private_requires_authority");
        EnsureConnected();
        await LeaveCurrentMatchAsync();

        currentMatch = await socket.CreateMatchAsync();
        roomCode = await RegisterRoomCodeAsync(currentMatch.Id);
        lobbyState = new ObjectHeadLobbyState
        {
            revision = 1,
            matchId = currentMatch.Id,
            roomCode = roomCode,
            hostUserId = LocalUserId,
            settings = NormalizeSettings(requestedSettings),
            players = new[] { CreateLocalLobbyPlayer(false) }
        };

        SetStatus("Room created. Share the match ID.");
        NotifyLobbyChanged();
        await BroadcastLobbyStateAsync();
    }

    public async Task JoinRoomAsync(string roomCodeOrMatchId,string password=null)
    {
        if (UseEpicOnlineServices)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(roomCodeOrMatchId)) throw new ArgumentException("A room code is required.");
            ObjectHeadEosTransport.Room room = await eos.JoinRoomAsync(roomCodeOrMatchId.Trim(), password);
            roomCode = room.code;
            lobbyState = null;
            SetStatus("EOS room joined. Synchronizing host state...");
            await SendAsync(ObjectHeadNetworkProtocol.PlayerHello, new ObjectHeadPlayerHello { username = displayName });
            return;
        }
        if (UseDedicatedAuthority) { await JoinAuthoritativeRoomAsync(roomCodeOrMatchId,password); return; }
        if(!string.IsNullOrEmpty(password))throw new InvalidOperationException("private_requires_authority");
        EnsureConnected();
        if (string.IsNullOrWhiteSpace(roomCodeOrMatchId))
        {
            throw new ArgumentException("A room code is required.", nameof(roomCodeOrMatchId));
        }

        await LeaveCurrentMatchAsync();
        string requested = roomCodeOrMatchId.Trim();
        string matchId = await ResolveMatchIdAsync(requested);
        currentMatch = await socket.JoinMatchAsync(matchId);
        roomCode = LooksLikeFriendlyRoomCode(requested) ? requested.ToUpperInvariant() : string.Empty;
        lobbyState = null;
        SetStatus("Joined room. Waiting for the host state...");
        NotifyStateChanged();
        await SendAsync(ObjectHeadNetworkProtocol.PlayerHello, new ObjectHeadPlayerHello { username = displayName });
    }

    public Task StartQuickMatchAsync(int playerCount = 2) => StartQuickMatchAsync(playerCount == 2 ? ObjectHeadMatchMode.Duel : playerCount == 4 ? ObjectHeadMatchMode.FreeForAll : throw new ArgumentException("unsupported_mode"));

    private ObjectHeadMatchMode pendingMatchMode;
    public async Task StartQuickMatchAsync(ObjectHeadMatchMode mode)
    {
        if (UseEpicOnlineServices)
        {
            EnsureConnected();
            await LeaveCurrentMatchAsync();
            eosMatchmaking = true;
            NotifyStateChanged();
            try
            {
                ObjectHeadEosTransport.Room room = (await eos.FindRoomsAsync())
                    .Where(candidate => candidate.mode == mode)
                    .OrderByDescending(candidate => candidate.players)
                    .FirstOrDefault();
                if (room != null) await JoinRoomAsync(room.code);
                else
                {
                    ObjectHeadRoomSettings settings = config.DefaultRoomSettings;
                    settings.mode = mode;
                    await CreateRoomAsync(settings);
                }
            }
            finally { eosMatchmaking = false; NotifyStateChanged(); }
            return;
        }
        EnsureConnected();
        await LeaveCurrentMatchAsync();

        var definition = ObjectHeadContent.Load().Mode(mode) ?? throw new ArgumentException("unsupported_mode");
        int count = definition.players;
        pendingMatchMode = mode;
        pendingMatchPlayerCount = count;
        Dictionary<string, string> stringProperties = new Dictionary<string, string>
        {
            { "game", "object_head_battle" },
            { "ruleset", UseDedicatedAuthority ? AuthorityRuleset : ObjectHeadRoomSettings.CurrentRulesetVersion },
            { "mode", mode.ToString() }
        };
        Dictionary<string, double> numericProperties = new Dictionary<string, double>
        {
            { "player_count", count }
        };
        string query = "+properties.game:object_head_battle +properties.ruleset:" + (UseDedicatedAuthority ? AuthorityRuleset : ObjectHeadRoomSettings.CurrentRulesetVersion) + " +properties.mode:" + mode;

        matchmakerTicket = await socket.AddMatchmakerAsync(query, count, count, stringProperties, numericProperties);
        SetStatus($"Matchmaking for {count} players...");
    }

    public async Task CancelQuickMatchAsync()
    {
        if (UseEpicOnlineServices)
        {
            eosMatchmaking = false;
            await LeaveCurrentMatchAsync();
            SetStatus("Matchmaking cancelled.");
            return;
        }
        if (matchmakerTicket == null || socket == null || !socket.IsConnected)
        {
            return;
        }

        await socket.RemoveMatchmakerAsync(matchmakerTicket);
        matchmakerTicket = null;
        SetStatus("Matchmaking cancelled.");
    }

    public async Task SetSelectionAsync(ObjectHeadCharacterKind[] selection)
    {
        if(UseDedicatedAuthority){await SendAsync(6,new ObjectHeadSelectionRequest{characters=selection});return;}
        EnsureInMatch();
        if (GameStartData.Instance != null) throw new InvalidOperationException("match_in_progress");
        if (lobbyState == null || !ObjectHeadContent.Load().ValidSelection(selection, lobbyState.settings.maxPlayers))
            throw new InvalidOperationException("selection_required");
        if (IsHost)
        {
            ApplySelection(LocalUserId, selection);
            await BroadcastLobbyStateAsync();
        }
        else await SendAsync(ObjectHeadNetworkProtocol.SelectionRequest, new ObjectHeadSelectionRequest { characters = selection });
    }

    private void ApplySelection(string userId, ObjectHeadCharacterKind[] selection)
    {
        ObjectHeadLobbyPlayer player = lobbyState?.players?.FirstOrDefault(p => p.userId == userId);
        if (player == null || !ObjectHeadContent.Load().ValidSelection(selection, lobbyState.settings.maxPlayers)) return;
        player.characters = (ObjectHeadCharacterKind[])selection.Clone();
        player.ready = false;
        IncrementLobbyRevision();
        NotifyLobbyChanged();
    }

    public async Task ReturnToLobbyAsync()
    {
        if(UseDedicatedAuthority){await SendAsync(7,new ObjectHeadReadyRequest());return;}
        EnsureInMatch();
        if (!IsHost) throw new InvalidOperationException("host_only");
        if (UseEpicOnlineServices) await eos.SetRoomOpenAsync(true);
        await SendAsync(ObjectHeadNetworkProtocol.ReturnToLobby, new ObjectHeadReadyRequest());
        ApplyReturnToLobby();
        await BroadcastLobbyStateAsync();
    }

    private void ApplyReturnToLobby()
    {
        GameStartData.Clear();
        gameStarting = false;
        foreach (ObjectHeadLobbyPlayer player in lobbyState.players) player.ready = false;
        if (IsHost && !UseDedicatedAuthority) IncrementLobbyRevision();
        SceneManager.LoadScene(config.TitleSceneName);
    }

    public async Task SetReadyAsync(bool ready)
    {
        if (UseDedicatedAuthority) { await SendAsync(3,new ObjectHeadReadyRequest {ready=ready}); return; }
        EnsureInMatch();
        ObjectHeadLobbyPlayer local = lobbyState?.players?.FirstOrDefault(p => p.userId == LocalUserId);
        if (ready && (local == null || !ObjectHeadContent.Load().ValidSelection(local.characters, lobbyState.settings.maxPlayers)))
            throw new InvalidOperationException("selection_required");
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
        if (UseDedicatedAuthority) { settings=settings.Copy();settings.rulesetVersion=AuthorityRuleset;await SendAsync(4,new ObjectHeadSettingsRequest{settings=settings});return; }
        EnsureInMatch();
        ObjectHeadRoomSettings normalized = NormalizeSettings(settings);
        if (IsHost)
        {
            if (normalized.maxPlayers < lobbyState.players.Length)
                throw new InvalidOperationException("room_capacity_too_small");
            if (UseEpicOnlineServices) await eos.UpdateRoomAsync(normalized);
            lobbyState.settings = normalized;
            foreach (ObjectHeadLobbyPlayer player in lobbyState.players)
            {
                player.ready = false;
                if (!ObjectHeadContent.Load().ValidSelection(player.characters, normalized.maxPlayers))
                    player.characters = Array.Empty<ObjectHeadCharacterKind>();
            }
            IncrementLobbyRevision();
            NotifyLobbyChanged();
            await BroadcastLobbyStateAsync();
            return;
        }

        await SendAsync(ObjectHeadNetworkProtocol.SettingsRequest, new ObjectHeadSettingsRequest { settings = normalized });
    }

    public async Task StartGameAsync()
    {
        if (UseDedicatedAuthority) { await SendAsync(5,new ObjectHeadReadyRequest()); return; }
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
        if (UseEpicOnlineServices) await eos.SetRoomOpenAsync(false);
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
        if (players.Length < lobbyState.settings.minPlayers || players.Length > lobbyState.settings.maxPlayers)
        {
            reason = $"Need at least {lobbyState.settings.minPlayers} players.";
            return false;
        }

        if (players.Any(player => !player.ready))
        {
            reason = "Every player must be ready.";
            return false;
        }

        if (players.Any(player => !ObjectHeadContent.Load().ValidSelection(player.characters, players.Length)))
        {
            reason = "selection_required";
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
            SessionInterrupted?.Invoke("connection_lost");
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
            List<IUserPresence> participants = GetMatchedPresences(matched);
            currentMatch = await socket.JoinMatchAsync(matched);
            if(UseDedicatedAuthority){lobbyState=null;await SendAsync(1,new ObjectHeadPlayerHello{username=displayName});return;}
            roomCode = string.Empty;
            if (participants.Count == 0)
            {
                participants = GetAllPresences();
            }

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
                    roomCode = string.Empty,
                    hostUserId = electedHost,
                    settings = BuildQuickMatchSettings(),
                    players = participants
                        .OrderBy(presence => presence.UserId, StringComparer.Ordinal)
                        .Select((presence, index) =>
                        {
                            ObjectHeadLobbyPlayer player = CreateLobbyPlayer(presence);
                            player.playerIndex = index + 1;
                            return player;
                        })
                        .ToArray()
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

    private static List<IUserPresence> GetMatchedPresences(IMatchmakerMatched matched)
    {
        List<IUserPresence> result = new List<IUserPresence>();
        if (matched == null)
        {
            return result;
        }

        void AddPresence(IUserPresence presence)
        {
            if (presence != null &&
                !string.IsNullOrEmpty(presence.UserId) &&
                result.All(existing => existing.UserId != presence.UserId))
            {
                result.Add(presence);
            }
        }

        AddPresence(matched.Self?.Presence);
        foreach (IMatchmakerUser user in matched.Users ?? Array.Empty<IMatchmakerUser>())
        {
            AddPresence(user?.Presence);
        }

        return result;
    }

    private ObjectHeadRoomSettings BuildQuickMatchSettings()
    {
        ObjectHeadRoomSettings settings = config.DefaultRoomSettings;
        settings.mode = pendingMatchMode;
        settings.minPlayers = pendingMatchPlayerCount;
        settings.maxPlayers = pendingMatchPlayerCount;
        return settings;
    }

    private async void HandlePresenceChanged(IMatchPresenceEvent presenceEvent)
    {
        if (UseDedicatedAuthority) return; // Only the server assigns seats and handles departures.
        if (currentMatch == null || presenceEvent.MatchId != currentMatch.Id)
        {
            return;
        }

        currentMatch.UpdatePresences(presenceEvent);
        if (presenceEvent.Leaves.Any(p => p.UserId == HostUserId) ||
            (GameStartData.Instance != null && presenceEvent.Leaves.Any()))
        {
            SessionInterrupted?.Invoke("player_disconnected");
        }
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
        if (UseDedicatedAuthority) { HandleAuthorityMatchState(matchState); return; }
        if (currentMatch == null || matchState.MatchId != currentMatch.Id)
        {
            return;
        }

        string json = Encoding.UTF8.GetString(matchState.State);
        string senderUserId = matchState.UserPresence != null ? matchState.UserPresence.UserId : string.Empty;
        await HandleIncomingMessageAsync(matchState.OpCode, senderUserId, json);
    }

    private async void HandleEosMessage(long opCode, string senderUserId, string json)
    {
        if (eos == null || !eos.InRoom) return;
        await HandleIncomingMessageAsync(opCode, senderUserId, json);
    }

    private async void HandleEosMemberLeft(string userId)
    {
        if (lobbyState == null ||
            !(lobbyState.players ?? Array.Empty<ObjectHeadLobbyPlayer>()).Any(player => player.userId == userId)) return;
        if (GameStartData.Instance != null) SessionInterrupted?.Invoke("player_disconnected");
        if (!IsHost) return;
        RemovePlayer(userId);
        IncrementLobbyRevision();
        NotifyLobbyChanged();
        await BroadcastLobbyStateAsync();
    }

    private async void HandleEosDisconnected(string reason)
    {
        lobbyState = null;
        roomCode = string.Empty;
        NotifyLobbyChanged();
        SessionInterrupted?.Invoke(reason);
        try { if (eos != null && eos.InRoom) await eos.LeaveRoomAsync(); }
        catch (Exception exception) { Log("EOS room cleanup failed: " + exception.Message); }
    }

    private async Task HandleIncomingMessageAsync(long opCode, string senderUserId, string json)
    {

        try
        {
            switch (opCode)
            {
                case ObjectHeadNetworkProtocol.PlayerHello:
                    if (IsHost)
                    {
                        ObjectHeadPlayerHello hello = JsonUtility.FromJson<ObjectHeadPlayerHello>(json);
                        if (hello == null || hello.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion) return;
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

                    if (receivedLobby.hostUserId != senderUserId || receivedLobby.matchId != MatchId ||
                        (lobbyState != null && receivedLobby.revision < lobbyState.revision)) return;

                    lobbyState = receivedLobby;
                    roomCode = receivedLobby.roomCode;
                    SetStatus("Lobby synchronized.");
                    NotifyLobbyChanged();
                    break;

                case ObjectHeadNetworkProtocol.ReadyRequest:
                    if (IsHost)
                    {
                        ObjectHeadReadyRequest readyRequest = JsonUtility.FromJson<ObjectHeadReadyRequest>(json);
                        ObjectHeadLobbyPlayer readyPlayer = lobbyState.players.FirstOrDefault(p => p.userId == senderUserId);
                        if (readyRequest == null || readyRequest.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion ||
                            readyPlayer == null || (readyRequest.ready && !ObjectHeadContent.Load().ValidSelection(readyPlayer.characters, lobbyState.settings.maxPlayers))) return;
                        UpsertPlayer(senderUserId, FindPresenceUsername(senderUserId), readyRequest.ready);
                        IncrementLobbyRevision();
                        NotifyLobbyChanged();
                        await BroadcastLobbyStateAsync();
                    }
                    break;

                case ObjectHeadNetworkProtocol.SelectionRequest:
                    if (IsHost && GameStartData.Instance == null)
                    {
                        ObjectHeadSelectionRequest selection = JsonUtility.FromJson<ObjectHeadSelectionRequest>(json);
                        if (selection == null || selection.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion) return;
                        ApplySelection(senderUserId, selection.characters);
                        await BroadcastLobbyStateAsync();
                    }
                    break;

                case ObjectHeadNetworkProtocol.ReturnToLobby:
                    if (senderUserId == HostUserId) ApplyReturnToLobby();
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
                    if (opCode >= ObjectHeadNetworkProtocol.GameplayCommand)
                    {
                        GameplayMessageReceived?.Invoke(opCode, senderUserId, json);
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
        if (UseEpicOnlineServices) await eos.SendAsync(opCode, json);
        else await socket.SendMatchStateAsync(currentMatch.Id, opCode, json);
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
            matchId = MatchId,
            rulesetVersion = settings.rulesetVersion,
            mode = settings.mode,
            playerCount = orderedPlayers.Length,
            mapSelectionMode = settings.mapSelectionMode,
            mapId = mapId,
            mapSeed = seed,
            characterSpawnSeed = seed,
            startingPlayerIndex = seed % orderedPlayers.Length + 1,
            startedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            players = orderedPlayers.Select(player => new ObjectHeadPlayerAssignment
            {
                userId = player.userId,
                username = player.username,
                playerIndex = player.playerIndex,
                allianceId = ObjectHeadContent.Load().Mode(settings.mode).Alliance(player.playerIndex),
                characters = (ObjectHeadCharacterKind[])player.characters.Clone()
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
        string targetScene = config.ResolveSceneName(GameStartData.Instance != null ? GameStartData.Instance.mapId : null);
        SceneManager.LoadScene(string.IsNullOrWhiteSpace(targetScene) ? activeScene.name : targetScene);
        gameStarting = false;
    }

    private async Task LeaveCurrentMatchAsync()
    {
        if (UseEpicOnlineServices)
        {
            if (eos != null) await eos.LeaveRoomAsync();
            lobbyState = null;
            roomCode = string.Empty;
            GameStartData.Clear();
            NotifyLobbyChanged();
            return;
        }
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
            roomCode = string.Empty;
            NotifyLobbyChanged();
        }
    }

    private async Task<string> RegisterRoomCodeAsync(string matchId)
    {
        try
        {
            IApiRpc response = await client.RpcAsync(
                session,
                "objecthead_register_room_code",
                JsonUtility.ToJson(new ObjectHeadRoomCodeRequest { match_id = matchId }));
            ObjectHeadRoomCodeResponse data = JsonUtility.FromJson<ObjectHeadRoomCodeResponse>(response.Payload);
            if (data != null && !string.IsNullOrWhiteSpace(data.room_code))
            {
                return data.room_code.Trim().ToUpperInvariant();
            }
        }
        catch (Exception exception)
        {
            Log("Room-code registration unavailable; using the internal match ID. " + exception.Message);
        }

        return matchId;
    }

    private async Task<string> ResolveMatchIdAsync(string roomCodeOrMatchId)
    {
        if (!LooksLikeFriendlyRoomCode(roomCodeOrMatchId))
        {
            return roomCodeOrMatchId;
        }

        IApiRpc response = await client.RpcAsync(
            session,
            "objecthead_resolve_room_code",
            JsonUtility.ToJson(new ObjectHeadRoomCodeResponse
            {
                room_code = roomCodeOrMatchId.Trim().ToUpperInvariant()
            }));
        ObjectHeadRoomCodeResponse data = JsonUtility.FromJson<ObjectHeadRoomCodeResponse>(response.Payload);
        if (data == null || string.IsNullOrWhiteSpace(data.match_id))
        {
            throw new InvalidOperationException("The room code could not be resolved.");
        }

        return data.match_id.Trim();
    }

    private static bool LooksLikeFriendlyRoomCode(string value)
    {
        string trimmed = value != null ? value.Trim() : string.Empty;
        return trimmed.Length == 6 && trimmed.All(character =>
            char.IsLetterOrDigit(character) && character <= 127);
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
            if (GameStartData.Instance != null || players.Count >= lobbyState.settings.maxPlayers) return;
            foreach (var player in players) player.ready = false;
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
        foreach (var player in lobbyState.players) player.ready = false;

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
        if (UseEpicOnlineServices)
            return lobbyState?.players?.FirstOrDefault(player => player.userId == userId)?.username ?? "Player";
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
        var definition = ObjectHeadContent.Load().Mode(result.mode) ?? throw new ArgumentException("unsupported_mode");
        result.minPlayers = result.maxPlayers = definition.players;
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
            throw new InvalidOperationException(UseEpicOnlineServices ? "Connect to Epic Online Services first." : "Connect to Nakama first.");
        }
    }

    private void EnsureInMatch()
    {
        EnsureConnected();
        if (!IsInMatch)
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

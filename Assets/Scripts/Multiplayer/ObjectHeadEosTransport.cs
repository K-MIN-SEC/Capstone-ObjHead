using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;
using UnityEngine;
using EosAttribute = Epic.OnlineServices.Lobby.Attribute;

// EOS supplies identity, discovery and NAT/relay transport. Game rules stay on the room host.
// Never treat lobby attributes or another peer's claimed role as combat authority.
public sealed class ObjectHeadEosTransport : MonoBehaviour
{
    private const string SocketName = "OBJHEAD";
    private const string Bucket = "objecthead-v1";
    private const int FragmentBytes = 960;
    private const int HeaderBytes = 16;
    private const int MaxMessageBytes = 512 * 1024;
    // A full-size terrain snapshot may need more than 512 packets at 960 bytes each.
    private const int MaxFragments = (MaxMessageBytes + FragmentBytes - 1) / FragmentBytes;

    private readonly HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> admitted = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, Reassembly> partialMessages = new Dictionary<string, Reassembly>();
    private readonly Dictionary<string, Queue<float>> passwordAttempts = new Dictionary<string, Queue<float>>();
    private LobbyInterface lobbies;
    private P2PInterface p2p;
    private ProductUserId localUser;
    private ulong memberNotification;
    private ulong connectionNotification;
    private int nextMessageId;
    private string lobbyId;
    private string hostId;
    private string password;
    private bool privateRoom;
    private bool acceptingMembers;
    private TaskCompletionSource<string> admissionReply;
    private readonly Dictionary<string, float> pendingConnections = new Dictionary<string, float>();
    private float nextConnectionRetry;
    private bool closeListenerAdded;

    public event Action<long, string, string> MessageReceived;
    public event Action<string> MemberLeft;
    public event Action<string> Disconnected;

    public bool Connected => localUser != null && localUser.IsValid() && lobbies != null && p2p != null;
    public bool InRoom => !string.IsNullOrEmpty(lobbyId);
    public bool IsHost => InRoom && LocalUserId == hostId;
    public string LocalUserId => localUser != null && localUser.IsValid() ? localUser.ToString() : string.Empty;
    public string LobbyId => lobbyId ?? string.Empty;
    public string HostUserId => hostId ?? string.Empty;
    // Includes peers which joined EOS but have not sent their game-level hello yet.
    // AI seats are local host actors and never appear in this count.
    public int LiveMemberCount => RefreshMemberCount();

    private sealed class Reassembly
    {
        public readonly byte[][] parts;
        public readonly int expectedBytes;
        public int received;
        public int totalBytes;
        public float createdAt;
        public Reassembly(int count, int bytes)
        {
            parts = new byte[count][];
            expectedBytes = bytes;
            createdAt = Time.realtimeSinceStartup;
        }
    }

    public sealed class Room
    {
        public string lobbyId;
        public string code;
        public string hostId;
        public bool isPrivate;
        public int players;
        public int capacity;
        public ObjectHeadMatchMode mode;
        public string mapId;
        public int protocolVersion;
    }

    public async Task ConnectAsync(string displayName)
    {
        if (Connected) return;
        ProductConfig product = Config.Get<ProductConfig>();
        if (product == null || product.ProductId == Guid.Empty)
            throw new InvalidOperationException("eos_setup_required");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // This Device-ID/P2P game has no social/store overlay. Set the flag before
        // platform creation as well as in the local config (which contains secrets and is not versioned).
        PlatformManager.GetPlatformConfig().platformOptionsFlags |= WrappedPlatformFlags.DisableOverlay;
#endif
        if (FindAnyObjectByType<EOSManager>() == null)
            new GameObject("EpicOnlineServices").AddComponent<EOSManager>();
        var eos = EOSManager.Instance;
        if (!closeListenerAdded)
        {
            // The SDK calls this BEFORE releasing native interfaces. OnDestroy can run afterwards.
            var weak = new WeakReference<ObjectHeadEosTransport>(this);
            eos.AddApplicationCloseListener(() => { if (weak.TryGetTarget(out var owner)) owner.ReleaseNotifications(); });
            closeListenerAdded = true;
        }
        if (eos.GetEOSPlatformInterface() == null)
            throw new InvalidOperationException("EOS 초기화에 실패했습니다. Epic 배포 설정을 확인해 주세요.");

        var connect = eos.GetEOSConnectInterface();
        var deviceReady = new TaskCompletionSource<Result>();
        var deviceOptions = new CreateDeviceIdOptions { DeviceModel = SystemInfo.deviceModel };
        connect.CreateDeviceId(ref deviceOptions, null, (ref CreateDeviceIdCallbackInfo data) => deviceReady.TrySetResult(data.ResultCode));
        Result deviceResult = await WaitFor(deviceReady.Task, "기기 인증");
        if (deviceResult != Result.Success && deviceResult != Result.DuplicateNotAllowed)
            throw new InvalidOperationException("EOS 기기 인증 실패: " + deviceResult);

        var login = new TaskCompletionSource<LoginCallbackInfo>();
        eos.StartConnectLoginWithDeviceToken(displayName, data => login.TrySetResult(data));
        LoginCallbackInfo loginResult = await WaitFor(login.Task, "EOS 로그인");
        if (loginResult.ResultCode == Result.InvalidUser)
        {
            var created = new TaskCompletionSource<CreateUserCallbackInfo>();
            eos.CreateConnectUserWithContinuanceToken(loginResult.ContinuanceToken, data => created.TrySetResult(data));
            CreateUserCallbackInfo createResult = await WaitFor(created.Task, "EOS 사용자 생성");
            if (createResult.ResultCode != Result.Success)
                throw new InvalidOperationException("EOS 계정 생성 실패: " + createResult.ResultCode);
            localUser = createResult.LocalUserId;
        }
        else if (loginResult.ResultCode == Result.Success) localUser = loginResult.LocalUserId;
        else throw new InvalidOperationException("EOS 로그인 실패: " + loginResult.ResultCode);

        if (localUser == null || !localUser.IsValid())
            throw new InvalidOperationException("EOS Product User ID를 얻지 못했습니다.");
        lobbies = eos.GetEOSLobbyInterface();
        p2p = eos.GetEOSPlatformInterface().GetP2PInterface();
        var memberOptions = new AddNotifyLobbyMemberStatusReceivedOptions();
        memberNotification = lobbies.AddNotifyLobbyMemberStatusReceived(ref memberOptions, null, OnMemberStatus);
        var connectionOptions = new AddNotifyPeerConnectionRequestOptions
        {
            LocalUserId = localUser,
            SocketId = new SocketId { SocketName = SocketName }
        };
        connectionNotification = p2p.AddNotifyPeerConnectionRequest(ref connectionOptions, null, OnConnectionRequest);
    }

    public async Task<Room> CreateRoomAsync(ObjectHeadRoomSettings settings, bool isPrivate, string requestedPassword)
    {
        RequireConnected();
        await LeaveRoomAsync();
        if (isPrivate && (string.IsNullOrEmpty(requestedPassword) || requestedPassword.Length < 4 ||
                          requestedPassword.Length > 32 || requestedPassword.Any(character => character <= 32 || character > 126)))
            throw new InvalidOperationException("room_password_invalid");
        string code = NewRoomCode();
        var finished = new TaskCompletionSource<CreateLobbyCallbackInfo>();
        var options = new CreateLobbyOptions
        {
            LocalUserId = localUser,
            MaxLobbyMembers = (uint)settings.maxPlayers,
            PermissionLevel = LobbyPermissionLevel.Publicadvertised,
            BucketId = Bucket,
            DisableHostMigration = true,
            AllowInvites = true,
            PresenceEnabled = false,
            EnableRTCRoom = false
        };
        lobbies.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo data) => finished.TrySetResult(data));
        CreateLobbyCallbackInfo result = await WaitFor(finished.Task, "방 만들기");
        Check(result.ResultCode, "방 만들기");
        lobbyId = result.LobbyId;
        hostId = LocalUserId;
        password = isPrivate ? requestedPassword : null;
        privateRoom = isPrivate;
        acceptingMembers = true;
        members.Add(LocalUserId);
        admitted.Add(LocalUserId);
        try
        {
            await SetAttributesAsync(new Dictionary<string, string>
            {
                ["game"] = Bucket,
                ["rules"] = ObjectHeadRoomSettings.CurrentRulesetVersion,
                ["protocol"] = ObjectHeadNetworkProtocol.ProtocolVersion.ToString(),
                ["code"] = code,
                ["private"] = isPrivate ? "1" : "0",
                ["reserved_ai"] = "0",
                ["mode"] = settings.mode.ToString(),
                ["map"] = settings.fixedMapId ?? string.Empty,
                ["open"] = "1"
            });
        }
        catch
        {
            await LeaveRoomAsync();
            throw;
        }
        return new Room { lobbyId = lobbyId, code = code, hostId = hostId, isPrivate = isPrivate,
            players = 1, capacity = settings.maxPlayers, mode = settings.mode, mapId = settings.fixedMapId };
    }

    public async Task<Room> JoinRoomAsync(string codeOrId, string suppliedPassword)
    {
        RequireConnected();
        await LeaveRoomAsync();
        bool isId = codeOrId.Length > 12;
        List<Room> found = await SearchAsync(isId ? null : "code", isId ? null : codeOrId.ToUpperInvariant(), isId ? codeOrId : null);
        Room room = found.FirstOrDefault();
        if (room == null) throw new InvalidOperationException("room_not_found");
        if (room.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion) throw new InvalidOperationException("incompatible_ruleset");
        if (room.players >= room.capacity) throw new InvalidOperationException("room_full");
        if (room.isPrivate && string.IsNullOrEmpty(suppliedPassword))
            throw new InvalidOperationException("room_password_required");

        LobbyDetails details = await FindDetailsAsync(room.lobbyId);
        try
        {
            var finished = new TaskCompletionSource<Result>();
            var options = new JoinLobbyOptions { LobbyDetailsHandle = details, LocalUserId = localUser, PresenceEnabled = false };
            lobbies.JoinLobby(ref options, null, (ref JoinLobbyCallbackInfo data) => finished.TrySetResult(data.ResultCode));
            Check(await WaitFor(finished.Task, "방 참가"), "방 참가");
        }
        finally { details.Release(); }
        lobbyId = room.lobbyId;
        hostId = room.hostId;
        privateRoom = room.isPrivate;
        members.Add(LocalUserId);
        members.Add(hostId);
        QueuePeerAcceptance(hostId);
        // An EOS lobby membership is not proof of a working host P2P connection.
        // Public rooms use the same explicit, bounded admission handshake as private rooms.
        admissionReply = new TaskCompletionSource<string>();
        var reply = admissionReply;
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!reply.Task.IsCompleted && InRoom && DateTime.UtcNow < deadline)
        {
            await SendRawAsync(hostId, -1, room.isPrivate ? suppliedPassword : string.Empty);
            await Task.WhenAny(reply.Task, Task.Delay(1000));
        }
        string answer = reply.Task.IsCompleted ? await reply.Task : "timeout";
        admissionReply = null;
        if (answer != "ok")
        {
            await LeaveRoomAsync();
            throw new InvalidOperationException(answer == "timeout" || answer == "left" ? "connection_help" :
                answer == "closed" ? "match_in_progress" :
                answer == "rate_limited" ? "room_password_rate_limited" : "room_password_incorrect");
        }
        admitted.Add(hostId);
        return room;
    }

    public async Task<List<Room>> FindRoomsAsync() => (await SearchAsync("game", Bucket, null))
        .Where(room => !room.isPrivate && room.players < room.capacity).ToList();

    public Task UpdateRoomAsync(ObjectHeadRoomSettings settings, int reservedAiSeats = 0)
    {
        if (!IsHost) throw new InvalidOperationException("방장만 방 설정을 변경할 수 있습니다.");
        if (settings == null) throw new InvalidOperationException("room_capacity_too_small");
        int humanCapacity = HumanCapacityFor(settings.maxPlayers, reservedAiSeats, RefreshMemberCount());
        return SetAttributesAsync(new Dictionary<string, string>
        {
            ["mode"] = settings.mode.ToString(),
            ["map"] = settings.fixedMapId ?? string.Empty,
            ["reserved_ai"] = reservedAiSeats.ToString()
        }, humanCapacity);
    }

    public async Task SetRoomOpenAsync(bool open)
    {
        if (!IsHost) throw new InvalidOperationException("방장만 방을 열거나 닫을 수 있습니다.");
        bool previous = acceptingMembers;
        acceptingMembers = open;
        try { await SetAttributesAsync(new Dictionary<string, string> { ["open"] = open ? "1" : "0" }); }
        catch { acceptingMembers = previous; throw; }
    }

    public async Task SendAsync(long opCode, string json)
    {
        if (!InRoom) throw new InvalidOperationException("방에 참가하지 않았습니다.");
        if (IsHost)
            await BroadcastToPeersAsync(admitted.Where(peer => peer != LocalUserId).ToArray(),
                peer => SendRawAsync(peer, opCode, json));
        else await SendRawAsync(hostId, opCode, json);
    }

    private static async Task BroadcastToPeersAsync(IEnumerable<string> peers, Func<string, Task> send)
    {
        List<Exception> failures = null;
        foreach (string peer in peers)
        {
            try { await send(peer); }
            catch (Exception exception)
            {
                // A stale/failed second peer must not prevent the third or fourth
                // peer from receiving their initial lobby state or battle updates.
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("EOS 일부 참가자에게 전송하지 못했습니다.", failures);
    }

    private static int HumanCapacityFor(int totalCapacity, int reservedAiSeats, int liveMembers)
    {
        if (totalCapacity < 1 || reservedAiSeats < 0 || reservedAiSeats >= totalCapacity ||
            liveMembers > totalCapacity - reservedAiSeats)
            throw new InvalidOperationException("room_capacity_too_small");
        return totalCapacity - reservedAiSeats;
    }

    public Task SendToPeerAsync(string peerId, long opCode, string json)
    {
        if (!IsHost || !admitted.Contains(peerId) || !IsMemberInLobby(peerId))
            throw new InvalidOperationException("승인된 방 참가자에게만 전송할 수 있습니다.");
        return SendRawAsync(peerId, opCode, json);
    }

    public async Task LeaveRoomAsync()
    {
        if (!InRoom) return;
        string leaving = lobbyId;
        bool owner = IsHost;
        ClosePeerConnections();
        lobbyId = null;
        hostId = null;
        password = null;
        privateRoom = false;
        acceptingMembers = false;
        members.Clear();
        admitted.Clear();
        passwordAttempts.Clear();
        partialMessages.Clear();
        pendingConnections.Clear();
        admissionReply?.TrySetResult("left");
        admissionReply = null;
        var finished = new TaskCompletionSource<Result>();
        if (owner)
        {
            var options = new DestroyLobbyOptions { LobbyId = leaving, LocalUserId = localUser };
            lobbies.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo data) => finished.TrySetResult(data.ResultCode));
        }
        else
        {
            var options = new LeaveLobbyOptions { LobbyId = leaving, LocalUserId = localUser };
            lobbies.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo data) => finished.TrySetResult(data.ResultCode));
        }
        Result result = await WaitFor(finished.Task, "방 나가기");
        if (result != Result.Success && result != Result.NotFound) Debug.LogWarning("[EOS] Leave lobby: " + result);
    }

    public async Task DisconnectAsync()
    {
        try { await LeaveRoomAsync(); }
        finally { ReleaseNotifications(); }
    }

    private void ClosePeerConnections()
    {
        if (p2p == null || localUser == null) return;
        var close = new CloseConnectionsOptions { LocalUserId = localUser, SocketId = new SocketId { SocketName = SocketName } };
        p2p.CloseConnections(ref close);
    }

    private void ReleaseNotifications()
    {
        ClosePeerConnections();
        if (lobbies != null && memberNotification != 0) lobbies.RemoveNotifyLobbyMemberStatusReceived(memberNotification);
        if (p2p != null && connectionNotification != 0) p2p.RemoveNotifyPeerConnectionRequest(connectionNotification);
        memberNotification = 0;
        connectionNotification = 0;
        lobbies = null;
        p2p = null;
        localUser = null;
        lobbyId = hostId = password = null;
        acceptingMembers = false;
        members.Clear();
        admitted.Clear();
        passwordAttempts.Clear();
        partialMessages.Clear();
        pendingConnections.Clear();
        admissionReply?.TrySetResult("left");
    }

    private async Task SetAttributesAsync(Dictionary<string, string> values, int maxMembers = 0)
    {
        var options = new UpdateLobbyModificationOptions { LobbyId = lobbyId, LocalUserId = localUser };
        Check(lobbies.UpdateLobbyModification(ref options, out LobbyModification modification), "방 정보 수정");
        try
        {
            if (maxMembers > 0)
            {
                var capacity = new LobbyModificationSetMaxMembersOptions { MaxMembers = (uint)maxMembers };
                Check(modification.SetMaxMembers(ref capacity), "방 정원 변경");
            }
            foreach (var value in values)
            {
                var data = new AttributeData { Key = value.Key, Value = value.Value };
                var add = new LobbyModificationAddAttributeOptions
                {
                    Attribute = data,
                    Visibility = LobbyAttributeVisibility.Public
                };
                Check(modification.AddAttribute(ref add), "방 속성 저장");
            }
            var updated = new TaskCompletionSource<Result>();
            var update = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobbies.UpdateLobby(ref update, null, (ref UpdateLobbyCallbackInfo data) => updated.TrySetResult(data.ResultCode));
            Check(await WaitFor(updated.Task, "방 정보 게시"), "방 정보 게시");
        }
        finally { modification.Release(); }
    }

    private async Task<List<Room>> SearchAsync(string key, string value, string exactLobbyId)
    {
        var create = new CreateLobbySearchOptions { MaxResults = 100 };
        Check(lobbies.CreateLobbySearch(ref create, out LobbySearch search), "방 검색 시작");
        try
        {
            if (!string.IsNullOrEmpty(exactLobbyId))
            {
                var byId = new LobbySearchSetLobbyIdOptions { LobbyId = exactLobbyId };
                Check(search.SetLobbyId(ref byId), "방 ID 검색");
            }
            else if (!string.IsNullOrEmpty(key))
            {
                var parameter = new LobbySearchSetParameterOptions
                {
                    Parameter = new AttributeData { Key = key, Value = value },
                    ComparisonOp = ComparisonOp.Equal
                };
                Check(search.SetParameter(ref parameter), "방 조건 검색");
            }
            var finished = new TaskCompletionSource<Result>();
            var find = new LobbySearchFindOptions { LocalUserId = localUser };
            search.Find(ref find, null, (ref LobbySearchFindCallbackInfo data) => finished.TrySetResult(data.ResultCode));
            Result searchResult = await WaitFor(finished.Task, "방 검색");
            if (searchResult == Result.NotFound) return new List<Room>();
            Check(searchResult, "방 검색");
            var countOptions = new LobbySearchGetSearchResultCountOptions();
            uint count = search.GetSearchResultCount(ref countOptions);
            var result = new List<Room>();
            for (uint index = 0; index < count; index++)
            {
                var copy = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = index };
                if (search.CopySearchResultByIndex(ref copy, out LobbyDetails details) != Result.Success) continue;
                try
                {
                    Room room = ReadRoom(details);
                    if (room != null && GetAttribute(details, "game") == Bucket &&
                        GetAttribute(details, "rules") == ObjectHeadRoomSettings.CurrentRulesetVersion &&
                        GetAttribute(details, "open") == "1") result.Add(room);
                }
                finally { details.Release(); }
            }
            return result;
        }
        finally { search.Release(); }
    }

    private async Task<LobbyDetails> FindDetailsAsync(string id)
    {
        var create = new CreateLobbySearchOptions { MaxResults = 1 };
        Check(lobbies.CreateLobbySearch(ref create, out LobbySearch search), "방 상세 조회");
        try
        {
            var byId = new LobbySearchSetLobbyIdOptions { LobbyId = id };
            Check(search.SetLobbyId(ref byId), "방 ID 조회");
            var finished = new TaskCompletionSource<Result>();
            var find = new LobbySearchFindOptions { LocalUserId = localUser };
            search.Find(ref find, null, (ref LobbySearchFindCallbackInfo data) => finished.TrySetResult(data.ResultCode));
            Check(await WaitFor(finished.Task, "방 상세 검색"), "방 상세 검색");
            var copy = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
            Check(search.CopySearchResultByIndex(ref copy, out LobbyDetails details), "방 상세 복사");
            return details;
        }
        finally { search.Release(); }
    }

    private static Room ReadRoom(LobbyDetails details)
    {
        var copy = new LobbyDetailsCopyInfoOptions();
        if (details.CopyInfo(ref copy, out LobbyDetailsInfo? info) != Result.Success || !info.HasValue) return null;
        var raw = info.Value;
        Enum.TryParse(GetAttribute(details, "mode"), out ObjectHeadMatchMode mode);
        int.TryParse(GetAttribute(details, "protocol"), out int protocol);
        int.TryParse(GetAttribute(details, "reserved_ai"), out int reservedAiSeats);
        reservedAiSeats = Math.Max(0, Math.Min(3, reservedAiSeats));
        return new Room
        {
            lobbyId = raw.LobbyId,
            code = GetAttribute(details, "code"),
            hostId = raw.LobbyOwnerUserId.ToString(),
            isPrivate = GetAttribute(details, "private") == "1",
            players = (int)(raw.MaxMembers - raw.AvailableSlots) + reservedAiSeats,
            capacity = (int)raw.MaxMembers + reservedAiSeats,
            mode = mode,
            protocolVersion = protocol,
            mapId = GetAttribute(details, "map")
        };
    }

    private static string GetAttribute(LobbyDetails details, string key)
    {
        var copy = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
        if (details.CopyAttributeByKey(ref copy, out EosAttribute? attribute) != Result.Success || !attribute.HasValue)
            return string.Empty;
        return attribute.Value.Data?.Value.AsUtf8 ?? string.Empty;
    }

    private async Task SendRawAsync(string peerId, long opCode, string json)
    {
        ProductUserId peer = ProductUserId.FromString(peerId);
        if (peer == null || !peer.IsValid()) throw new InvalidOperationException("잘못된 EOS 사용자 ID입니다.");
        byte[] bytes = Encoding.UTF8.GetBytes(lobbyId + "\n" + opCode + "|" + (json ?? string.Empty));
        if (bytes.Length > MaxMessageBytes) throw new InvalidOperationException("EOS 메시지가 너무 큽니다.");
        int count = Math.Max(1, (bytes.Length + FragmentBytes - 1) / FragmentBytes);
        int id = ++nextMessageId;
        for (int index = 0; index < count; index++)
        {
            int length = Math.Min(FragmentBytes, bytes.Length - index * FragmentBytes);
            byte[] packet = new byte[HeaderBytes + length];
            packet[0] = (byte)'O'; packet[1] = (byte)'H'; packet[2] = (byte)'B'; packet[3] = (byte)'1';
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, packet, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)index), 0, packet, 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)count), 0, packet, 10, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, packet, 12, 4);
            Buffer.BlockCopy(bytes, index * FragmentBytes, packet, HeaderBytes, length);
            var send = new SendPacketOptions
            {
                LocalUserId = localUser,
                RemoteUserId = peer,
                SocketId = new SocketId { SocketName = SocketName },
                Channel = 0,
                Reliability = PacketReliability.ReliableOrdered,
                AllowDelayedDelivery = true,
                Data = new ArraySegment<byte>(packet)
            };
            Check(p2p.SendPacket(ref send), "EOS 패킷 전송");
        }
        await Task.CompletedTask;
    }

    private void Update()
    {
        if (Connected && InRoom && Time.realtimeSinceStartup >= nextConnectionRetry)
        {
            nextConnectionRetry = Time.realtimeSinceStartup + .25f;
            foreach (var peer in pendingConnections.ToArray())
                if (Time.realtimeSinceStartup - peer.Value > 15f) pendingConnections.Remove(peer.Key);
                else if (IsMemberInLobby(peer.Key) && AcceptPeer(ProductUserId.FromString(peer.Key))) pendingConnections.Remove(peer.Key);
        }
        if (!Connected || !InRoom) return;
        var sizeOptions = new GetNextReceivedPacketSizeOptions { LocalUserId = localUser, RequestedChannel = 0 };
        for (int handled = 0; handled < 128; handled++)
        {
            if (p2p.GetNextReceivedPacketSize(ref sizeOptions, out uint nextSize) != Result.Success || nextSize == 0) break;
            if (nextSize > P2PInterface.MAX_PACKET_SIZE) break;
            byte[] packet = new byte[nextSize];
            ProductUserId sender = null;
            SocketId socket = default;
            var receive = new ReceivePacketOptions { LocalUserId = localUser, MaxDataSizeBytes = nextSize, RequestedChannel = 0 };
            Result result = p2p.ReceivePacket(ref receive, ref sender, ref socket, out _, new ArraySegment<byte>(packet), out uint written);
            if (result != Result.Success) break;
            if (sender == null || socket.SocketName != SocketName || written < HeaderBytes || !IsMemberInLobby(sender.ToString())) continue;
            ReceiveFragment(sender.ToString(), packet, (int)written);
        }
        foreach (string key in partialMessages.Where(item => Time.realtimeSinceStartup - item.Value.createdAt > 10f)
                     .Select(item => item.Key).ToArray()) partialMessages.Remove(key);
    }

    private void ReceiveFragment(string sender, byte[] packet, int length)
    {
        if (packet[0] != 'O' || packet[1] != 'H' || packet[2] != 'B' || packet[3] != '1') return;
        int id = BitConverter.ToInt32(packet, 4);
        int index = BitConverter.ToUInt16(packet, 8);
        int count = BitConverter.ToUInt16(packet, 10);
        int bytes = BitConverter.ToInt32(packet, 12);
        if (count < 1 || count > MaxFragments || index >= count || bytes < 1 || bytes > MaxMessageBytes) return;
        if (count != (bytes + FragmentBytes - 1) / FragmentBytes) return;
        int partBytes = length - HeaderBytes;
        int expectedPartBytes = index == count - 1 ? bytes - FragmentBytes * (count - 1) : FragmentBytes;
        if (partBytes != expectedPartBytes) return;
        string key = sender + ":" + id;
        if (!partialMessages.TryGetValue(key, out Reassembly message))
        {
            if (partialMessages.Count >= 32) return;
            message = new Reassembly(count, bytes);
            partialMessages[key] = message;
        }
        if (message.parts.Length != count || message.expectedBytes != bytes) { partialMessages.Remove(key); return; }
        if (message.parts[index] != null) return;
        byte[] part = new byte[partBytes];
        Buffer.BlockCopy(packet, HeaderBytes, part, 0, partBytes);
        message.parts[index] = part;
        message.received++;
        message.totalBytes += partBytes;
        if (message.received != count) return;
        partialMessages.Remove(key);
        if (message.totalBytes != bytes) return;
        byte[] full = new byte[bytes];
        int offset = 0;
        foreach (byte[] segment in message.parts) { Buffer.BlockCopy(segment, 0, full, offset, segment.Length); offset += segment.Length; }
        string text = Encoding.UTF8.GetString(full);
        int roomDelimiter = text.IndexOf('\n');
        if (roomDelimiter < 1 || text.Substring(0, roomDelimiter) != lobbyId) return;
        text = text.Substring(roomDelimiter + 1);
        int delimiter = text.IndexOf('|');
        if (delimiter < 1 || !long.TryParse(text.Substring(0, delimiter), out long opCode)) return;
        string body = text.Substring(delimiter + 1);
        if (opCode == -1 && IsHost) { _ = AdmitSafelyAsync(sender, body); return; }
        if (opCode == -2 && !IsHost && sender == hostId) { admissionReply?.TrySetResult(body); return; }
        if (!admitted.Contains(sender)) return;
        if (!IsHost && sender != hostId) return;
        MessageReceived?.Invoke(opCode, sender, body);
    }

    private async Task AdmitSafelyAsync(string sender, string supplied)
    {
        try { await AdmitAsync(sender, supplied); }
        catch (Exception exception) { Debug.LogWarning("[EOS] Admission failed: " + exception.GetType().Name); }
    }

    private async Task AdmitAsync(string sender, string supplied)
    {
        if (admitted.Contains(sender)) { await SendRawAsync(sender, -2, "ok"); return; }
        string admissionRoom = lobbyId;
        if (!acceptingMembers)
        {
            await SendRawAsync(sender, -2, "closed");
            return;
        }
        if (!passwordAttempts.TryGetValue(sender, out Queue<float> attempts))
        {
            attempts = new Queue<float>();
            passwordAttempts[sender] = attempts;
        }
        while (attempts.Count > 0 && Time.realtimeSinceStartup - attempts.Peek() > 60f) attempts.Dequeue();
        bool rateLimited = attempts.Count >= 5;
        if (!rateLimited) attempts.Enqueue(Time.realtimeSinceStartup);
        bool valid = !rateLimited && IsMemberInLobby(sender) && (!privateRoom || supplied == password);
        if (valid) admitted.Add(sender);
        await SendRawAsync(sender, -2, valid ? "ok" : rateLimited ? "rate_limited" : "denied");
        if (!valid)
        {
            await Task.Delay(250); // Give EOS reliable delivery a tick before removing this peer.
            if (!InRoom || !IsHost || lobbyId != admissionRoom || admitted.Contains(sender)) return;
            var kick = new KickMemberOptions { LobbyId = lobbyId, LocalUserId = localUser,
                TargetUserId = ProductUserId.FromString(sender) };
            lobbies.KickMember(ref kick, null, (ref KickMemberCallbackInfo data) => { });
        }
    }

    private void OnMemberStatus(ref LobbyMemberStatusReceivedCallbackInfo data)
    {
        if (data.LobbyId != lobbyId) return;
        string userId = data.TargetUserId.ToString();
        if (data.CurrentStatus == LobbyMemberStatus.Joined)
        {
            members.Add(userId);
            // Proactively accept verified lobby peers. This also recovers a connection
            // request received before the EOS member callback/local join callback.
            if (userId != LocalUserId && (IsHost || userId == hostId)) QueuePeerAcceptance(userId);
            return;
        }
        if (data.CurrentStatus == LobbyMemberStatus.Promoted) return;
        members.Remove(userId);
        admitted.Remove(userId);
        pendingConnections.Remove(userId);
        passwordAttempts.Remove(userId);
        if (userId == LocalUserId || userId == hostId) Disconnected?.Invoke("player_disconnected");
        else MemberLeft?.Invoke(userId);
    }

    private void OnConnectionRequest(ref OnIncomingConnectionRequestInfo data)
    {
        if (!InRoom || data.SocketId?.SocketName != SocketName) return;
        string peer = data.RemoteUserId.ToString();
        if (!IsHost && peer != hostId) return;
        QueuePeerAcceptance(peer);
    }

    private void QueuePeerAcceptance(string peer)
    {
        if (IsMemberInLobby(peer) && AcceptPeer(ProductUserId.FromString(peer)))
        {
            pendingConnections.Remove(peer);
            return;
        }
        if (pendingConnections.Count < 16 && !pendingConnections.ContainsKey(peer))
            pendingConnections[peer] = Time.realtimeSinceStartup;
    }

    private bool AcceptPeer(ProductUserId peer)
    {
        if (p2p == null || localUser == null || peer == null || !peer.IsValid()) return false;
        var accept = new AcceptConnectionOptions { LocalUserId = localUser, RemoteUserId = peer,
            SocketId = new SocketId { SocketName = SocketName } };
        Result result = p2p.AcceptConnection(ref accept);
        if (result != Result.Success) Debug.LogWarning("[EOS] P2P accept: " + result);
        return result == Result.Success;
    }

    private static string NewRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        byte[] random = new byte[6];
        using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
        return new string(random.Select(value => alphabet[value % alphabet.Length]).ToArray());
    }

    private void RequireConnected()
    {
        if (!Connected) throw new InvalidOperationException("EOS 연결이 필요합니다.");
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success) throw new InvalidOperationException(operation + " 실패: " + result);
    }

    private bool IsMemberInLobby(string userId)
    {
        if (members.Contains(userId)) return true;
        RefreshMemberCount();
        return members.Contains(userId);
    }

    public int RefreshMemberCount()
    {
        if (!InRoom || lobbies == null || localUser == null) return 0;
        var copy = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = localUser };
        if (lobbies.CopyLobbyDetailsHandle(ref copy, out LobbyDetails details) != Result.Success) return members.Count;
        try
        {
            var countOptions = new LobbyDetailsGetMemberCountOptions();
            uint count = details.GetMemberCount(ref countOptions);
            for (uint index = 0; index < count; index++)
            {
                var memberOptions = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = index };
                ProductUserId member = details.GetMemberByIndex(ref memberOptions);
                if (member != null && member.IsValid()) members.Add(member.ToString());
            }
            return Math.Max((int)count, members.Count);
        }
        finally { details.Release(); }
    }

    private static async Task<T> WaitFor<T>(Task<T> operation, string label)
    {
        if (await Task.WhenAny(operation, Task.Delay(15000)) != operation)
            throw new TimeoutException(label + " 응답 시간이 초과됐습니다.");
        return await operation;
    }

    private void OnDestroy()
    {
        ReleaseNotifications();
    }
}

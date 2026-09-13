using System;

public enum ObjectHeadMapSelectionMode
{
    Fixed = 0,
    Random = 1
}

[Serializable]
public sealed class ObjectHeadRoomSettings
{
    public const string CurrentRulesetVersion = "demo-2026-09-15";

    public string rulesetVersion = CurrentRulesetVersion;
    public int minPlayers = 2;
    public int maxPlayers = 2;
    public ObjectHeadMapSelectionMode mapSelectionMode = ObjectHeadMapSelectionMode.Fixed;
    public string fixedMapId = "object_head_demo_01";
    public string[] randomMapPool = { "object_head_demo_01" };

    public ObjectHeadRoomSettings Copy()
    {
        return new ObjectHeadRoomSettings
        {
            rulesetVersion = rulesetVersion,
            minPlayers = minPlayers,
            maxPlayers = maxPlayers,
            mapSelectionMode = mapSelectionMode,
            fixedMapId = fixedMapId,
            randomMapPool = randomMapPool != null ? (string[])randomMapPool.Clone() : Array.Empty<string>()
        };
    }
}

[Serializable]
public sealed class ObjectHeadLobbyPlayer
{
    public string userId;
    public string username;
    public bool ready;
    public int playerIndex;
}

[Serializable]
public sealed class ObjectHeadLobbyState
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public int revision;
    public string matchId;
    public string roomCode;
    public string hostUserId;
    public ObjectHeadRoomSettings settings = new ObjectHeadRoomSettings();
    public ObjectHeadLobbyPlayer[] players = Array.Empty<ObjectHeadLobbyPlayer>();
}

[Serializable]
public sealed class ObjectHeadRoomCodeRequest
{
    public string match_id;
}

[Serializable]
public sealed class ObjectHeadRoomCodeResponse
{
    public string room_code;
    public string match_id;
}

[Serializable]
public sealed class ObjectHeadPlayerHello
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string username;
}

[Serializable]
public sealed class ObjectHeadReadyRequest
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public bool ready;
}

[Serializable]
public sealed class ObjectHeadSettingsRequest
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public ObjectHeadRoomSettings settings;
}

public static class ObjectHeadNetworkProtocol
{
    public const int ProtocolVersion = 1;

    public const long PlayerHello = 1;
    public const long LobbyState = 2;
    public const long ReadyRequest = 3;
    public const long SettingsRequest = 4;
    public const long GameStart = 5;

    // Gameplay opcodes are reserved now so lobby work never collides with them.
    public const long GameplayCommand = 100;
    public const long GameplayEvent = 101;
    public const long TerrainOperation = 110;
    public const long StateSnapshot = 120;
}

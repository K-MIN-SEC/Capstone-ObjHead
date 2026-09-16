using System;

[Serializable]
public sealed class ObjectHeadPlayerAssignment
{
    public string userId;
    public string username;
    public int playerIndex;
    public int allianceId;
    public ObjectHeadCharacterKind[] characters = Array.Empty<ObjectHeadCharacterKind>();
}

[Serializable]
public sealed class GameStartData
{
    public static GameStartData Instance { get; private set; }

    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string matchId;
    public bool localMatch;
    public ObjectHeadMatchMode mode;
    public string rulesetVersion;
    public int playerCount;
    public ObjectHeadMapSelectionMode mapSelectionMode;
    public string mapId;
    public int mapSeed;
    public int characterSpawnSeed;
    public int startingPlayerIndex = 1;
    public long startedAtUnixMilliseconds;
    public ObjectHeadPlayerAssignment[] players = Array.Empty<ObjectHeadPlayerAssignment>();

    public static void Apply(GameStartData data)
    {
        Instance = data;
    }

    public static void Clear()
    {
        Instance = null;
    }
}

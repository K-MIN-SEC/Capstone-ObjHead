using System;

public enum ObjectHeadGameplayMessageKind
{
    CharacterSnapshot = 1,
    FireCommand = 2,
    TurnState = 3,
    EndTurnRequest = 4
}

[Serializable]
public sealed class ObjectHeadGameplayMessage
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string messageId;
    public ObjectHeadGameplayMessageKind kind;
    public string characterId;
    public int turnSerial;
    public int roundSerial;
    public int currentTurnIndex;
    public int currentPlayerIndex;
    public int phase;
    public bool actionUsed;
    public bool residualTimeActive;
    public float remainingTurnSeconds;
    public float remainingResidualSeconds;
    public float positionX;
    public float positionY;
    public float velocityX;
    public float velocityY;
    public float aimX;
    public float aimY;
    public int selectedSkillIndex;
    public float normalizedPower;
}

[Serializable]
public sealed class ObjectHeadTerrainOperationMessage
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string messageId;
    public int turnSerial;
    public TerrainEditOperation operation;
}

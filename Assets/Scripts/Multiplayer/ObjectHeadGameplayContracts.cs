using System;

public enum ObjectHeadGameplayMessageKind
{
    CharacterSnapshot = 1,
    FireCommand = 2,
    TurnState = 3,
    EndTurnRequest = 4,
    CommonUseRequest = 5,
    CommonUseAccepted = 6,
    MovementInput = 7,
    FireAccepted = 8,
    ImpactPresentation = 9,
    ProjectileLanded = 10
}

[Serializable]
public sealed class ObjectHeadGameplayMessage
{
    public long matchEpoch;
    public ObjectHeadProjectileState[] projectiles;
    public string projectileId;
    public bool projectileCulled;
    public ObjectHeadCombatState[] combatStates;
    public ObjectHeadInventoryState[] inventoryStates;
    public ObjectHeadWorldItemState[] worldItems;
    public long stateSequence;
    public int commonSlot=-1;
    public CommonHeadType commonType;
    public bool matchOver;
    public int winner;
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string messageId;
    public bool gourdRequest;
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
    public bool settlementPending;
    public bool networkStartPending;
    public float positionX;
    public float positionY;
    public float velocityX;
    public float velocityY;
    public float aimX;
    public float aimY;
    public int selectedSkillIndex;
    public string gourdChoiceId;
    public float normalizedPower;
    public float moveX;
    public bool jumpHeld;
    public bool jumpPressed;
    public int skillId;
    public float radius;
}

[Serializable]
public sealed class ObjectHeadProjectileState
{
    public string id;
    public float x,y,vx,vy,rotation;
}

[Serializable]
public sealed class ObjectHeadTerrainOperationMessage
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public string messageId;
    public int turnSerial;
    public TerrainEditOperation operation;
}

[Serializable]
public sealed class ObjectHeadCombatState
{
    public bool grounded;
    public bool facingRight;
    public int selectedSkill;
    public int commonSlot = -1;
    public int[] cooldowns;
    public float aimX, aimY;
    public ObjectHeadGourdCharge[] gourdCharges;
    public string characterId;
    public int hp;
    public int pending;
    public int shield;
    public int captiveTurns;
    public float x,y,vx,vy;
}

[Serializable]
public sealed class ObjectHeadInventoryState
{
    public int playerIndex;
    public CommonHeadType[] slots;
}

[Serializable]
public sealed class ObjectHeadWorldItemState
{
    public string id;
    public CommonHeadType type;
    public float x,y;
}

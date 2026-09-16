using System;
using System.Linq;

public enum ObjectHeadMatchMode { Duel = 0, FreeForAll = 1, Teams = 2 }

[Serializable]
public sealed class ObjectHeadModeDefinition
{
    public ObjectHeadMatchMode mode;
    public string nameKey;
    public int players;
    // Indexed by player seat; owner identity remains independent from alliance identity.
    public int[] alliances;
    public int[] spawnSeats;
    public int Alliance(int player) => alliances[player - 1];
}

public static class ObjectHeadMatchRules
{
    public static ObjectHeadModeDefinition Current => ObjectHeadContent.Load()?.Mode(GameStartData.Instance != null
        ? GameStartData.Instance.mode : ObjectHeadMatchMode.Duel);
    public static int Alliance(int player) => Current != null && player > 0 && player <= Current.alliances.Length
        ? Current.Alliance(player) : player;
    public static bool IsTeamMatch => GameStartData.Instance?.mode == ObjectHeadMatchMode.Teams;
}

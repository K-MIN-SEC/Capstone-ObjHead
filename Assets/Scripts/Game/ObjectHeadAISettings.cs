using System;
using System.Linq;

public enum ObjectHeadAIDifficulty
{
    Beginner,
    Normal,
    Pro
}

public readonly struct ObjectHeadAIProfile
{
    public readonly float decisionDelay;
    public readonly float aimErrorDegrees;
    public readonly float powerError;

    public ObjectHeadAIProfile(float decisionDelay, float aimErrorDegrees, float powerError)
    {
        this.decisionDelay = decisionDelay;
        this.aimErrorDegrees = aimErrorDegrees;
        this.powerError = powerError;
    }
}

/// <summary>Data-backed AI presets and roster generation shared by UI, gameplay and tests.</summary>
public static class ObjectHeadAISettings
{
    public static ObjectHeadAIProfile Profile(ObjectHeadAIDifficulty difficulty)
    {
        ObjectHeadBalanceTable balance = ObjectHeadBalanceTable.Load();
        string key = difficulty.ToString().ToLowerInvariant();
        float fallbackDelay = difficulty == ObjectHeadAIDifficulty.Beginner ? 1.25f : difficulty == ObjectHeadAIDifficulty.Normal ? .7f : .32f;
        float fallbackAim = difficulty == ObjectHeadAIDifficulty.Beginner ? 12f : difficulty == ObjectHeadAIDifficulty.Normal ? 5f : 0f;
        float fallbackPower = difficulty == ObjectHeadAIDifficulty.Beginner ? .14f : difficulty == ObjectHeadAIDifficulty.Normal ? .06f : 0f;
        // Difficulty changes execution accuracy and reaction time, not tactical options.
        // Pro is exact even when an older balance sheet still contains non-zero values.
        return new ObjectHeadAIProfile(
            balance != null ? balance.GetFloat($"ai.{key}.decision_delay", fallbackDelay) : fallbackDelay,
            difficulty == ObjectHeadAIDifficulty.Pro ? 0f : Math.Max(0f,balance != null ? balance.GetFloat($"ai.{key}.aim_error_degrees", fallbackAim) : fallbackAim),
            difficulty == ObjectHeadAIDifficulty.Pro ? 0f : Math.Max(0f,balance != null ? balance.GetFloat($"ai.{key}.power_error", fallbackPower) : fallbackPower));
    }

    public static ObjectHeadCharacterKind[] CreateRoster(ObjectHeadContent content, int players, Random random, bool randomized)
    {
        if (content == null || content.characters == null || content.characters.Length == 0)
            return Array.Empty<ObjectHeadCharacterKind>();
        if (!randomized) return content.DefaultSelection(players);
        int count = content.CharactersPerPlayer(players);
        return Enumerable.Range(0, count)
            .Select(_ => content.characters[random.Next(content.characters.Length)].kind)
            .ToArray();
    }
}

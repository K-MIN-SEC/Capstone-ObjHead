using UnityEngine;

[CreateAssetMenu(menuName = "Object Head/AI Tactics")]
public sealed class ObjectHeadAITuning : ScriptableObject
{
    [Header("Bounded shot search (per target / skill)")]
    [Range(3, 18)] public int beginnerPowerSamples = 4;
    [Range(3, 18)] public int normalPowerSamples = 7;
    [Range(3, 18)] public int proPowerSamples = 11;
    [Range(.05f, .9f)] public float minimumPower = .2f;
    [Min(.02f)] public float trajectoryStep = .04f;
    [Min(.2f)] public float trajectorySeconds = 4f;
    [Min(1)] public int candidatesPerFrame = 8;
    [Min(.1f)] public float planningBudgetSeconds = 1.2f;
    [Header("Tactical utility")]
    [Min(1)] public float friendlyFirePenalty = 1.8f;
    [Min(1)] public float selfDamagePenalty = 2.4f;
    [Min(0)] public float killBonus = 18f;
    [Min(0)] public float healingWeight = 1.2f;
    [Min(0)] public float zoneWeight = .55f;
    [Min(0)] public float controlWeight = 12f;
    [Min(0)] public float cooldownCost = .6f;
    [Min(0)] public float minimumShotUtility = .5f;
    [Header("Movement and post-shot escape")]
    [Min(0)] public float retreatSeconds = 1.1f;
    [Min(0)] public float dangerClearance = .6f;
    [Min(.1f)] public float landingWaitSeconds = .7f;
    [Min(.1f)] public float navigationBudgetSeconds = 1.6f;
    [Range(2,12)] public int navigationPositionSamples = 6;
    [Min(.1f)] public float navigationStepWorld = 1.25f;
    [Min(.1f)] public float movementSearchSeconds = 3f;
    [Min(.01f)] public float jumpTraceStep = .035f;
    [Min(.1f)] public float jumpTraceSeconds = 1.7f;
    [Min(.1f)] public float walkStepHeight = .22f;
    private static ObjectHeadAITuning loaded;
    public static ObjectHeadAITuning Load()
    {
        if (loaded == null) loaded = Resources.Load<ObjectHeadAITuning>("ObjectHeadAITuning");
        // Safe for older scenes; the authoring tool saves the editable asset for release.
        if (loaded == null) loaded = CreateInstance<ObjectHeadAITuning>();
        return loaded;
    }
    public int PowerSamples(ObjectHeadAIDifficulty difficulty) => Mathf.Clamp(
        difficulty == ObjectHeadAIDifficulty.Beginner ? beginnerPowerSamples :
        difficulty == ObjectHeadAIDifficulty.Pro ? proPowerSamples : normalPowerSamples, 3, 18);
}

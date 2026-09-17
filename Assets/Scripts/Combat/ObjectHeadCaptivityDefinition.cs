using UnityEngine;

[CreateAssetMenu(menuName="Object Head/Captivity Definition")]
public sealed class ObjectHeadCaptivityDefinition : ScriptableObject
{
    public ObjectHeadCageVisual cagePrefab;
    [Range(1,2)] public int ownTurns=2;
    [Min(0)] public int damagePerOwnTurn=6;
    [Min(.1f)] public float skippedTurnPresentationSeconds=.65f;
    public int Turns => Mathf.Clamp(ObjectHeadBalanceTable.Load()?.GetInt("common.lock.turns",ownTurns)??ownTurns,1,2);
    public int Damage => Mathf.Max(0,ObjectHeadBalanceTable.Load()?.GetInt("common.lock.tick_damage",damagePerOwnTurn)??damagePerOwnTurn);
    public static ObjectHeadCaptivityDefinition Load()=>Resources.Load<ObjectHeadCaptivityDefinition>("ObjectHeadCaptivity");
}

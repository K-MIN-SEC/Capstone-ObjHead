using UnityEngine;

// Adding a character can reuse an effect behaviour without adding a switch branch.
[CreateAssetMenu(menuName="Object Head/Skill Definition")]
public sealed class ObjectHeadSkillDefinition : ScriptableObject
{
    public string balancePrefix;
    public string nameKey;
    public string descriptionKey;
    [Min(0)] public int cooldown;
    public ObjectHeadSkillSettings settings;
    public int Cooldown => ObjectHeadBalanceTable.Load()?.GetInt(balancePrefix+".cooldown",cooldown) ?? cooldown;
    public ObjectHeadSkillSettings Resolve(CharacterVisual visual)
    {
        var result=settings;
        if(result.vacuum!=null)result.vacuum.ApplySheet();
        var table=ObjectHeadBalanceTable.Load();
        if(table!=null)
        {
            result.maxDamage=table.GetInt(balancePrefix+".max_damage",result.maxDamage);
            result.healing=table.GetInt(balancePrefix+".healing",result.healing);
            result.explosionRadiusWorld=table.GetFloat(balancePrefix+".radius",result.explosionRadiusWorld);
            result.straightSpeed=table.GetFloat(balancePrefix+".speed",result.straightSpeed);
            result.chainCount=table.GetInt(balancePrefix+".count",result.chainCount);
            result.chainSpacingWorld=table.GetFloat(balancePrefix+".spacing",result.chainSpacingWorld);
            result.delaySeconds=table.GetFloat(balancePrefix+".delay",result.delaySeconds);
            result.knockbackForce=table.GetFloat(balancePrefix+".force",result.knockbackForce);
            result.zoneDamagePerTurn=table.GetInt(balancePrefix+".zone_damage",result.zoneDamagePerTurn);
            result.zoneDurationRounds=table.GetInt(balancePrefix+".zone_rounds",result.zoneDurationRounds);
            result.zoneLengthWorld=table.GetFloat(balancePrefix+".zone_length",result.zoneLengthWorld);
            result.slowMultiplier=table.GetFloat(balancePrefix+".slow",result.slowMultiplier);
            result.terrainRadiusPx=table.GetInt(balancePrefix+".terrain_radius_px",result.terrainRadiusPx);
        }
        if(result.projectileSprite!=null)result.headSprite=result.projectileSprite;
        else if(visual!=null) result.headSprite=visual.CurrentHeadSprite;
        return result;
    }
}

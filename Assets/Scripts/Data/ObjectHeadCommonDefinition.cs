using System;
using UnityEngine;

public enum ObjectHeadCommonUse { Projectile, SelfShield }
[Serializable]
public sealed class ObjectHeadCommonDefinition
{
    public CommonHeadType type;
    public string nameKey;
    public Sprite sprite;
    [Min(.1f)] public float worldVisualSize=.7f;
    public ObjectHeadSkillDefinition skill;
    public ObjectHeadCommonUse use;
    [Min(0)]public int spawnCount=1;
    [Min(0)]public int shieldAmount=25;
}

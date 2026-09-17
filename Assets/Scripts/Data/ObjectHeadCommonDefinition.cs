using System;
using UnityEngine;

public enum ObjectHeadCommonUse { Projectile, SelfShield, EraseTerrain }
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
    [Min(.5f)]public float eraseMinimumRadius=2.4f;
    [Min(.5f)]public float eraseMaximumRadius=4.8f;
    [Min(.05f)]public float eraseClearance=.25f;
}

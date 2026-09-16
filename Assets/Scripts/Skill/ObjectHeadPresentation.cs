using System;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadSkillPresentation
{
    public int skillId;
    public Sprite zoneSprite;
    public GameObject launchPrefab;
    public GameObject flightPrefab;
    public GameObject impactPrefab;
    [Min(.1f)] public float impactScale = 1f;
}

/// <summary>Visual-only assets. Does not apply damage, terrain edits, or turn changes.</summary>
[CreateAssetMenu(menuName="Object Head/Skill Presentation")]
public sealed class ObjectHeadPresentation : ScriptableObject
{
    public static event Action<int,Vector2,float> ImpactCommitted;
    public Sprite smokeSprite,cloudTerrainSprite,dirtTerrainSprite,airstrikeBombSprite;
    [Min(1)]public float airstrikeDropHeight=8;
    [Min(.1f)]public float airstrikeFallSeconds=.45f;
    public ObjectHeadSkillPresentation[] skills;
    [Min(1)] public int maximumEffects = 48;
    private static ObjectHeadPresentation loaded;
    private static int activeEffects;
    public static ObjectHeadPresentation Load() => loaded != null ? loaded : loaded = Resources.Load<ObjectHeadPresentation>("ObjectHeadPresentation");
    public ObjectHeadSkillPresentation Find(int skillId) => Array.Find(skills ?? Array.Empty<ObjectHeadSkillPresentation>(), s => s.skillId == skillId);

    public static void Launch(int id, Transform projectile)
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return;
        var library=Load(); var profile=library != null ? library.Find(id) : null;
        if(profile==null)return;
        Spawn(profile.launchPrefab,projectile.position,1);
        if(profile.flightPrefab!=null)
        {
            var flight=Instantiate(profile.flightPrefab,projectile.position,Quaternion.identity);
            // Keep the prefab's authored world scale; projectile sprite sizing must not resize effects.
            flight.transform.SetParent(projectile,true);
        }
    }

    public static bool Impact(int id, Vector2 point, float radius)
    {
        if(ObjectHeadCommonAuthority.IsDedicatedMatch)
        {
            if(ObjectHeadCommonAuthority.CanWrite)ImpactCommitted?.Invoke(id,point,radius);
            return true; // Network clients display only the confirmed impact, not their predicted collision.
        }
        return PresentConfirmedImpact(id,point,radius);
    }
    public static bool PresentConfirmedImpact(int id,Vector2 point,float radius)
    {
        var library=Load(); var profile=library != null ? library.Find(id) : null;
        if(profile?.impactPrefab==null)return false;
        Spawn(profile.impactPrefab,point,Mathf.Max(.3f,radius)*profile.impactScale);
        return true;
    }

    private static void Spawn(GameObject prefab,Vector3 point,float scale)
    {
        if(prefab==null || activeEffects>=Load().maximumEffects)return;
        var effect=Instantiate(prefab,point,Quaternion.identity);
        effect.transform.localScale*=scale;
        activeEffects++;
        effect.AddComponent<ObjectHeadEffectLifetime>();
    }
    internal static void Release()=>activeEffects=Mathf.Max(0,activeEffects-1);
}

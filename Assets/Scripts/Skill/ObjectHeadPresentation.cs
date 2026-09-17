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
    public GameObject warningPrefab;
    [Min(.1f)] public float impactScale = 1f;
    [Range(0f, .3f)] public float cameraImpulse;
}

/// <summary>Visual-only assets. Does not apply damage, terrain edits, or turn changes.</summary>
[CreateAssetMenu(menuName="Object Head/Skill Presentation")]
public sealed class ObjectHeadPresentation : ScriptableObject
{
    public static event Action<int,Vector2,float> ImpactCommitted;
    public static event Action<Vector2,float,float> ImpactPresented;
    public Sprite smokeSprite,cloudTerrainSprite,dirtTerrainSprite,airstrikeBombSprite;
    [Header("Helicopter support (visual-only; one final authoritative damage event)")]
    public Sprite supportHelicopter, supportPilot, supportHeavyHead;
    public Sprite supportLauncherPilot,rainbowCat;
    public Sprite[] supportMachineGunFrames,supportLauncherFrames;
    public Sprite supportDoorMaskSprite;
    [Min(.01f)] public float supportFiringFrameSeconds=.06f;
    public Vector2 supportDoorMaskSize=new Vector2(1.45f,.72f);
    public Vector2 supportDoorMaskOffset=new Vector2(.88f,-.15f);
    public Vector2 supportMuzzleOffset=new Vector2(1.46f,-.32f);
    public Vector2 supportLauncherMuzzleOffset=new Vector2(1.46f,-.13f);
    public Sprite supportTracerSprite;
    public Vector2 supportTracerSize=new Vector2(.35f,.045f);
    public Color supportTracerColor=new Color(1f,.88f,.48f,1f);
    public float supportRocketRotationOffset=90f;
    [Min(.01f)] public float supportRotorThickness=.045f;
    [Range(.01f,.3f)] public float supportRotorPerspective=.08f;
    [Range(0f,1f)] public float supportRotorTrailOpacity=.24f;
    [Header("Rainbow TV sweep")]
    [Min(.2f)] public float rainbowSeconds=3;
    [Min(.1f)] public float rainbowSize=3;
    [Min(.1f)] public float rainbowTerrainStep=.5f;
    public Vector2 supportPilotOffset=new Vector2(.25f,.3f);
    public Vector2 supportRotorOffset=new Vector2(0,.7f);
    public float supportRotorWidth=3.2f,supportRotorRate=32;
    [Min(.1f)] public float supportHelicopterSize=4.5f;
    [Min(.1f)] public float supportPilotSize=1.3f;
    [Min(.1f)] public float supportOpeningSeconds=.7f;
    [Min(0)] public float supportSilenceSeconds=.65f;
    [Min(.1f)] public float supportArrivalSeconds=1.1f;
    [Min(.1f)] public float supportAttackSeconds=1f;
    [Min(1)] public float supportHoverHeight=5f;
    [Min(.1f)] public float teleportSearchRadius=2f;
    [Min(.05f)] public float teleportClearance=.12f;
    [Header("Ground zone artwork")]
    [Min(.05f)] public float groundZoneArtHeight = .3f;
    [Range(.5f,1.5f)] public float groundZoneArtSpacing = .9f;
    [Range(0f,.5f)] public float groundZoneTerrainOverlap = .22f;
    [Min(1)]public float airstrikeDropHeight=8;
    [Min(.1f)]public float airstrikeFallSeconds=.45f;
    [Header("Sky drop artwork (world size, independent of damage radius)")]
    public bool dropsFromSkyEdge=true;
    [Min(0)] public float skyDropMargin=1.5f;
    [Min(.1f)] public float airstrikeBombSize=1.5f;
    [Header("Healing supply drop")]
    public Sprite healingSupplySprite;
    [Min(.1f)] public float healingSupplySize = .8f;
    public ObjectHeadSkillPresentation[] skills;
    [Min(1)] public int maximumEffects = 48;
    private static ObjectHeadPresentation loaded;
    private static int activeEffects;
    [Header("Eraser (cosmetic only)")]
    [Min(.1f)] public float eraserSweepSeconds=.7f;
    [Min(.1f)] public float eraserVisualSize=1.1f;
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
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return true;
        var library=Load(); var profile=library != null ? library.Find(id) : null;
        if(profile?.impactPrefab==null)return false;
        Spawn(profile.impactPrefab,point,Mathf.Max(.3f,radius)*profile.impactScale);
        ImpactPresented?.Invoke(point,radius,profile.cameraImpulse);
        return true;
    }

    public static GameObject Warning(int id, Vector2 point, float radius)
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return null;
        var profile=Load()?.Find(id);
        if(profile?.warningPrefab==null)return null;
        var marker=Instantiate(profile.warningPrefab,point,Quaternion.identity);
        marker.transform.localScale*=Mathf.Max(.5f,radius*2f);
        return marker;
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

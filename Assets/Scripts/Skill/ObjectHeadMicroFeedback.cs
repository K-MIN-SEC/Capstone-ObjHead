using System;
using UnityEngine;

public enum ObjectHeadMicroCue { Debris, Dust, Step, Jump, Land, Launch, Hit, Water, EraserCrumb }

[Serializable]
public sealed class ObjectHeadMicroProfile
{
    public ObjectHeadMicroCue cue;
    public Sprite sprite;
    public Color tint=Color.white;
    [Range(1,40)] public int count=5;
    public Vector2 size=new Vector2(.08f,.22f);
    public Vector2 lifetime=new Vector2(.25f,.65f);
    public Vector2 speed=new Vector2(.6f,2f);
    [Range(0,180)] public float spread=65;
    [Min(0)] public float gravity=3;
    [Min(0)] public float scatter=.1f;
    public float spin=140;
    [Min(0)] public float endScale=.5f;
}

[CreateAssetMenu(menuName="Object Head/Micro Feedback")]
public sealed class ObjectHeadMicroFeedback : ScriptableObject
{
    public bool effectsEnabled=true;
    [Range(16,512)] public int particleBudget=160;
    [Range(4,128)] public int emissionsPerFrame=48;
    [Min(0)] public float visibilityMargin=2;
    public int sortingOrder=22;
    [Header("Motion thresholds")]
    [Min(.05f)] public float footstepDistance=.65f;
    [Min(.05f)] public float footstepInterval=.18f;
    [Min(.05f)] public float airborneGrace=.12f;
    [Min(.1f)] public float landingSpeed=1.8f;
    [Min(.01f)] public float hitInterval=.15f;
    [Min(0)] public float recoilDistance=.055f;
    [Min(.01f)] public float recoilSeconds=.12f;
    public ObjectHeadMicroProfile[] profiles;
    private static ObjectHeadMicroFeedback cached;
    public static ObjectHeadMicroFeedback Load()=>cached!=null?cached:cached=Resources.Load<ObjectHeadMicroFeedback>("ObjectHeadMicroFeedback");
    public ObjectHeadMicroProfile Find(ObjectHeadMicroCue cue)=>Array.Find(profiles??Array.Empty<ObjectHeadMicroProfile>(),p=>p!=null&&p.cue==cue);
}

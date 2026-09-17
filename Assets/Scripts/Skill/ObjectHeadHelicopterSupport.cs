using System.Collections;
using UnityEngine;

/// <summary>Cosmetic sequence only. SkillProjectile owns the single damage/terrain commit after this returns.</summary>
public static class ObjectHeadHelicopterSupport
{
    public static int LastVariant {get;private set;}
    public static bool IsPresenting {get;private set;}
    public static IEnumerator Play(Vector2 target,ObjectHeadSkillSettings skill,int serial)
    {
        var art=ObjectHeadPresentation.Load();
        if(art==null)yield break;
        bool visible=ObjectHeadNetworkManager.Instance?.IsDedicatedWorker!=true;
        // Stable cosmetic choice independent of gameplay RNG and frame rate.
        int seed=unchecked(serial*73856093+Mathf.RoundToInt(target.x*32)*19349663+Mathf.RoundToInt(target.y*32)*83492791);
        int variant=new System.Random(seed).Next(2);LastVariant=variant;IsPresenting=true;
        GameObject helicopter=null;
        ObjectHeadSupportPilotAnimation pilotAnimation=null;
        try
        {
            // Loud heavy-weapon impact: effects, but deliberately no HP or terrain changes.
            ObjectHeadPresentation.Impact(111,target,skill.explosionRadiusWorld*.75f);
            if(visible)ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Dust,target,Vector2.up,2);
            yield return new WaitForSeconds(art.supportOpeningSeconds);
            yield return new WaitForSeconds(art.supportSilenceSeconds);
            Vector2 hover=target+Vector2.up*art.supportHoverHeight;
            var cam=Camera.main;
            Vector2 start=hover+Vector2.left*(cam!=null?cam.orthographicSize*cam.aspect+art.supportHelicopterSize:20);
            if(visible && art.supportHelicopter!=null)
            {
                helicopter=SpriteObject("SupportHelicopter",art.supportHelicopter,art.supportHelicopterSize,hover,38);
                if(art.supportPilot!=null)
                {
                    var pilot=SpriteObject("WormsPilot",variant==1 && art.supportLauncherPilot!=null?art.supportLauncherPilot:art.supportPilot,art.supportPilotSize,hover+art.supportPilotOffset,39);
                    pilot.transform.SetParent(helicopter.transform,true);
                    pilotAnimation=pilot.AddComponent<ObjectHeadSupportPilotAnimation>();
                    pilotAnimation.Configure(variant==0?art.supportMachineGunFrames:art.supportLauncherFrames,art.supportFiringFrameSeconds);
                    if(art.supportDoorMaskSprite!=null)
                    {
                        var maskObject=new GameObject("CabinPilotMask",typeof(SpriteMask));var mask=maskObject.GetComponent<SpriteMask>();mask.sprite=art.supportDoorMaskSprite;
                        mask.isCustomRangeActive=true;mask.frontSortingOrder=39;mask.backSortingOrder=38;
                        maskObject.transform.position=hover+art.supportDoorMaskOffset;
                        maskObject.transform.localScale=new Vector3(art.supportDoorMaskSize.x/mask.sprite.bounds.size.x,art.supportDoorMaskSize.y/mask.sprite.bounds.size.y,1);
                        maskObject.transform.SetParent(helicopter.transform,true);pilot.GetComponent<SpriteRenderer>().maskInteraction=SpriteMaskInteraction.VisibleInsideMask;
                    }
                }
                helicopter.AddComponent<ObjectHeadSupportRotor>().Configure(art);
                ObjectHeadSpecialAudio.Play("rotor",helicopter.transform,true);
            }
            for(float t=0;t<art.supportArrivalSeconds;t+=Time.deltaTime)
            {
                if(helicopter!=null)helicopter.transform.position=Vector2.Lerp(start,hover,Mathf.SmoothStep(0,1,t/art.supportArrivalSeconds));
                yield return null;
            }
            float nextShot=0;
            for(float t=0;t<art.supportAttackSeconds;t+=Time.deltaTime)
            {
                if(helicopter!=null)helicopter.transform.position=hover+Vector2.up*(Mathf.Sin(t*25)*.045f);
                if(visible && t>=nextShot)
                {
                    nextShot=t+(variant==0?.1f:art.supportAttackSeconds);
                    ObjectHeadSpecialAudio.Play(variant==0?"mg":"rocket",helicopter!=null?helicopter.transform:null);
                    pilotAnimation?.Shoot();
                    var from=hover+(variant==0?art.supportMuzzleOffset:art.supportLauncherMuzzleOffset);
                    var shot=SpriteObject(variant==0?"SupportTracer":"SupportRocket",variant==0?art.supportTracerSprite:art.airstrikeBombSprite,variant==0?art.supportTracerSize.x:.7f,from,40);
                    if(variant==0 && art.supportTracerSprite!=null)
                    {
                        shot.transform.localScale=new Vector3(art.supportTracerSize.x/art.supportTracerSprite.bounds.size.x,art.supportTracerSize.y/art.supportTracerSprite.bounds.size.y,1);
                        shot.GetComponent<SpriteRenderer>().color=art.supportTracerColor;
                    }
                    shot.AddComponent<ObjectHeadSupportRound>().Configure(from,target,variant==0?.12f:art.supportAttackSeconds,variant==0?0:art.supportRocketRotationOffset);
                    ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Hit,from,(target-from).normalized,1.5f);
                    if(variant==0)ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Dust,target,Vector2.up,.7f);
                }
                yield return null;
            }
            if(visible)ObjectHeadSpecialAudio.Play("final");
            // Leave a short departing silhouette while the shared final explosion is committed.
            if(helicopter!=null)
            {helicopter.AddComponent<ObjectHeadSupportDeparture>().direction=Vector2.right*12+Vector2.up*3;helicopter=null;}
        }
        finally {IsPresenting=false;if(helicopter!=null)Object.Destroy(helicopter);}
    }
    private static GameObject SpriteObject(string name,Sprite sprite,float width,Vector2 point,int order)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=order;
        go.transform.position=point;
        if(sprite!=null)go.transform.localScale=Vector3.one*(width/Mathf.Max(.01f,sprite.bounds.size.x));
        return go;
    }
}

public sealed class ObjectHeadSupportRotor:MonoBehaviour
{
    private LineRenderer blade,trail;private ObjectHeadPresentation art;private Material material;
    public void Configure(ObjectHeadPresentation config)
    {
        art=config;var go=new GameObject("SpinningRotor");go.transform.SetParent(transform,false);
        blade=go.AddComponent<LineRenderer>();material=new Material(Shader.Find("Sprites/Default"));blade.sharedMaterial=material;
        blade.useWorldSpace=true;blade.positionCount=2;blade.startWidth=blade.endWidth=art.supportRotorThickness;blade.sortingOrder=40;blade.startColor=blade.endColor=new Color(.1f,.11f,.12f,.85f);
        var ghost=new GameObject("RotorMotionTrail");ghost.transform.SetParent(transform,false);trail=ghost.AddComponent<LineRenderer>();
        trail.sharedMaterial=material;trail.useWorldSpace=true;trail.positionCount=25;trail.loop=true;
        trail.startWidth=trail.endWidth=art.supportRotorThickness;trail.sortingOrder=40;
        trail.startColor=trail.endColor=new Color(.1f,.11f,.12f,art.supportRotorTrailOpacity);
    }
    private void Update()
    {
        if(blade==null)return;Vector3 center=transform.position+(Vector3)art.supportRotorOffset;
        float phase=Time.time*art.supportRotorRate,half=art.supportRotorWidth*.5f;
        var direction=new Vector3(Mathf.Cos(phase),Mathf.Sin(phase)*art.supportRotorPerspective)*half;
        blade.SetPosition(0,center-direction);blade.SetPosition(1,center+direction);
        for(int i=0;i<trail.positionCount;i++)
        {float angle=i*Mathf.PI*2/trail.positionCount;trail.SetPosition(i,center+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*art.supportRotorPerspective)*half);}
    }
    private void OnDestroy(){if(material!=null)Destroy(material);}
}

public sealed class ObjectHeadSupportRound:MonoBehaviour
{
    private Vector2 from,to;private float duration,born;
    public void Configure(Vector2 a,Vector2 b,float seconds,float artworkAngle=0){from=a;to=b;duration=Mathf.Max(.01f,seconds);born=Time.time;transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg+artworkAngle);}
    private void Update(){float t=(Time.time-born)/duration;transform.position=Vector2.Lerp(from,to,t);if(t>=1)Destroy(gameObject);}
}
public sealed class ObjectHeadSupportDeparture:MonoBehaviour
{
    public Vector2 direction;private float age;
    private void Update(){age+=Time.deltaTime;transform.position+=(Vector3)direction*Time.deltaTime;if(age>2)Destroy(gameObject);}
}

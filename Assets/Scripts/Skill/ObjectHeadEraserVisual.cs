using UnityEngine;

/// <summary>Short scrubbing animation. No pre-use range preview and no gameplay changes.</summary>
public sealed class ObjectHeadEraserVisual:MonoBehaviour
{
    private SpriteRenderer eraser;
    private bool burst;private float born,duration,radius,nextCrumb;private Vector2 center;private ObjectHeadPresentation art;
    public static float Radius(float power,Collider2D body=null)
    {
        var def=ObjectHeadContent.Load()?.Common(CommonHeadType.Eraser);var balance=ObjectHeadBalanceTable.Load();
        float min=balance?.GetFloat("common.eraser.min_radius",def?.eraseMinimumRadius??2.4f)??2.4f;
        float max=balance?.GetFloat("common.eraser.max_radius",def?.eraseMaximumRadius??4.8f)??4.8f;
        float safety=balance?.GetFloat("common.eraser.clearance",def?.eraseClearance??.25f)??.25f;
        return Mathf.Max(Mathf.Lerp(min,Mathf.Max(min,max),Mathf.Clamp01(power)),body!=null?body.bounds.extents.magnitude+safety:0);
    }
    private void Awake()
    {
        art=ObjectHeadPresentation.Load();
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true){enabled=false;return;}
    }
    public static void Play(Vector2 point,float radius,Sprite sprite,float seconds)
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return;
        var go=new GameObject("EraserScrub");var effect=go.AddComponent<ObjectHeadEraserVisual>();effect.burst=true;effect.center=point;effect.radius=radius;effect.duration=seconds;effect.born=Time.time;
        var image=new GameObject("Eraser",typeof(SpriteRenderer));image.transform.SetParent(go.transform,false);effect.eraser=image.GetComponent<SpriteRenderer>();effect.eraser.sprite=sprite;effect.eraser.sortingOrder=31;
        if(sprite!=null)image.transform.localScale=Vector3.one*((effect.art!=null?effect.art.eraserVisualSize:1.1f)/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y));
    }
    private void Update()
    {
        if(burst)
        {
            float t=(Time.time-born)/Mathf.Max(.1f,duration);if(t>=1){Destroy(gameObject);return;}
            float angle=t*Mathf.PI*4;Vector2 offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(radius*Mathf.Lerp(.25f,.85f,t));
            eraser.transform.position=center+offset;eraser.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(t*30)*22);
            if(Time.time>=nextCrumb){nextCrumb=Time.time+.055f;ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.EraserCrumb,center+offset,offset.normalized);ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Dust,center+offset,Vector2.up,.3f);}
            var color=eraser.color;color.a=1-Mathf.InverseLerp(.8f,1,t);eraser.color=color;return;
        }
    }
}

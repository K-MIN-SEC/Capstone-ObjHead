using UnityEngine;

public sealed class ObjectHeadCageVisual : MonoBehaviour
{
    public SpriteRenderer cageRenderer;
    public Sprite closed,halfOpen,open,impact;
    [Min(.1f)] public float fallHeight=6;
    [Min(.05f)] public float fallSeconds=.45f;
    [Min(.05f)] public float closeSeconds=.18f;
    [Min(.05f)] public float releaseSeconds=.35f;
    public AnimationCurve fallCurve=AnimationCurve.EaseInOut(0,0,1,1);
    private Vector3 rest;
    private float born,released=-1,pulseUntil;
    private bool landed;
    private void Awake()
    {
        rest=transform.localPosition;born=Time.time;
        float size=cageRenderer!=null?cageRenderer.bounds.size.y:2.5f;
        var origin=ObjectHeadSkyDrop.Origin(transform.position,size);
        fallHeight=(origin.y-transform.position.y)/Mathf.Max(.01f,transform.parent!=null?transform.parent.lossyScale.y:1);
        transform.localPosition=rest+Vector3.up*fallHeight;
    }
    public void Pulse()=>pulseUntil=Time.time+.18f;
    public void Release()=>released=Time.time;
    private void Update()
    {
        if(released>=0)
        {
            float t=(Time.time-released)/releaseSeconds;
            cageRenderer.sprite=t<.35f?halfOpen:open;
            cageRenderer.color=new Color(1,1,1,1-Mathf.Clamp01((t-.65f)/.35f));
            if(t>=1)Destroy(gameObject);
            return;
        }
        float elapsed=Time.time-born;
        if(elapsed<fallSeconds)
        {
            transform.localPosition=rest+Vector3.up*fallHeight*(1-fallCurve.Evaluate(elapsed/fallSeconds));
            cageRenderer.sprite=open;
        }
        else
        {
            transform.localPosition=rest;
            if(!landed){landed=true;ObjectHeadPresentation.PresentConfirmedImpact(108,transform.position,1.6f);ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Debris,transform.position,Vector2.up,1.4f);}
            cageRenderer.sprite=elapsed<fallSeconds+closeSeconds?halfOpen:Time.time<pulseUntil?impact:closed;
        }
    }
}

using UnityEngine;

public sealed class ObjectHeadLandingMarker : MonoBehaviour
{
    [Min(.1f)] public float pulseSeconds=.85f;
    [Range(0,1)] public float minimumAlpha=.35f;
    private SpriteRenderer sprite;
    private Color color;
    private void Awake(){sprite=GetComponentInChildren<SpriteRenderer>();if(sprite!=null)color=sprite.color;}
    private void Update()
    {
        if(sprite==null)return;
        float wave=.5f+.5f*Mathf.Sin(Time.time*Mathf.PI*2/Mathf.Max(.1f,pulseSeconds));
        sprite.color=new Color(color.r,color.g,color.b,color.a*Mathf.Lerp(minimumAlpha,1,wave));
    }
}

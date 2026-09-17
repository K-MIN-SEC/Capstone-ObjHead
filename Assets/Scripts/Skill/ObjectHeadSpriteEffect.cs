using UnityEngine;

/// <summary>Authored, visual-only sprite animation. Safe to disable without changing the battle.</summary>
public sealed class ObjectHeadSpriteEffect : MonoBehaviour
{
    [Min(.05f)] public float duration = .65f;
    public float rise = .35f;
    public float rotationDegrees = 12f;
    public AnimationCurve size = new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.18f,1.1f),new Keyframe(.5f,1f),new Keyframe(1,1.2f));
    public AnimationCurve opacity = new AnimationCurve(new Keyframe(0,1),new Keyframe(.45f,1),new Keyframe(1,0));
    private Vector3 startScale,startPosition;
    private Quaternion startRotation;
    private SpriteRenderer sprite;
    private Color color;
    private float age;
    public bool IsAlive => age < duration;
    private void Awake()
    {
        sprite=GetComponent<SpriteRenderer>();
        startScale=transform.localScale;startPosition=transform.localPosition;startRotation=transform.localRotation;
        if(sprite!=null)color=sprite.color;
    }
    private void Update()
    {
        age+=Time.deltaTime;
        float t=Mathf.Clamp01(age/Mathf.Max(.05f,duration));
        transform.localScale=startScale*size.Evaluate(t);
        transform.localPosition=startPosition+Vector3.up*(rise*t);
        transform.localRotation=startRotation*Quaternion.Euler(0,0,rotationDegrees*t);
        if(sprite!=null)sprite.color=new Color(color.r,color.g,color.b,color.a*opacity.Evaluate(t));
    }
}

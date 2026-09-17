using UnityEngine;

/// <summary>Small authored squash/rise/orbit sequence. Has no gameplay side effects.</summary>
public sealed class ObjectHeadAbilityCue:MonoBehaviour
{
    public SpriteRenderer icon;
    public SpriteRenderer[] orbitIcons;
    public Vector2 offset;
    [Min(.05f)] public float duration=.8f;
    [Min(.01f)] public float iconSize=.65f;
    public float rise=.3f,orbitRadius=.6f,orbitDegrees=180;
    public AnimationCurve scale=new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.16f,1.15f),new Keyframe(.3f,1),new Keyframe(1,1));
    private Transform follow;private Vector3 origin,baseScale;private float age;
    private SpriteRenderer[] renderers;private Color[] colors;
    public void Configure(Sprite sprite,Transform owner)
    {
        follow=owner;if(icon!=null && sprite!=null)icon.sprite=sprite;
        if(icon!=null && icon.sprite!=null)icon.transform.localScale=Vector3.one*iconSize/Mathf.Max(icon.sprite.bounds.size.x,icon.sprite.bounds.size.y);
    }
    private void Awake()
    {
        origin=transform.position;baseScale=transform.localScale;renderers=GetComponentsInChildren<SpriteRenderer>();colors=new Color[renderers.Length];
        for(int i=0;i<renderers.Length;i++)colors[i]=renderers[i].color;
    }
    private void Update()
    {
        age+=Time.deltaTime;float t=Mathf.Clamp01(age/Mathf.Max(.05f,duration));
        transform.position=(follow!=null?follow.position:origin)+(Vector3)offset+Vector3.up*(rise*t);
        transform.localScale=baseScale*scale.Evaluate(t);
        for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null){var c=colors[i];c.a*=1-Mathf.InverseLerp(.65f,1,t);renderers[i].color=c;}
        for(int i=0;orbitIcons!=null && i<orbitIcons.Length;i++)if(orbitIcons[i]!=null)
        {float a=(i*360f/orbitIcons.Length+t*orbitDegrees)*Mathf.Deg2Rad;orbitIcons[i].transform.localPosition=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.65f)*orbitRadius;}
        if(t>=1)Destroy(gameObject);
    }
}

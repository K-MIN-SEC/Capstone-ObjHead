using UnityEngine;

public sealed class ObjectHeadEffectLifetime : MonoBehaviour
{
    private ParticleSystem[] particles;
    private ObjectHeadSpriteEffect[] sprites;
    private void Start(){particles=GetComponentsInChildren<ParticleSystem>();sprites=GetComponentsInChildren<ObjectHeadSpriteEffect>();}
    private void Update()
    {
        foreach(var particle in particles)if(particle.IsAlive(true))return;
        foreach(var sprite in sprites)if(sprite!=null && sprite.IsAlive)return;
        Destroy(gameObject);
    }
    private void OnDestroy()=>ObjectHeadPresentation.Release();
}

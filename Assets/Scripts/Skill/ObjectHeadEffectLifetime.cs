using UnityEngine;

public sealed class ObjectHeadEffectLifetime : MonoBehaviour
{
    private ParticleSystem[] particles;
    private void Start()=>particles=GetComponentsInChildren<ParticleSystem>();
    private void Update()
    {
        foreach(var particle in particles)if(particle.IsAlive(true))return;
        Destroy(gameObject);
    }
    private void OnDestroy()=>ObjectHeadPresentation.Release();
}

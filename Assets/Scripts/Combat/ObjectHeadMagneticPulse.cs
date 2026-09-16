using System.Collections.Generic;
using UnityEngine;

public static class ObjectHeadMagneticPulse
{
    // One hit per character; polarity affects everyone, regardless of alliance.
    public static void Apply(Vector2 center,float radius,int damage,float force,bool pull)
    {
        if(radius<=0)return;
        var seen=new HashSet<CharacterCombat>();
        foreach(var hit in Physics2D.OverlapCircleAll(center,radius))
        {
            var target=hit.GetComponentInParent<CharacterCombat>();
            if(target==null || target.IsDead || !seen.Add(target))continue;
            float falloff=DamageSystem.CalculateExplosionFalloff(hit,center,target.KnockbackCenter,radius);
            var direction=target.KnockbackCenter-center;
            if(direction.sqrMagnitude<.001f)direction=Vector2.up;
            direction=direction.normalized*(pull?-1:1);
            target.ApplyKnockback(direction*Mathf.Max(0,force)*falloff);
            target.TakeDamage(Mathf.CeilToInt(Mathf.Max(0,damage)*falloff));
        }
    }
}

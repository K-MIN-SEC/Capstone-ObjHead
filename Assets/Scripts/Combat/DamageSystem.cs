using System.Collections.Generic;
using UnityEngine;

public static class DamageSystem
{
    public const bool FriendlyFireEnabled = true;

    public static void ApplyExplosion(Vector2 center, CharacterCombat owner, float radius, int maxDamage, float knockbackForce, float fallbackHorizontalSign = 0f)
    {
        if (radius <= 0f || maxDamage <= 0)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        HashSet<CharacterCombat> damagedCharacters = new HashSet<CharacterCombat>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            CharacterCombat combat = hit.GetComponentInParent<CharacterCombat>();
            if (!CanDamage(owner, combat) || !damagedCharacters.Add(combat))
            {
                continue;
            }

            Vector2 characterCenter = combat.KnockbackCenter;
            float falloff = CalculateExplosionFalloff(hit, center, characterCenter, radius);
            int damage = Mathf.CeilToInt(maxDamage * falloff);

            combat.ApplyExplosionKnockback(center, maxDamage);
            combat.TakeDamage(damage);
        }
    }

    public static float CalculateExplosionFalloff(Collider2D hit, Vector2 center, Vector2 fallbackPoint, float radius)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        Vector2 damagePoint = fallbackPoint;
        if (hit != null && hit.enabled)
        {
            damagePoint = hit.ClosestPoint(center);
        }

        float distanceRatio = Mathf.Clamp01(Vector2.Distance(center, damagePoint) / radius);
        return 1f - distanceRatio;
    }

    public static bool CanDamage(CharacterCombat owner, CharacterCombat target)
    {
        // Owner, allies, and enemies are all valid targets in Object Head Battle.
        return target != null && !target.IsDead;
    }
}

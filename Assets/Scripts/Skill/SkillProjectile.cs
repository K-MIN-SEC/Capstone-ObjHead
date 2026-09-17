using System.Collections;
using System.Collections.Generic;
using Action = System.Action;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class SkillProjectile : MonoBehaviour
{
    private const int CircleTextureSize = 64;

    [SerializeField, Min(0f)] private float flightSpinDegreesPerSecond = 720f;
    [SerializeField, Min(0f)] private float cameraExitCullMarginWorld = 0.25f;

    private static Sprite circleSprite;

    private CharacterCombat owner;
    private TurnManager turnManager;
    private ObjectHeadSkillSettings skillSettings;
    private float explosionFadeSeconds;
    private float remainingLifetime;
    private bool isCompleted;
    private bool completionNotified;
    private bool isFlying;
    private Rigidbody2D body;
    private Vector2 lastVelocity;
    private TerrainManager terrain;
    private Vector2 previousPosition;
    private bool hasPreviousPosition;
    private Vector2 launchDirection;
    private ObjectHeadCameraController cameraController;
    private GameObject landingMarker;

    public bool IsFlying => isFlying && !isCompleted;
    public event Action Resolved;

    public void Initialize(
        Vector2 velocity,
        float radius,
        float gravityScale,
        float lifetime,
        Color color,
        CharacterCombat ownerCombat,
        TurnManager manager,
        int damage,
        float impactRadius,
        float fadeSeconds,
        Color fadeColor,
        float knockback,
        ObjectHeadSkillSettings settings)
    {
        body = GetComponent<Rigidbody2D>();
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        owner = ownerCombat;
        turnManager = manager;
        explosionFadeSeconds = fadeSeconds;
        remainingLifetime = lifetime;
        lastVelocity = velocity;
        launchDirection = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;
        previousPosition = transform.position;
        hasPreviousPosition = true;
        terrain = FindTerrainManager();
        cameraController = FindCameraController();
        skillSettings = settings;
        isFlying = true;

        if (skillSettings.explosionRadiusWorld <= 0f)
        {
            skillSettings.explosionRadiusWorld = impactRadius;
        }

        if (skillSettings.skillId == 0 && skillSettings.maxDamage <= 0)
        {
            skillSettings.maxDamage = damage;
        }

        if (skillSettings.skillId == 0 && skillSettings.knockbackForce <= 0f)
        {
            skillSettings.knockbackForce = knockback;
        }

        if (skillSettings.chainMaxTotalDamage <= 0)
        {
            skillSettings.chainMaxTotalDamage = Mathf.Max(skillSettings.maxDamage, skillSettings.maxDamage * Mathf.Max(1, skillSettings.chainCount));
        }

        if (skillSettings.projectileColor.a <= 0f)
        {
            skillSettings.projectileColor = color;
        }

        if (skillSettings.impactColor.a <= 0f)
        {
            skillSettings.impactColor = fadeColor;
        }

        ApplyProjectileVisual(spriteRenderer, radius);

        circleCollider.radius = 0.5f;
        foreach (var item in FindObjectsByType<CommonHeadItem>(FindObjectsSortMode.None))
            item.IgnoreProjectile(circleCollider);

        body.gravityScale = gravityScale;
        body.freezeRotation = false;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = velocity;
        body.angularVelocity = skillSettings.straightShot ? 0 : CalculateFlightSpinVelocity(velocity);
        if(skillSettings.straightShot)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
        ObjectHeadPresentation.Launch(skillSettings.skillId, transform);
    }

    private void FixedUpdate()
    {
        if (isCompleted || terrain == null || !hasPreviousPosition)
        {
            previousPosition = transform.position;
            hasPreviousPosition = true;
            return;
        }

        Vector2 currentPosition = transform.position;
        if (terrain.TryCheckTerrainHit(previousPosition, currentPosition, out TerrainHit hit))
        {
            if (UsesRollingChainPath())
            {
                previousPosition = currentPosition;
                return;
            }

            ResolveImpact(hit.point);
            return;
        }

        previousPosition = currentPosition;
    }

    private void Update()
    {
        if (isCompleted)
        {
            return;
        }

        if (body != null && body.linearVelocity.sqrMagnitude > 0.001f)
        {
            lastVelocity = body.linearVelocity;
        }

        if (TryResolveCameraExit())
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;

        if (remainingLifetime <= 0f)
        {
            ResolveImpact(transform.position);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<CommonHeadItem>() != null) return;
        if (isCompleted)
        {
            return;
        }

        Vector2 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)transform.position;
        ResolveImpact(impactPoint);
    }

    private void ResolveImpact(Vector2 impactPoint)
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        isFlying = false;

        if (skillSettings.effectType == SkillEffectType.DelayedExplosion && skillSettings.delaySeconds > 0f)
        {
            turnManager?.NotifyPostImpactDelay();
            StartCoroutine(DelayedImpactRoutine(impactPoint));
            return;
        }

        if (skillSettings.blinkBeforeEffect)
        {
            turnManager?.NotifyPostImpactDelay();
            StartCoroutine(BlinkThenImpactRoutine(impactPoint));
            return;
        }

        turnManager?.NotifyResolving();
        StartCoroutine(ResolveSkillEffectRoutine(impactPoint));
    }

    private IEnumerator DelayedImpactRoutine(Vector2 impactPoint)
    {
        DisablePhysicsAtImpact(impactPoint);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = skillSettings.projectileColor;
        }

        yield return new WaitForSeconds(skillSettings.delaySeconds);

        turnManager?.NotifyResolving();
        yield return ResolveSkillEffectRoutine(impactPoint);
    }

    private IEnumerator BlinkThenImpactRoutine(Vector2 impactPoint)
    {
        DisablePhysicsAtImpact(impactPoint);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        float interval = Mathf.Max(0.03f, skillSettings.blinkIntervalSeconds);
        float duration = Mathf.Max(interval, skillSettings.blinkSeconds);
        float elapsed = 0f;
        bool useA = true;

        while (elapsed < duration)
        {
            if (spriteRenderer != null)
            {
                Sprite nextSprite = useA ? skillSettings.blinkSpriteA : skillSettings.blinkSpriteB;
                if (nextSprite != null)
                {
                    spriteRenderer.sprite = nextSprite;
                }

                spriteRenderer.color = useA ? Color.white : skillSettings.projectileColor;
            }

            useA = !useA;
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        turnManager?.NotifyResolving();
        yield return ResolveSkillEffectRoutine(impactPoint);
    }

    private IEnumerator ResolveSkillEffectRoutine(Vector2 impactPoint)
    {
        Vector2 fadePoint = impactPoint;

        if (skillSettings.resolveAtTurnEnd)
        {
            DisablePhysicsAtImpact(impactPoint);
            transform.rotation = Quaternion.identity;
            turnManager?.NotifyPostImpactDelay();
            // The spent flare/lock remains a marker. It has no physics or damage yet.
            landingMarker=ObjectHeadPresentation.Warning(skillSettings.skillId,impactPoint,skillSettings.explosionRadiusWorld);
            if(landingMarker!=null)landingMarker.transform.SetParent(transform,true);
            while (turnManager != null && turnManager.IsResidualTimeActive) yield return null;
            if(landingMarker!=null)Destroy(landingMarker);
            turnManager?.NotifyResolving();
            GetComponent<SpriteRenderer>().enabled = false;
        }

        if(skillSettings.effectType==SkillEffectType.RainbowSweep)
        {
            DisablePhysicsAtImpact(impactPoint);GetComponent<SpriteRenderer>().enabled=false;
            yield return ObjectHeadRainbowSweep.Play(impactPoint,owner,terrain,skillSettings);
        }
        else if (skillSettings.effectType == SkillEffectType.Teleport)
        {
            ObjectHeadTeleport.Apply(owner,terrain,impactPoint);
        }
        else if (skillSettings.effectType == SkillEffectType.HelicopterSupport)
        {
            yield return ObjectHeadHelicopterSupport.Play(impactPoint,skillSettings,turnManager!=null?turnManager.TurnSerial:0);
            // Presentation variants never change combat. Exactly one final explosion.
            DestroyTerrainAtImpact(impactPoint);
            ApplyDamage(impactPoint,0);
            ObjectHeadPresentation.Impact(skillSettings.skillId,impactPoint,skillSettings.explosionRadiusWorld);
        }
        else if (skillSettings.effectType == SkillEffectType.Airstrike)
        {
            yield return ApplyAirstrikeRoutine(impactPoint);
        }
        else if (skillSettings.resolveAtTurnEnd && skillSettings.effectType == SkillEffectType.HealBurst)
        {
            yield return ApplyHealingSupplyRoutine(impactPoint);
        }
        else if (skillSettings.resolveAtTurnEnd && skillSettings.effectType == SkillEffectType.Captivity)
        {
            ObjectHeadCaptivity.CaptureArea(impactPoint, skillSettings.explosionRadiusWorld);
            var cage = ObjectHeadCaptivityDefinition.Load()?.cagePrefab;
            if (cage != null) yield return new WaitForSeconds(cage.fallSeconds + cage.closeSeconds);
            ObjectHeadPresentation.Impact(skillSettings.skillId, impactPoint, skillSettings.explosionRadiusWorld);
        }
        else if (skillSettings.effectType == SkillEffectType.ChainExplosion)
        {
            if (UsesRollingChainPath())
            {
                yield return ApplyRollingChainExplosionRoutine(impactPoint);
                fadePoint = transform.position;
            }
            else
            {
                yield return ApplyChainExplosionRoutine(impactPoint);
            }
        }
        else if (skillSettings.effectType == SkillEffectType.CreateTerrainCircle)
        {
            yield return CreateTerrainBurstRoutine(impactPoint);
        }
        else
        {
            ObjectHeadPresentation.Impact(skillSettings.skillId, impactPoint, skillSettings.explosionRadiusWorld);
            ApplySkillEffect(impactPoint);
        }

        StartCoroutine(ExplosionFadeRoutine(fadePoint));
    }

    private IEnumerator ApplyHealingSupplyRoutine(Vector2 point)
    {
        var art = ObjectHeadPresentation.Load();
        point=ObjectHeadSkyDrop.Landing(terrain,point);
        Vector2 from=ObjectHeadSkyDrop.Origin(point,art!=null?art.healingSupplySize:1.5f,terrain);
        GameObject supply = null;
        if (art?.healingSupplySprite != null && ObjectHeadNetworkManager.Instance?.IsDedicatedWorker != true)
        {
            supply = new GameObject("HealingSupplyDrop", typeof(SpriteRenderer));
            var renderer = supply.GetComponent<SpriteRenderer>();
            renderer.sprite = art.healingSupplySprite;
            renderer.sortingOrder = 32;
            supply.transform.localScale = Vector3.one * art.healingSupplySize / Mathf.Max(.01f, renderer.sprite.bounds.size.y);
            supply.transform.position=from;
        }
        float duration = art != null ? Mathf.Max(.1f, art.airstrikeFallSeconds) : .45f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (supply != null) supply.transform.position = Vector2.Lerp(from, point,Mathf.Pow(elapsed / duration,1.5f));
            yield return null;
        }
        if (supply != null) Destroy(supply);
        ObjectHeadAreaHealing.Apply(point, skillSettings.explosionRadiusWorld, skillSettings.healing);
        ObjectHeadPresentation.Impact(skillSettings.skillId, point, skillSettings.explosionRadiusWorld);
    }

    private void ApplySkillEffect(Vector2 impactPoint)
    {
        float fallbackHorizontalSign = Mathf.Abs(lastVelocity.x) > 0.001f ? Mathf.Sign(lastVelocity.x) : 0f;

        switch (skillSettings.effectType)
        {
            case SkillEffectType.Captivity:
                ObjectHeadCaptivity.CaptureArea(impactPoint,skillSettings.explosionRadiusWorld);
                break;
            case SkillEffectType.SmokeZone:
                ObjectHeadSmokeZone.Create(impactPoint,skillSettings.explosionRadiusWorld,skillSettings.zoneDurationRounds,turnManager);
                break;
            case SkillEffectType.MagneticPulse:
                ObjectHeadMagneticPulse.Apply(impactPoint,skillSettings.explosionRadiusWorld,
                    skillSettings.maxDamage,skillSettings.knockbackForce,skillSettings.pullsTargets);
                break;
            case SkillEffectType.HealBurst:
                ObjectHeadAreaHealing.Apply(impactPoint,skillSettings.explosionRadiusWorld,skillSettings.healing);
                break;
            case SkillEffectType.CreateTerrainCircle:
                break;
            case SkillEffectType.CreateTerrainBridge:
                CreateTerrainBridge(impactPoint);
                ApplyDamage(impactPoint, fallbackHorizontalSign);
                break;
            case SkillEffectType.CreateHazardZone:
            case SkillEffectType.CreateSlowZone:
                ApplyDamage(impactPoint, fallbackHorizontalSign);
                CreateHazardZone(impactPoint);
                break;
            case SkillEffectType.DelayedExplosion:
            case SkillEffectType.DamageExplosion:
            default:
                DestroyTerrainAtImpact(impactPoint);
                ApplyDamage(impactPoint, fallbackHorizontalSign);
                break;
        }
    }

    private void ApplyDamage(Vector2 impactPoint, float fallbackHorizontalSign)
    {
        DamageSystem.ApplyExplosion(
            impactPoint,
            owner,
            skillSettings.explosionRadiusWorld,
            skillSettings.maxDamage,
            skillSettings.knockbackForce,
            fallbackHorizontalSign);
    }

    private IEnumerator ApplyAirstrikeRoutine(Vector2 target)
    {
        // Warning is not an explosion: impacts are shown only when each bomb actually lands.
        yield return new WaitForSeconds(Mathf.Max(.1f,skillSettings.delaySeconds));
        int count=Mathf.Clamp(skillSettings.chainCount,1,12);
        for(int i=0;i<count;i++)
        {
            Vector2 point=target+Vector2.right*((i-(count-1)*.5f)*skillSettings.chainSpacingWorld);
            var art=ObjectHeadPresentation.Load();
            point=ObjectHeadSkyDrop.Landing(terrain,point);
            var from=ObjectHeadSkyDrop.Origin(point,art!=null?art.airstrikeBombSize:1.5f,terrain);
            GameObject bomb=null;
            if(art?.airstrikeBombSprite!=null && ObjectHeadNetworkManager.Instance?.IsDedicatedWorker!=true)
            {
                bomb=new GameObject("AirstrikeBomb",typeof(SpriteRenderer));var renderer=bomb.GetComponent<SpriteRenderer>();
                renderer.sprite=art.airstrikeBombSprite;renderer.sortingOrder=32;
                bomb.transform.localScale=Vector3.one*(art.airstrikeBombSize/renderer.sprite.bounds.size.y);
                bomb.transform.position=from;
            }
            float duration=art!=null?Mathf.Max(.1f,art.airstrikeFallSeconds):.45f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){if(bomb!=null)bomb.transform.position=Vector2.Lerp(from,point,Mathf.Pow(elapsed/duration,1.5f));yield return null;}
            if(bomb!=null)Destroy(bomb);
            // All targets, including the owner and allies, share the existing explosion rules.
            DestroyTerrainAtImpact(point);
            DamageSystem.ApplyExplosion(point,owner,skillSettings.explosionRadiusWorld,
                skillSettings.maxDamage,skillSettings.knockbackForce);
            ObjectHeadPresentation.Impact(skillSettings.skillId,point,skillSettings.explosionRadiusWorld);
            yield return new WaitForSeconds(Mathf.Max(.05f,skillSettings.chainDelaySeconds));
        }
    }

    private IEnumerator ApplyChainExplosionRoutine(Vector2 impactPoint)
    {
        int count = Mathf.Max(1, skillSettings.chainCount);
        Vector2 spreadDirection = GetHorizontalImpactDirection();
        Vector2 perpendicular = new Vector2(-spreadDirection.y, spreadDirection.x);
        float delay = Mathf.Clamp(skillSettings.chainDelaySeconds, 0.08f, 0.15f);
        Dictionary<CharacterCombat, int> accumulatedDamage = new Dictionary<CharacterCombat, int>();

        for (int i = 0; i < count; i++)
        {
            Vector2 point = GetChainExplosionPoint(
                impactPoint,
                i,
                count,
                spreadDirection,
                perpendicular);

            DestroyTerrainAtImpact(point);
            SpawnExplosionMarker(point);
            ApplyCappedChainDamageAtPoint(point, accumulatedDamage);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private IEnumerator ApplyRollingChainExplosionRoutine(Vector2 impactPoint)
    {
        int count = Mathf.Max(1, skillSettings.chainCount);
        float delay = Mathf.Clamp(skillSettings.chainDelaySeconds, 0.08f, 0.2f);
        Dictionary<CharacterCombat, int> accumulatedDamage = new Dictionary<CharacterCombat, int>();

        ApplyRollingChainMotion();
        for (int i = 0; i < count; i++)
        {
            Vector2 point = i == 0 ? impactPoint : (Vector2)transform.position;
            DestroyTerrainAtImpact(point);
            SpawnExplosionMarker(point);
            ApplyCappedChainDamageAtPoint(point, accumulatedDamage);

            if (i < count - 1)
            {
                ApplyRollingChainMotion();
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private Vector2 GetChainExplosionPoint(
        Vector2 impactPoint,
        int index,
        int count,
        Vector2 spreadDirection,
        Vector2 perpendicular)
    {
        if (!skillSettings.useWideClusterPattern)
        {
            float centeredIndex = index - (count - 1) * 0.5f;
            return impactPoint
                + spreadDirection * (centeredIndex * skillSettings.chainSpacingWorld)
                + perpendicular * (Mathf.Sin(index * 1.7f) * skillSettings.chainSpacingWorld * 0.5f);
        }

        Vector2[] clusterPattern =
        {
            Vector2.zero,
            new Vector2(0f, -0.55f),
            new Vector2(0f, 0.55f),
            new Vector2(0.42f, -0.42f),
            new Vector2(0.42f, 0.42f),
            new Vector2(-0.42f, -0.42f),
            new Vector2(-0.42f, 0.42f),
            new Vector2(0.78f, 0f)
        };

        float spreadRadius = skillSettings.chainSpreadRadiusWorld > 0f
            ? skillSettings.chainSpreadRadiusWorld
            : Mathf.Max(0.35f, skillSettings.chainSpacingWorld * Mathf.Max(1, count - 1) * 0.5f);

        Vector2 offset;
        if (index < clusterPattern.Length)
        {
            offset = clusterPattern[index];
        }
        else
        {
            float angle = (index - clusterPattern.Length) * 2.399963f;
            float radius = Mathf.Lerp(0.58f, 1f, (index % 5) / 4f);
            offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return impactPoint
            + spreadDirection * (offset.x * spreadRadius)
            + perpendicular * (offset.y * spreadRadius);
    }

    private void ApplyCappedChainDamageAtPoint(
        Vector2 center,
        Dictionary<CharacterCombat, int> accumulatedDamage)
    {
        if (skillSettings.maxDamage <= 0 || skillSettings.explosionRadiusWorld <= 0f)
        {
            return;
        }

        int damageCap = Mathf.Max(skillSettings.maxDamage, skillSettings.chainMaxTotalDamage);
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, skillSettings.explosionRadiusWorld);
        HashSet<CharacterCombat> damagedAtThisPoint = new HashSet<CharacterCombat>();

        for (int i = 0; i < hits.Length; i++)
        {
            CharacterCombat combat = hits[i] != null ? hits[i].GetComponentInParent<CharacterCombat>() : null;
            if (!DamageSystem.CanDamage(owner, combat) || !damagedAtThisPoint.Add(combat))
            {
                continue;
            }

            accumulatedDamage.TryGetValue(combat, out int currentDamage);
            int remainingDamage = damageCap - currentDamage;
            if (remainingDamage <= 0)
            {
                continue;
            }

            Vector2 characterCenter = combat.KnockbackCenter;
            float falloff = DamageSystem.CalculateExplosionFalloff(
                hits[i],
                center,
                characterCenter,
                skillSettings.explosionRadiusWorld);
            int damage = Mathf.Min(remainingDamage, Mathf.CeilToInt(skillSettings.maxDamage * falloff));
            if (damage <= 0)
            {
                continue;
            }

            combat.ApplyExplosionKnockback(center, skillSettings.maxDamage);
            combat.TakeDamage(damage);
            accumulatedDamage[combat] = currentDamage + damage;
        }
    }

    private void SpawnExplosionMarker(Vector2 point)
    {
        if (ObjectHeadPresentation.Impact(skillSettings.skillId, point, skillSettings.explosionRadiusWorld)) return;
        GameObject marker = new GameObject("ChainExplosionMarker");
        marker.transform.position = point;
        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = GetCircleSprite();
        renderer.color = skillSettings.impactColor;
        renderer.sortingOrder = 39;
        marker.transform.localScale = Vector3.one * Mathf.Max(0.01f, skillSettings.explosionRadiusWorld * 2f);
        StartCoroutine(FadeAndDestroyMarker(marker, renderer));
    }

    private IEnumerator FadeAndDestroyMarker(GameObject marker, SpriteRenderer renderer)
    {
        float duration = Mathf.Max(0.08f, explosionFadeSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (renderer != null)
            {
                Color color = skillSettings.impactColor;
                color.a *= 1f - elapsed / duration;
                renderer.color = color;
            }

            yield return null;
        }

        if (marker != null)
        {
            Destroy(marker);
        }
    }

    private void DestroyTerrainAtImpact(Vector2 impactPoint)
    {
        TerrainManager terrainManager = GetTerrain();
        if (terrainManager == null)
        {
            return;
        }

        int radiusPx = skillSettings.terrainRadiusPx > 0
            ? skillSettings.terrainRadiusPx
            : Mathf.Max(1, Mathf.RoundToInt(skillSettings.explosionRadiusWorld * terrainManager.PixelsPerUnit));
        terrainManager.DestroyCircle(impactPoint, radiusPx);
    }

    private void CreateTerrainCircle(Vector2 impactPoint)
    {
        TerrainManager terrainManager = GetTerrain();
        if (terrainManager == null)
        {
            return;
        }

        int radiusPx = skillSettings.terrainRadiusPx > 0
            ? skillSettings.terrainRadiusPx
            : Mathf.Max(1, Mathf.RoundToInt(skillSettings.explosionRadiusWorld * terrainManager.PixelsPerUnit));
        terrainManager.CreateCircle(impactPoint, radiusPx, CreatedTerrainType, FindBlockedCharacterColliders());
    }

    private IEnumerator CreateTerrainBurstRoutine(Vector2 impactPoint)
    {
        TerrainManager terrainManager = GetTerrain();
        if (terrainManager == null)
        {
            yield break;
        }

        int count = skillSettings.terrainBurstCount > 0
            ? skillSettings.terrainBurstCount
            : 10;
        int stampRadiusPx = skillSettings.terrainBurstStampRadiusPx > 0
            ? skillSettings.terrainBurstStampRadiusPx
            : Mathf.Max(6, skillSettings.terrainRadiusPx / 2);
        float interval = Mathf.Clamp(
            skillSettings.terrainBurstIntervalSeconds,
            0.04f,
            0.09f);
        float spreadX = Mathf.Max(
            0.2f,
            skillSettings.finalTerrainRadiusXWorld > 0f
                ? skillSettings.finalTerrainRadiusXWorld
                : skillSettings.terrainBurstSpreadWorld);
        float spreadY = Mathf.Max(
            0.5f,
            skillSettings.finalTerrainRadiusYWorld > 0f
                ? skillSettings.finalTerrainRadiusYWorld
                : spreadX * 0.8f);
        int maxAttemptsPerStamp = Mathf.Max(1, skillSettings.terrainBurstMaxPlacementAttemptsPerStamp);
        int seed = TerrainGrowthSeedUtility.Build(
            owner,
            turnManager,
            skillSettings.skillId,
            skillSettings.commonHeadTypeId,
            impactPoint);
        System.Random random = new System.Random(seed);
        Bounds terrainBounds = terrainManager.GetTerrainBounds();
        Collider2D[] blockedColliders = FindBlockedCharacterColliders();
        int clippedStamps = 0;
        int buriedStamps = 0;
        int placedStamps = 0;
        int stampsSinceRebuild = 0;
        float lastRebuildTime = Time.time;

        Debug.Log(
            $"Terrain burst seed={seed}, count={count}, stampRadiusPx={stampRadiusPx}, " +
            $"spread=({spreadX:0.##}, {spreadY:0.##}), maxAttempts={maxAttemptsPerStamp}.");

        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttemptsPerStamp; attempt++)
            {
                Vector2 normalizedOffset = GetBurstPatternOffset(i, count, attempt, random);
                Vector2 point = impactPoint + new Vector2(
                    normalizedOffset.x * spreadX,
                    normalizedOffset.y * spreadY + skillSettings.terrainBurstVerticalBiasWorld);
                point.y = Mathf.Min(
                    point.y,
                    impactPoint.y + Mathf.Max(0.5f, skillSettings.maxBuildHeightAboveSurfaceWorld));

                if (!terrainBounds.Contains(point))
                {
                    clippedStamps++;
                    continue;
                }

                if (terrainManager.CreateCircleDeferred(
                    point,
                    stampRadiusPx,
                    CreatedTerrainType,
                    blockedColliders))
                {
                    placed = true;
                    placedStamps++;
                    stampsSinceRebuild++;
                    SpawnGrowthMarker(point);
                    break;
                }
            }

            if (!placed)
            {
                buriedStamps++;
            }

            if (stampsSinceRebuild >= 2 || Time.time - lastRebuildTime >= 0.1f)
            {
                terrainManager.FlushDeferredTerrainChanges();
                stampsSinceRebuild = 0;
                lastRebuildTime = Time.time;
            }

            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        terrainManager.FlushDeferredTerrainChanges();
        if (clippedStamps * 2 >= count)
        {
            Debug.LogWarning(
                $"Terrain burst at {impactPoint} was heavily clipped by texture bounds " +
                $"({clippedStamps}/{count} stamps).");
        }

        if (buriedStamps > 0)
        {
            Debug.Log(
                $"Terrain burst placed {placedStamps}/{count} stamps; " +
                $"{buriedStamps} stamps were fully buried or blocked after retries.");
        }

        float fallbackHorizontalSign = Mathf.Abs(launchDirection.x) > 0.001f
            ? Mathf.Sign(launchDirection.x)
            : 0f;
        ApplyDamage(impactPoint, fallbackHorizontalSign);
    }

    private static Vector2 GetBurstPatternOffset(int index, int count, int attempt, System.Random random)
    {
        Vector2[] foundation =
        {
            Vector2.zero,
            new Vector2(-0.55f, 0f),
            new Vector2(0.55f, 0f),
            new Vector2(0f, 0.55f),
            new Vector2(0f, -0.55f),
            new Vector2(-0.45f, 0.45f),
            new Vector2(0.45f, 0.45f),
            new Vector2(-0.45f, -0.45f),
            new Vector2(0.45f, -0.45f)
        };

        if (attempt == 0 && index < foundation.Length)
        {
            return foundation[index];
        }

        float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
        float radius = Mathf.Lerp(0.15f, 0.92f, Mathf.Sqrt((float)random.NextDouble()));
        float x = Mathf.Cos(angle) * radius;
        float y = Mathf.Sin(angle) * radius;
        return new Vector2(x, y);
    }

    private void SpawnGrowthMarker(Vector2 point)
    {
        if (ObjectHeadPresentation.Impact(skillSettings.skillId, point, .25f)) return;
        GameObject marker = new GameObject("TerrainGrowthMarker");
        marker.transform.position = point;
        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = GetCircleSprite();
        renderer.color = new Color(0.35f, 1f, 0.35f, 0.65f);
        renderer.sortingOrder = 39;
        marker.transform.localScale = Vector3.one * 0.22f;
        StartCoroutine(FadeAndDestroyMarker(marker, renderer));
    }

    private void CreateTerrainBridge(Vector2 impactPoint)
    {
        TerrainManager terrainManager = GetTerrain();
        if (terrainManager == null)
        {
            return;
        }

        Vector2 direction = launchDirection.sqrMagnitude > 0.0001f
            ? launchDirection.normalized
            : Vector2.right;
        float length = Mathf.Max(0.5f, skillSettings.bridgeLengthWorld);
        int thicknessPx = Mathf.Max(1, skillSettings.bridgeThicknessPx);
        terrainManager.CreateBridge(impactPoint, direction, length, thicknessPx);
    }

    private void CreateHazardZone(Vector2 impactPoint)
    {
        if (skillSettings.zoneDurationRounds <= 0 ||
            skillSettings.zoneLengthWorld <= 0f ||
            (skillSettings.zoneDamagePerTurn <= 0 && skillSettings.slowMultiplier >= 1f))
        {
            return;
        }

        var zone=GroundHazardZone.Create(
            impactPoint,
            skillSettings.zoneLengthWorld,
            skillSettings.zoneThicknessWorld,
            skillSettings.zoneDurationRounds,
            skillSettings.zoneDamagePerTurn,
            skillSettings.slowMultiplier,
            skillSettings.impactColor,
            owner,
            turnManager);
        zone.SetArtwork(ObjectHeadPresentation.Load()?.Find(skillSettings.skillId)?.zoneSprite);
    }

    private TerrainType CreatedTerrainType=>skillSettings.commonHeadTypeId==(int)CommonHeadType.TerrainCreation?TerrainType.Cloud:TerrainType.Created;

    private Collider2D[] FindBlockedCharacterColliders()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        CharacterCombat[] combats = Object.FindObjectsByType<CharacterCombat>(FindObjectsSortMode.None);
#else
        CharacterCombat[] combats = Object.FindObjectsOfType<CharacterCombat>();
#endif
        Collider2D[] colliders = new Collider2D[combats.Length];
        for (int i = 0; i < combats.Length; i++)
        {
            colliders[i] = combats[i] != null ? combats[i].GetComponent<Collider2D>() : null;
        }

        return colliders;
    }

    private Vector2 GetHorizontalImpactDirection()
    {
        float sign = Mathf.Abs(lastVelocity.x) > 0.001f ? Mathf.Sign(lastVelocity.x) : 1f;
        return new Vector2(sign, 0.15f).normalized;
    }

    private bool UsesRollingChainPath()
    {
        return skillSettings.effectType == SkillEffectType.ChainExplosion &&
               skillSettings.useRollingChainPath;
    }

    private void ApplyRollingChainMotion()
    {
        if (body == null || !body.simulated)
        {
            return;
        }

        float sign = GetRollingDirectionSign();
        float minSpeed = Mathf.Max(0f, skillSettings.rollingChainMinSpeed);
        if (minSpeed > 0f)
        {
            Vector2 velocity = body.linearVelocity;
            if (Mathf.Abs(velocity.x) < minSpeed)
            {
                velocity.x = sign * minSpeed;
                body.linearVelocity = velocity;
            }
        }

        float angularSpeed = Mathf.Max(0f, skillSettings.rollingChainAngularSpeed);
        if (angularSpeed > 0f)
        {
            body.angularVelocity = -sign * angularSpeed;
        }
    }

    private float GetRollingDirectionSign()
    {
        if (Mathf.Abs(lastVelocity.x) > 0.001f)
        {
            return Mathf.Sign(lastVelocity.x);
        }

        if (Mathf.Abs(launchDirection.x) > 0.001f)
        {
            return Mathf.Sign(launchDirection.x);
        }

        return 1f;
    }

    private void CompleteTurn()
    {
        if (!isCompleted)
        {
            isCompleted = true;
        }

        if (!completionNotified)
        {
            completionNotified = true;
            Resolved?.Invoke();
        }

        if (turnManager != null)
        {
            turnManager.NotifyActionResolved();
        }
    }

    private bool TryResolveCameraExit()
    {
        if (!isFlying)
        {
            return false;
        }

        if (cameraController == null)
        {
            cameraController = FindCameraController();
        }

        if (cameraController == null ||
            !cameraController.IsOutsideReachableView(transform.position, cameraExitCullMarginWorld))
        {
            return false;
        }

        ResolveCameraExit();
        return true;
    }

    private void ResolveCameraExit()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        isFlying = false;
        DisablePhysicsAtImpact(transform.position);
        CompleteTurn();

        if (cameraController == null)
        {
            cameraController = FindCameraController();
        }

        cameraController?.FocusOnCurrentCharacterImmediate(false);
        Destroy(gameObject);
    }

    private IEnumerator ExplosionFadeRoutine(Vector2 impactPoint)
    {
        DisablePhysicsAtImpact(impactPoint);

        // Authored effects replace the old opaque circle, without changing resolution timing.
        if (ObjectHeadPresentation.Load()?.Find(skillSettings.skillId)?.impactPrefab != null)
        {
            GetComponent<SpriteRenderer>().enabled = false;
            CompleteTurn();
            yield return new WaitForSeconds(Mathf.Max(.01f, explosionFadeSeconds));
            Destroy(gameObject);
            yield break;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        transform.position = impactPoint;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, skillSettings.explosionRadiusWorld * 2f);

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = GetCircleSprite();
            spriteRenderer.sortingOrder = 40;
        }

        CompleteTurn();

        float duration = Mathf.Max(0.01f, explosionFadeSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            SetExplosionAlpha(spriteRenderer, 1f - elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void DisablePhysicsAtImpact(Vector2 impactPoint)
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Collider2D projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        transform.position = impactPoint;
    }

    private void ApplyProjectileVisual(SpriteRenderer spriteRenderer, float radius)
    {
        Sprite sprite = skillSettings.headSprite != null ? skillSettings.headSprite : GetCircleSprite();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = skillSettings.projectileColor;
        spriteRenderer.sortingOrder = 30;

        float visualDiameter = skillSettings.projectileVisualDiameter > 0f
            ? skillSettings.projectileVisualDiameter
            : Mathf.Max(0.01f, radius * 2f);
        float spriteDiameter = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, visualDiameter / Mathf.Max(0.01f, spriteDiameter));
    }

    private float CalculateFlightSpinVelocity(Vector2 velocity)
    {
        if (flightSpinDegreesPerSecond <= 0f)
        {
            return 0f;
        }

        if (Mathf.Abs(velocity.x) > 0.001f)
        {
            return -Mathf.Sign(velocity.x) * flightSpinDegreesPerSecond;
        }

        if (Mathf.Abs(launchDirection.x) > 0.001f)
        {
            return -Mathf.Sign(launchDirection.x) * flightSpinDegreesPerSecond;
        }

        return -flightSpinDegreesPerSecond;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((CircleTextureSize - 1) * 0.5f, (CircleTextureSize - 1) * 0.5f);
        float radius = CircleTextureSize * 0.48f;

        for (int y = 0; y < CircleTextureSize; y++)
        {
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
            new Vector2(0.5f, 0.5f),
            CircleTextureSize);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    private void SetExplosionAlpha(SpriteRenderer spriteRenderer, float alphaMultiplier)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = skillSettings.impactColor;
        color.a *= Mathf.Clamp01(alphaMultiplier);
        spriteRenderer.color = color;
    }

    private TerrainManager GetTerrain()
    {
        if (terrain == null)
        {
            terrain = FindTerrainManager();
        }

        return terrain;
    }

    private TerrainManager FindTerrainManager()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<TerrainManager>();
#else
        return Object.FindObjectOfType<TerrainManager>();
#endif
    }

    private ObjectHeadCameraController FindCameraController()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<ObjectHeadCameraController>();
#else
        return Object.FindObjectOfType<ObjectHeadCameraController>();
#endif
    }
}

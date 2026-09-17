using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Original bounded artillery search. Never changes physics, HP, RNG or terrain while planning.</summary>
public sealed class ObjectHeadAIPlanner
{
    public struct Shot
    {
        public bool valid;
        public int skill;
        public Vector2 direction, impact;
        public float power, score;
        public ObjectHeadSkillSettings settings;
        public string gourdChoice;
    }
    public Shot Best { get; private set; }
    public int Evaluated { get; private set; }
    private readonly RaycastHit2D[] hits = new RaycastHit2D[48];
    private readonly TurnCharacterController actor;
    private readonly IReadOnlyList<TurnCharacterController> characters;
    private readonly TerrainManager terrain;
    private readonly ObjectHeadAITuning tuning;
    private readonly SkillFireController fire;
    private readonly DemoSkillSelector selector;
    private readonly AimController aim;
    private readonly ContactFilter2D filter;
    private readonly Vector2 originShift;
    private Vector2 PlanningOrigin => aim.AimOrigin + originShift;

    public ObjectHeadAIPlanner(TurnCharacterController actor, IReadOnlyList<TurnCharacterController> characters, TerrainManager terrain,Vector2? planningPosition=null)
    {
        this.actor = actor; this.characters = characters; this.terrain = terrain;
        tuning = ObjectHeadAITuning.Load(); fire = actor.GetComponent<SkillFireController>();
        selector = actor.GetComponent<DemoSkillSelector>(); aim = actor.GetComponent<AimController>();
        originShift=planningPosition.HasValue?planningPosition.Value-(Vector2)actor.transform.position:Vector2.zero;
        filter = new ContactFilter2D { useTriggers = false };
    }

    public IEnumerator Search(ObjectHeadAIDifficulty difficulty, Func<bool> canContinue,float budgetOverride=-1)
    {
        Best = default; Evaluated = 0;
        float deadline = Time.realtimeSinceStartup + (budgetOverride>0?budgetOverride:tuning.planningBudgetSeconds);
        int samples = tuning.PowerSamples(difficulty);
        var candidates=new List<Shot>();
        for(int index=0;index<3;index++)
        {
            if(selector.GetRemainingCooldown(index)>0)continue;
            var effect=selector.GetSkillSettings(index);
            var gourd=actor.GetComponent<ObjectHeadGourd>();
            if(effect.effectType==SkillEffectType.GourdSelect && gourd!=null)
            {
                foreach(var choice in gourd.Choices.Where(c=>gourd.Remaining(c)!=0))
                {
                    var common=choice.common!=CommonHeadType.None?ObjectHeadContent.Load().Common(choice.common):null;
                    if(choice.source==null && common?.skill==null)continue;
                    candidates.Add(new Shot{skill=index,settings=choice.source!=null?choice.Resolve():common.skill.Resolve(null),gourdChoice=choice.id});
                }
            }
            else if(effect.effectType==SkillEffectType.GourdRefill)
            {
                if(gourd!=null && gourd.Choices.Count(c=>gourd.Remaining(c)==0)>3)
                    Best=new Shot{valid=true,skill=index,settings=effect,direction=Vector2.up,power=1,score=8};
            }
            else if(effect.effectType==SkillEffectType.GourdRandom)
            {
                // Never inspect a future random outcome. It is only a last-resort gamble.
                var enemy=characters.FirstOrDefault(c=>c!=null && !SameTeam(actor,c) && !c.GetComponent<CharacterCombat>().IsDead);
                if(enemy!=null)Best=new Shot{valid=true,skill=index,settings=effect,direction=((Vector2)(enemy.transform.position-actor.transform.position)+Vector2.up*3).normalized,power=.7f,score=.6f};
            }
            else candidates.Add(new Shot{skill=index,settings=effect});
        }
        foreach(var candidateShot in candidates)
        {
            int skill=candidateShot.skill;
            if (selector.GetRemainingCooldown(skill) > 0) continue;
            var settings = candidateShot.settings;
            if(settings.effectType==SkillEffectType.Hover)continue; // Mobility needs a safe destination, not an artillery arc.
            if(settings.effectType==SkillEffectType.Airflow)
            {
                var vacuum=settings.vacuum;if(vacuum==null)continue;
                foreach(var enemy in characters)
                {
                    if(enemy==null || SameTeam(actor,enemy) || enemy.GetComponent<CharacterCombat>()?.IsDead!=false)continue;
                    var point=enemy.GetComponent<CharacterCombat>().KnockbackCenter;var direction=(point-PlanningOrigin).normalized;
                    float score=0;
                    foreach(var candidate in characters)
                    {
                        if(candidate==null || candidate==actor || candidate.GetComponent<CharacterCombat>()?.IsDead!=false)continue;
                        var center=candidate.GetComponent<CharacterCombat>().KnockbackCenter;
                        if(ObjectHeadVacuum.InBeam(PlanningOrigin,direction,center,vacuum.Range(1),vacuum.beamWidth) && ObjectHeadVacuum.Clear(terrain,PlanningOrigin,center))
                            score+=SameTeam(actor,candidate)?-vacuum.Force(1):vacuum.Force(1);
                    }
                    score-=selector.GetCooldownDuration(skill)*tuning.cooldownCost;
                    if(score>tuning.minimumShotUtility && (!Best.valid || score>Best.score))
                        Best=new Shot{valid=true,skill=skill,direction=direction,power=1,impact=point,score=score,settings=settings,gourdChoice=candidateShot.gourdChoice};
                }
                continue;
            }
            // Bridge construction requires path planning, not ballistic firing.
            if (settings.effectType == SkillEffectType.CreateTerrainBridge) continue;
            foreach (var target in characters)
            {
                if (target == null || target.GetComponent<CharacterCombat>()?.IsDead != false) continue;
                bool friendly = SameTeam(actor, target);
                bool healing = settings.effectType == SkillEffectType.HealBurst;
                if (healing ? !friendly : friendly) continue;
                if (healing && target.GetComponent<CharacterCombat>().CurrentHp >= target.GetComponent<CharacterCombat>().MaxHp) continue;
                Vector2 targetPoint = target.GetComponent<Collider2D>()?.bounds.center ?? target.transform.position;
                for (int p = 0; p < (settings.straightShot ? 1 : samples); p++)
                {
                    float power = settings.straightShot ? 1f : Mathf.Lerp(tuning.minimumPower, 1f, p / (float)(samples - 1));
                    for (int arc = 0; arc < (settings.straightShot ? 1 : 2); arc++)
                    {
                        if (!canContinue()) yield break;
                        Vector2 direction = (targetPoint - PlanningOrigin).normalized;
                        bool reachable = true;
                        float speed = settings.straightShot ? settings.straightSpeed : fire.LaunchSpeed * power;
                        // Account for the spawn offset, rather than aiming from the character centre.
                        for (int refine = 0; refine < 3; refine++)
                        {
                            Vector2 delta = targetPoint - (PlanningOrigin + direction * fire.LaunchOffset);
                            if (settings.straightShot) direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
                            else if (!SolveArc(delta, speed, -Physics2D.gravity.y * fire.ProjectileGravity, arc == 1, out direction))
                            { reachable = false; break; }
                        }
                        if (reachable && Trace(settings, direction, power, out Vector2 impact))
                        {
                            float score = Score(settings, impact) - selector.GetCooldownDuration(skill) * tuning.cooldownCost;
                            if (score > tuning.minimumShotUtility && (!Best.valid || score > Best.score))
                                Best = new Shot { valid = true, skill = skill, direction = direction, power = power,
                                    impact = impact, score = score, settings = settings,gourdChoice=candidateShot.gourdChoice };
                        }
                        Evaluated++;
                        if (Evaluated % Mathf.Max(1, tuning.candidatesPerFrame) == 0)
                        {
                            yield return null;
                            if (Time.realtimeSinceStartup >= deadline) yield break;
                        }
                    }
                }
            }
        }
    }

    public static bool SameTeam(TurnCharacterController a, TurnCharacterController b) =>
        a.GetComponent<ObjectHeadTeamMember>()?.AllianceId == b.GetComponent<ObjectHeadTeamMember>()?.AllianceId;

    public static bool SolveArc(Vector2 delta, float speed, float gravity, bool high, out Vector2 direction)
    {
        direction = Vector2.right;
        if (speed <= .001f) return false;
        if (gravity <= .001f) { direction = delta.normalized; return true; }
        float x = Mathf.Abs(delta.x), v2 = speed * speed;
        if (x < .001f)
        {
            if (delta.y > v2 / (2 * gravity)) return false;
            direction = delta.y >= 0 || high ? Vector2.up : Vector2.down; return true;
        }
        float discriminant = v2 * v2 - gravity * (gravity * x * x + 2 * delta.y * v2);
        if (discriminant < 0) return false;
        float tangent = (v2 + (high ? 1 : -1) * Mathf.Sqrt(discriminant)) / (gravity * x);
        direction = new Vector2(Mathf.Sign(delta.x), tangent).normalized;
        return true;
    }

    public bool Trace(ObjectHeadSkillSettings settings, Vector2 direction, float power, out Vector2 impact)
    {
        Vector2 position = PlanningOrigin + direction.normalized * fire.LaunchOffset;
        Vector2 velocity = direction.normalized * (settings.straightShot ? settings.straightSpeed : fire.LaunchSpeed * power);
        Vector2 gravity = settings.straightShot ? Vector2.zero : Physics2D.gravity * fire.ProjectileGravity;
        float step = Mathf.Clamp(tuning.trajectoryStep, .01f, .1f);
        float spriteSize=settings.headSprite!=null?Mathf.Max(settings.headSprite.bounds.size.x,settings.headSprite.bounds.size.y):1f;
        float radius = .5f*Mathf.Max(.01f,settings.projectileVisualDiameter/Mathf.Max(.01f,spriteSize));
        for (float time = 0; time < Mathf.Min(fire.ProjectileLifetime, tuning.trajectorySeconds); time += step)
        {
            Vector2 next = position + velocity * step + gravity * (.5f * step * step);
            velocity += gravity * step;
            Vector2 segment = next - position;
            float nearest = segment.magnitude + 1f;
            Vector2 point = next;
            int count = Physics2D.CircleCast(position, radius, segment.normalized, filter, hits, segment.magnitude);
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(actor.transform) ||
                    collider.GetComponentInParent<CommonHeadItem>() != null || collider.GetComponentInParent<SkillProjectile>() != null) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; point = hits[i].point; }
            }
            if (terrain != null && terrain.TryCheckTerrainHit(position, next, out TerrainHit hit))
            {
                float distance = Vector2.Distance(position, hit.point);
                if (distance < nearest) { nearest = distance; point = hit.point; }
            }
            if (nearest <= segment.magnitude) { impact = point; return true; }
            position = next;
        }
        impact = position;
        return false; // No impact: do not award damage for an imaginary landing point.
    }

    public float Score(ObjectHeadSkillSettings settings, Vector2 impact)
    {
        float score = 0;
        foreach (var target in characters)
        {
            if (target == null) continue;
            var combat = target.GetComponent<CharacterCombat>();
            if (combat == null || combat.IsDead || ObjectHeadCaptivity.Captured(target)) continue;
            var collider = target.GetComponent<Collider2D>();
            Vector2 shift=target==actor?originShift:Vector2.zero;
            float distance = Vector2.Distance(impact, collider != null ? collider.ClosestPoint(impact-shift)+shift : (Vector2)target.transform.position+shift);
            float radius = Mathf.Max(.05f, settings.explosionRadiusWorld);
            float falloff = Mathf.Clamp01(1f - distance / radius);
            if(settings.effectType==SkillEffectType.RainbowSweep)falloff=Mathf.Abs(combat.KnockbackCenter.y-impact.y)<=radius?1:0;
            bool friendly = SameTeam(actor, target);
            float friendlyPenalty = target == actor ? tuning.selfDamagePenalty : tuning.friendlyFirePenalty;
            if (settings.effectType == SkillEffectType.HealBurst)
            {
                if (distance <= radius) score += Mathf.Min(settings.healing, combat.MaxHp - combat.CurrentHp) *
                    tuning.healingWeight * (friendly ? 1f : -tuning.friendlyFirePenalty);
                continue;
            }
            float damage = settings.maxDamage * falloff;
            if (settings.effectType == SkillEffectType.Airstrike || settings.effectType == SkillEffectType.ChainExplosion)
                damage *= Mathf.Min(2, Mathf.Max(1, settings.chainCount));
            float utility = Mathf.Min(combat.CurrentHp, damage);
            if (damage >= combat.CurrentHp) utility += tuning.killBonus;
            if (settings.effectType == SkillEffectType.CreateHazardZone || settings.effectType == SkillEffectType.CreateSlowZone)
            {
                if (Mathf.Abs(target.transform.position.x - impact.x) <= settings.zoneLengthWorld * .5f &&
                    Mathf.Abs(target.transform.position.y - impact.y) <= radius)
                    utility += settings.zoneDamagePerTurn * tuning.zoneWeight;
            }
            if (distance <= radius && (settings.effectType == SkillEffectType.MagneticPulse ||
                settings.effectType == SkillEffectType.Captivity || settings.effectType == SkillEffectType.CreateTerrainCircle))
                utility += tuning.controlWeight;
            score += utility * (friendly ? -friendlyPenalty : 1f);
        }
        return score;
    }
}

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
        public int commonSlot;
        public CommonHeadType commonType;
        public bool opensObstruction;
        public Vector2 progressTarget;
    }
    public Shot Best { get; private set; }
    public int Evaluated { get; private set; }
    private readonly RaycastHit2D[] hits = new RaycastHit2D[48];
    private readonly Collider2D[] launchOverlaps = new Collider2D[48];
    private readonly TurnCharacterController actor;
    private readonly IReadOnlyList<TurnCharacterController> characters;
    private readonly TerrainManager terrain;
    private readonly ObjectHeadAITuning tuning;
    private readonly SkillFireController fire;
    private readonly DemoSkillSelector selector;
    private readonly AimController aim;
    private readonly CommonHeadUseController commonUse;
    private readonly CommonHeadInventory inventory;
    private readonly ContactFilter2D filter;
    private readonly Vector2 originShift;
    private Vector2 PlanningOrigin => aim.AimOrigin + originShift;

    public ObjectHeadAIPlanner(TurnCharacterController actor, IReadOnlyList<TurnCharacterController> characters, TerrainManager terrain,Vector2? planningPosition=null)
    {
        this.actor = actor; this.characters = characters; this.terrain = terrain;
        tuning = ObjectHeadAITuning.Load(); fire = actor.GetComponent<SkillFireController>();
        selector = actor.GetComponent<DemoSkillSelector>(); aim = actor.GetComponent<AimController>();
        commonUse = actor.GetComponent<CommonHeadUseController>();
        inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventoryManager>()?.GetInventoryFor(actor.gameObject);
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
        if(commonUse!=null && inventory!=null)
        {
            var catalog=ObjectHeadContent.Load();
            int commonCandidateCount=0;
            for(int slot=0;slot<CommonHeadInventory.SlotCount;slot++)
            {
                CommonHeadType type=inventory.GetSlot(slot);
                var definition=type!=CommonHeadType.None?catalog?.Common(type):null;
                if(definition==null || definition.use!=ObjectHeadCommonUse.Projectile || definition.skill==null)continue;
                var settings=definition.skill.Resolve(null);
                settings.headSprite=definition.sprite;
                // Interleave inventory with character skills so a time budget cannot
                // always starve the common heads at the end of the catalogue.
                candidates.Insert(Mathf.Min(commonCandidateCount*2+1,candidates.Count),
                    new Shot{commonSlot=slot,commonType=type,settings=settings});
                commonCandidateCount++;
            }
        }
        foreach(var candidateShot in candidates)
        {
            int skill=candidateShot.skill;
            if (candidateShot.commonType==CommonHeadType.None && selector.GetRemainingCooldown(skill) > 0) continue;
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
                    if(candidateShot.commonType==CommonHeadType.None)score-=selector.GetCooldownDuration(skill)*tuning.cooldownCost;
                    if(score>tuning.minimumShotUtility && (!Best.valid || score>Best.score))
                        Best=new Shot{valid=true,skill=skill,direction=direction,power=1,impact=point,score=score,settings=settings,gourdChoice=candidateShot.gourdChoice,commonSlot=candidateShot.commonSlot,commonType=candidateShot.commonType};
                }
                continue;
            }
            // Bridge construction requires path planning, not ballistic firing.
            if (settings.effectType == SkillEffectType.CreateTerrainBridge) continue;
            bool healing = settings.effectType == SkillEffectType.HealBurst;
            var targets=characters.Where(c=>c!=null && c.GetComponent<CharacterCombat>()?.IsDead==false &&
                (healing?SameTeam(actor,c):!SameTeam(actor,c)) &&
                (!healing || c.GetComponent<CharacterCombat>().CurrentHp<c.GetComponent<CharacterCombat>().MaxHp))
                .OrderBy(c=>Vector2.Distance(PlanningOrigin,c.transform.position)).ToArray();
            bool straight=settings.straightShot && candidateShot.commonType==CommonHeadType.None;
            // Round-robin targets before spending the remaining power samples on
            // one enemy. The old 18-check cap spent 14 on the nearest enemy and
            // offered the next enemy only two usually-unreachable low powers.
            foreach(var sample in TrajectorySamples(targets.Length,samples,straight,tuning.checksPerHead))
            {
                float power = straight ? 1f : SearchPower(sample.powerIndex,samples,tuning.minimumPower);
                int arc=sample.arc;
                var target=targets[sample.targetIndex];
                if (!canContinue()) yield break;
                Vector2 targetPoint = target.GetComponent<Collider2D>()?.bounds.center ?? target.transform.position;
                Vector2 direction = (targetPoint - PlanningOrigin).normalized;
                bool reachable = true;
                float speed = candidateShot.commonType!=CommonHeadType.None ? commonUse.AIThrowSpeed*power :
                    settings.straightShot ? settings.straightSpeed : fire.LaunchSpeed * power;
                // Account for the spawn offset, rather than aiming from the character centre.
                for (int refine = 0; refine < 3; refine++)
                {
                    Vector2 delta = targetPoint - (PlanningOrigin + direction * (candidateShot.commonType!=CommonHeadType.None?commonUse.AISpawnDistance:fire.LaunchOffset));
                    if (settings.straightShot && candidateShot.commonType==CommonHeadType.None) direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
                    else if (!SolveArc(delta, speed, -Physics2D.gravity.y * (candidateShot.commonType!=CommonHeadType.None?commonUse.AIProjectileGravity:fire.ProjectileGravity), arc == 1, out direction))
                    { reachable = false; break; }
                }
                if (reachable && Trace(settings, direction, power, out Vector2 impact,candidateShot.commonType))
                {
                    float score = Score(settings, impact) - (candidateShot.commonType==CommonHeadType.None?selector.GetCooldownDuration(skill)*tuning.cooldownCost:0);
                    if (score > tuning.minimumShotUtility && (!Best.valid || score > Best.score))
                        Best = new Shot { valid = true, skill = skill, direction = direction, power = power,
                            impact = impact, score = score, settings = settings,gourdChoice=candidateShot.gourdChoice,
                            commonSlot=candidateShot.commonSlot,commonType=candidateShot.commonType };
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

    // A full 4-player FFA has nine enemies. Keep high/medium/low power coverage
    // for each target; the outer real-time deadline still caps total planning.
    public static int HeadCheckBudget(int configured,int targetCount)=>Mathf.Max(configured,Mathf.Max(0,targetCount)*6);

    public struct TrajectorySample { public int powerIndex,arc,targetIndex; }
    public static IEnumerable<TrajectorySample> TrajectorySamples(int targetCount,int powerCount,bool straight,int configuredBudget)
    {
        int remaining=HeadCheckBudget(configuredBudget,targetCount);
        for(int power=0;power<(straight?1:powerCount);power++)
        for(int arc=0;arc<(straight?1:2);arc++)
        for(int target=0;target<targetCount;target++)
        {
            if(remaining--<=0)yield break;
            yield return new TrajectorySample{powerIndex=power,arc=arc,targetIndex=target};
        }
    }

    public static float SearchPower(int sample,int count,float minimum)
    {
        count=Mathf.Max(3,count);sample=Mathf.Clamp(sample,0,count-1);
        int middle=(count-1)/2;
        int index=sample==0?count-1:sample==1?middle:0;
        if(sample>=3)
        {
            int remaining=sample-3;
            for(int candidate=count-2;candidate>0;candidate--)
            {
                if(candidate==middle)continue;
                if(remaining--==0){index=candidate;break;}
            }
        }
        // Even a short budget visits long, medium and short throws for each enemy.
        return Mathf.Lerp(minimum,1f,index/(float)(count-1));
    }

    /// <summary>Last resort only: spend a real destructive skill to open a blocked line.
    /// Never accepts an empty miss, terrain creation, or an explosion near friendly bodies/support.</summary>
    public IEnumerator SearchObstruction(Func<bool> canContinue)
    {
        Best=default; Evaluated=0;
        if(terrain==null)yield break;
        var candidates=new List<Shot>();
        for(int skill=0;skill<3;skill++)
        {
            if(selector.GetRemainingCooldown(skill)>0)continue;
            var settings=selector.GetSkillSettings(skill);
            if(DestructiveProgressRadius(settings,terrain.PixelsPerUnit)>0)
                candidates.Add(new Shot{skill=skill,settings=settings});
        }
        var catalog=ObjectHeadContent.Load();
        if(commonUse!=null && inventory!=null)
            for(int slot=0;slot<CommonHeadInventory.SlotCount;slot++)
            {
                var type=inventory.GetSlot(slot); var definition=catalog?.Common(type);
                if(type==CommonHeadType.None || definition?.skill==null || definition.use!=ObjectHeadCommonUse.Projectile)continue;
                var settings=definition.skill.Resolve(null); settings.headSprite=definition.sprite;
                if(DestructiveProgressRadius(settings,terrain.PixelsPerUnit)>0)
                    candidates.Add(new Shot{commonSlot=slot,commonType=type,settings=settings});
            }
        var enemies=characters.Where(c=>c!=null && c.GetComponent<CharacterCombat>()?.IsDead==false && !SameTeam(actor,c))
            .OrderBy(c=>Vector2.Distance(PlanningOrigin,c.transform.position)).ToArray();
        float deadline=Time.realtimeSinceStartup+Mathf.Min(.6f,tuning.planningBudgetSeconds);
        foreach(var candidate in candidates)
        foreach(var enemy in enemies)
        {
            Vector2 target=enemy.GetComponent<Collider2D>()?.bounds.center??enemy.transform.position;
            bool blocked=terrain.TryCheckTerrainHit(PlanningOrigin,target,out var obstacle);
            // Enemy-directed arcs may encounter a roof or distant wall that is not
            // on the straight sightline. Trace their actual first contact as well.
            for(int aimIndex=0;aimIndex<(blocked?2:1);aimIndex++)
            {
            Vector2 aimPoint=aimIndex==0?target:obstacle.point+(target-PlanningOrigin).normalized/terrain.PixelsPerUnit;
            for(int p=0;p<5;p++)for(int arc=0;arc<2;arc++)
            {
                if(!canContinue() || Evaluated>=120 || Time.realtimeSinceStartup>=deadline)yield break;
                float power=SearchPower(p,5,tuning.minimumPower);
                var settings=candidate.settings;
                bool common=candidate.commonType!=CommonHeadType.None;
                float speed=common?commonUse.AIThrowSpeed*power:settings.straightShot?settings.straightSpeed:fire.LaunchSpeed*power;
                Vector2 direction=(aimPoint-PlanningOrigin).normalized;
                bool reachable=true;
                for(int refine=0;refine<3;refine++)
                {
                    Vector2 delta=aimPoint-(PlanningOrigin+direction*(common?commonUse.AISpawnDistance:fire.LaunchOffset));
                    if(settings.straightShot && !common)direction=delta.normalized;
                    else if(!SolveArc(delta,speed,-Physics2D.gravity.y*(common?commonUse.AIProjectileGravity:fire.ProjectileGravity),arc==1,out direction))
                    {reachable=false;break;}
                }
                if(reachable && Trace(settings,direction,power,out var impact,candidate.commonType))
                {
                    float score=ScoreObstructionProgress(settings,impact,target);
                    if(score>0 && (!Best.valid || score>Best.score))
                    {
                        var shot=candidate;shot.valid=true;shot.direction=direction;shot.power=power;
                        shot.impact=impact;shot.score=score;shot.opensObstruction=true;shot.progressTarget=target;Best=shot;
                    }
                }
                Evaluated++;
                if(Evaluated%Mathf.Max(1,tuning.candidatesPerFrame)==0)yield return null;
            }
            }
        }
    }

    public float ScoreObstructionProgress(ObjectHeadSkillSettings settings,Vector2 impact,Vector2 target)
    {
        if(terrain==null)return 0;
        float radius=DestructiveProgressRadius(settings,terrain.PixelsPerUnit);
        if(radius<=0)return 0;
        float danger=Mathf.Max(settings.explosionRadiusWorld,DestructionRadius(settings,terrain.PixelsPerUnit))+
            ChainReach(settings)+.35f;
        foreach(var friendly in characters)
        {
            if(friendly==null || !SameTeam(actor,friendly) || friendly.GetComponent<CharacterCombat>()?.IsDead!=false)continue;
            var collider=friendly.GetComponent<Collider2D>();
            Vector2 point=collider!=null?collider.ClosestPoint(impact):(Vector2)friendly.transform.position;
            if(Vector2.Distance(impact,point)<=danger)return 0;
            // Include the support immediately below the feet, not just damage radius.
            Vector2 foot=collider!=null?new Vector2(collider.bounds.center.x,collider.bounds.min.y-.2f):point;
            if(Vector2.Distance(impact,foot)<=danger)return 0;
        }
        float progress=ObstructionProgress(PlanningOrigin,target,impact,radius,point=>
        {
            var type=terrain.GetTerrainTypeWorld(point);
            return terrain.IsSolidWorld(point) && (type==TerrainType.Base || type==TerrainType.Created || type==TerrainType.Cloud);
        });
        return progress>.04f ? .5f+Mathf.Min(3f,progress*2f) : 0;
    }

    public static float ObstructionProgress(Vector2 origin,Vector2 target,Vector2 impact,float radius,Func<Vector2,bool> removable)
    {
        Vector2 delta=target-origin;float distance=delta.magnitude;
        if(radius<=0 || distance<.5f)return 0;
        Vector2 direction=delta/distance;
        float center=Vector2.Dot(impact-origin,direction);
        if(center<.5f || center>distance || Vector2.Distance(impact,target)>=distance-.1f)return 0;
        // The caller has traced a real first impact on a target-directed arc.
        // Count removable cells there; no value is awarded for empty air or a
        // surface behind the actor. Sample inside the guaranteed first-cut disk.
        float step=radius*2f/5f,area=0;
        for(int y=0;y<5;y++)for(int x=0;x<5;x++)
        {
            Vector2 point=impact+new Vector2(-radius+(x+.5f)*step,-radius+(y+.5f)*step);
            if((point-impact).sqrMagnitude<radius*radius && removable(point))area+=step*step;
        }
        return area/Mathf.Max(.1f,2f*radius);
    }

    private static float DestructionRadius(ObjectHeadSkillSettings settings,int pixelsPerUnit)
        => (settings.terrainRadiusPx>0?settings.terrainRadiusPx:
            Mathf.Max(1,Mathf.RoundToInt(settings.explosionRadiusWorld*Mathf.Max(1,pixelsPerUnit))))/(float)Mathf.Max(1,pixelsPerUnit);

    private static float ChainReach(ObjectHeadSkillSettings settings)
    {
        if(settings.effectType!=SkillEffectType.ChainExplosion)return 0;
        int count=Mathf.Max(1,settings.chainCount);
        if(settings.useWideClusterPattern)return settings.chainSpreadRadiusWorld>0?settings.chainSpreadRadiusWorld:
            Mathf.Max(.35f,settings.chainSpacingWorld*Mathf.Max(1,count-1)*.5f);
        return Mathf.Abs(settings.chainSpacingWorld)*count*.5f;
    }

    public static float DestructiveProgressRadius(ObjectHeadSkillSettings settings,int pixelsPerUnit)
    {
        if(settings.effectType==SkillEffectType.DamageExplosion || settings.effectType==SkillEffectType.DelayedExplosion)
            return DestructionRadius(settings,pixelsPerUnit);
        // Rolling chains and sky support have moving impact points; do not pretend
        // their blast envelope can be safely inferred from the initial contact.
        if(settings.effectType!=SkillEffectType.ChainExplosion || settings.useRollingChainPath || settings.chainCount>16)return 0;
        if(settings.useWideClusterPattern)return DestructionRadius(settings,pixelsPerUnit); // First stamp is centered.
        int count=Mathf.Max(1,settings.chainCount);float nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)nearest=Mathf.Min(nearest,new Vector2((i-(count-1)*.5f)*settings.chainSpacingWorld,
            Mathf.Sin(i*1.7f)*settings.chainSpacingWorld*.5f).magnitude);
        return Mathf.Max(0,DestructionRadius(settings,pixelsPerUnit)-nearest);
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

    public bool Trace(ObjectHeadSkillSettings settings, Vector2 direction, float power, out Vector2 impact,CommonHeadType commonType=CommonHeadType.None)
    {
        bool common=commonType!=CommonHeadType.None && commonUse!=null;
        Vector2 position = PlanningOrigin + direction.normalized * (common?commonUse.AISpawnDistance:fire.LaunchOffset);
        Vector2 velocity = direction.normalized * (common?commonUse.AIThrowSpeed*power:settings.straightShot ? settings.straightSpeed : fire.LaunchSpeed * power);
        Vector2 gravity = common?Physics2D.gravity*commonUse.AIProjectileGravity:settings.straightShot ? Vector2.zero : Physics2D.gravity * fire.ProjectileGravity;
        float step = Mathf.Clamp(tuning.trajectoryStep, .01f, .1f);
        float spriteSize=settings.headSprite!=null?Mathf.Max(settings.headSprite.bounds.size.x,settings.headSprite.bounds.size.y):1f;
        float radius = common?commonUse.AIProjectileRadius:.5f*Mathf.Max(.01f,settings.projectileVisualDiameter/Mathf.Max(.01f,spriteSize));
        // Shape casts can miss an initial overlap when the projectile is heading
        // out of a capsule. Actual physics still emits that first contact.
        if(TryLaunchOverlap(Physics2D.defaultPhysicsScene,position,radius,actor.transform,launchOverlaps,out impact))return true;
        for (float time = 0; time < Mathf.Min(common?commonUse.AIProjectileLifetime:fire.ProjectileLifetime, tuning.trajectorySeconds); time += step)
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
                if (IgnoreTraceCollider(collider,actor.transform)) continue;
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

    public static bool TryLaunchOverlap(PhysicsScene2D physics,Vector2 position,float radius,Transform owner,
        Collider2D[] results,out Vector2 impact)
    {
        impact=position;
        int count=physics.OverlapCircle(position,radius,new ContactFilter2D{useTriggers=false},results);
        float nearest=float.PositiveInfinity;
        bool found=false;
        for(int i=0;i<count;i++)
        {
            if(IgnoreTraceCollider(results[i],owner))continue;
            Vector2 point=results[i].ClosestPoint(position);
            float distance=(point-position).sqrMagnitude;
            if(distance>=nearest)continue;
            nearest=distance;impact=point;found=true;
        }
        // A saturated query is not evidence that the launch is clear.
        return found || count==results.Length;
    }

    private static bool IgnoreTraceCollider(Collider2D collider,Transform owner)=>collider==null ||
        (owner!=null && collider.transform.IsChildOf(owner)) ||
        collider.GetComponentInParent<CommonHeadItem>()!=null || collider.GetComponentInParent<SkillProjectile>()!=null;

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

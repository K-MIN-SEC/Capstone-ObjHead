using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turn driver for computer-controlled seats. Offline games and the online room
/// host run the same brain; guests only receive authoritative movement/actions.
/// </summary>
public sealed class ObjectHeadAIDirector : MonoBehaviour
{
    private TurnManager turns;
    private Coroutine decision;
    private System.Random random;
    public static int ActionsTaken {get;private set;}
    public static float DistanceMoved {get;private set;}
    public static int CandidatesEvaluated {get;private set;}
    public static int JumpsRequested {get;private set;}
    public static int MovementCandidates {get;private set;}
    private Vector2 movementGoal;
    private float movementScore;
    private bool movementIsPickup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded-=SceneLoaded;
        SceneManager.sceneLoaded+=SceneLoaded;
    }

    private static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        GameStartData data=GameStartData.Instance;
        if(!CanDriveAi(data,ObjectHeadNetworkManager.Instance?.IsHost==true) || FindAnyObjectByType<TurnManager>()==null)return;
        if(FindAnyObjectByType<ObjectHeadAIDirector>()==null)new GameObject("AuthoritativeAIDirector").AddComponent<ObjectHeadAIDirector>();
    }

    public static bool CanDriveAi(GameStartData data,bool isHost) => data!=null &&
        (data.localMatch || (!data.dedicatedAuthority && isHost)) &&
        data.players!=null && data.players.Any(player=>player!=null && player.isAi);

    private bool HasAuthority => CanDriveAi(GameStartData.Instance,ObjectHeadNetworkManager.Instance?.IsHost==true);

    private void Awake()
    {
        turns=FindAnyObjectByType<TurnManager>();
        random=new System.Random((GameStartData.Instance?.mapSeed??916)^0x0A11CE);
        ActionsTaken=0;
        DistanceMoved=0;
        CandidatesEvaluated=0;
        JumpsRequested=0;MovementCandidates=0;
        if(turns!=null)turns.TurnStarted+=TurnStarted;
    }

    private void Start(){if(turns?.CurrentCharacter!=null)TurnStarted(turns.CurrentCharacter);}
    private void OnDestroy(){if(turns!=null)turns.TurnStarted-=TurnStarted;}

    private void Update()
    {
        // The first turn can be announced before peers finish loading terrain.
        // Releasing that barrier does not announce a new turn, so resume its AI here.
        if(HasAuthority && decision==null && turns!=null && !turns.IsNetworkStartPending &&
            turns.CanCharacterFire(turns.CurrentCharacter) && IsAi(turns.CurrentCharacter))
            decision=StartCoroutine(Act(turns.CurrentCharacter,turns.TurnSerial));
    }

    private void TurnStarted(TurnCharacterController character)
    {
        if(decision!=null)StopCoroutine(decision);
        decision=null;
        if(HasAuthority && !turns.IsNetworkStartPending && IsAi(character))
            decision=StartCoroutine(Act(character,turns.TurnSerial));
    }

    private bool IsAi(TurnCharacterController character)
    {
        int player=character?.GetComponent<ObjectHeadTeamMember>()?.PlayerIndex??0;
        return GameStartData.Instance?.IsAiPlayer(player)==true;
    }

    private IEnumerator Act(TurnCharacterController actor,int serial)
    {
        ObjectHeadPlayerAssignment assignment=Assignment(actor);
        ObjectHeadAIDifficulty difficulty=assignment?.aiDifficulty??ObjectHeadAIDifficulty.Normal;
        ObjectHeadAIProfile profile=ObjectHeadAISettings.Profile(difficulty);
        actor.UseNetworkInput=true;
        yield return new WaitForSeconds(profile.decisionDelay*UnityEngine.Random.Range(.9f,1.1f));
        if(!StillCurrent(actor,serial)){FinishDecision(serial);yield break;}

        var tuning=ObjectHeadAITuning.Load();
        var terrain=FindAnyObjectByType<TerrainManager>();
        DemoSkillSelector selector=actor.GetComponent<DemoSkillSelector>();
        TurnCharacterController target=turns.Characters.Where(c=>c!=null && c!=actor &&
            c.GetComponent<CharacterCombat>()?.IsDead==false && !ObjectHeadAIPlanner.SameTeam(actor,c))
            .OrderBy(c=>Vector2.Distance(actor.transform.position,c.transform.position)).FirstOrDefault();
        if(target==null){FinishDecision(serial);turns.EndCurrentTurn();yield break;}
        var planner=new ObjectHeadAIPlanner(actor,turns.Characters,terrain);
        yield return planner.Search(difficulty,()=>StillCurrent(actor,serial));
        CandidatesEvaluated+=planner.Evaluated;
        // Compare a shot from here, a reachable firing position and nearby team loot.
        // A legal shot is no longer an unconditional order to stand still and fire.
        yield return FindFiringPosition(actor,target,terrain,difficulty,serial);
        FindPickupGoal(actor,terrain);
        // A distant opening spawn can have no hittable target from any nearby probe.
        // Do not spend that turn standing still: advance to a safe waypoint and try again.
        if(!planner.Best.valid && movementScore<=tuning.moveDecisionMargin)
            FindAdvanceGoal(actor,target,terrain);
        float currentScore=planner.Best.valid?planner.Best.score:0f;
#if UNITY_EDITOR
        Debug.Log($"[AI_CHOICE] {actor.name} shot={currentScore:F1} valid={planner.Best.valid} move={movementScore:F1} pickup={movementIsPickup} goal={movementGoal} from={actor.transform.position}");
#endif
        // Pickup utility already includes the travel cost; requiring an extra
        // firing-position margin made nearby team loot lose to mediocre shots.
        float moveMargin=movementIsPickup?0f:tuning.moveDecisionMargin;
        if(movementScore>currentScore+moveMargin)
        {
            yield return MoveToFiringPosition(actor,target,serial);
            float landingDeadline=Time.time+tuning.landingWaitSeconds;
            while(StillCurrent(actor,serial) && !actor.IsGrounded && Time.time<landingDeadline)yield return null;
            if(!StillCurrent(actor,serial)){FinishDecision(serial);yield break;}
            yield return planner.Search(difficulty,()=>StillCurrent(actor,serial));
            CandidatesEvaluated+=planner.Evaluated;
        }
        if(!StillCurrent(actor,serial)){FinishDecision(serial);yield break;}
        var shot=planner.Best;
        if(!shot.valid)
        {
            if(ObjectHeadAIHover.TryPlan(actor,target,terrain,out var hoverGoal))
            {yield return ObjectHeadAIHover.Move(actor,hoverGoal,turns,serial);FinishDecision(serial);yield break;}
            // Only after useful attacks and safe movement fail, open real blocking
            // terrain with a legal destructive skill. Zero-score random shots do not help.
            yield return planner.SearchObstruction(()=>StillCurrent(actor,serial));
            CandidatesEvaluated+=planner.Evaluated;
            if(!StillCurrent(actor,serial)){FinishDecision(serial);yield break;}
            shot=planner.Best;
            if(!shot.valid){actor.SetNetworkInput(0,false);FinishDecision(serial);turns.EndCurrentTurn();yield break;}
        }
        if(shot.commonType==CommonHeadType.None)
        {
            selector.SetSkillIndex(shot.skill,false);
            if(shot.gourdChoice!=null)actor.GetComponent<ObjectHeadGourd>()?.Select(shot.gourdChoice);
        }
        AimController aim=actor.GetComponent<AimController>();
        Vector2 direction=shot.direction;
        float power=shot.power;
        if(difficulty!=ObjectHeadAIDifficulty.Pro)
        {
            float safest=float.NegativeInfinity;
            // Keep execution error for both beginner and normal. Retry only to avoid
            // obvious self/team hits; a miss is allowed and never snaps to perfect aim.
            for(int attempt=0;attempt<tuning.aimSafetySamples;attempt++)
            {
                float error=((float)random.NextDouble()*2f-1f)*profile.aimErrorDegrees;
                Vector2 candidateDirection=Quaternion.Euler(0,0,error)*shot.direction;
                float candidatePower=Mathf.Clamp01(shot.power+((float)random.NextDouble()*2f-1f)*profile.powerError);
                bool hit=planner.Trace(shot.settings,candidateDirection,candidatePower,out var impact,shot.commonType);
                float safety=shot.opensObstruction
                    ? (hit?planner.ScoreObstructionProgress(shot.settings,impact,shot.progressTarget):0f)
                    : (hit?planner.Score(shot.settings,impact):0f);
                if(safety>safest){safest=safety;direction=candidateDirection;power=candidatePower;}
                if(shot.opensObstruction?safety>0f:safety>=0f)break;
            }
            // Keep the selected difficulty's execution error. Never replace an
            // unsafe perturbed excavation with the perfect, unperturbed shot.
            if(shot.opensObstruction && safest<=0f)
            {actor.SetNetworkInput(0,false);FinishDecision(serial);turns.EndCurrentTurn();yield break;}
        }
        aim.SetAimDirection(direction);
        yield return new WaitForSeconds(profile.decisionDelay*.45f);
        if(!StillCurrent(actor,serial)){FinishDecision(serial);yield break;}
        // Landing/knockback can change the launch point during the aiming delay.
        // Recheck even Pro without correcting aim or granting a different skill.
        if(shot.opensObstruction)
        {
            // The displayed facing can change during the aiming delay. Validate
            // the same direction the Fire API actually consumes, without a yield.
            aim.SetAimDirection(direction);
            direction=aim.AimDirection;
            if(!planner.Trace(shot.settings,direction,power,out var finalImpact,shot.commonType) ||
                planner.ScoreObstructionProgress(shot.settings,finalImpact,shot.progressTarget)<=0)
            {actor.SetNetworkInput(0,false);FinishDecision(serial);turns.EndCurrentTurn();yield break;}
            shot.impact=finalImpact;
        }
        if(shot.commonType!=CommonHeadType.None)
        {
            var common=actor.GetComponent<CommonHeadUseController>();
            if(common!=null && common.TrySelectCommonHeadSlot(shot.commonSlot))common.UseSelectedHead(power);
        }
        else actor.GetComponent<SkillFireController>().Fire(power);
        if(turns.ActionUsedThisTurn)
        {
            ActionsTaken++;
            if(shot.opensObstruction)Debug.Log($"[AI_OBSTRUCTION] turn={serial} actor={actor.name} skill={shot.settings.skillId} impact={shot.impact} utility={shot.score:F2}");
            // Wait for the legal escape window, not while a projectile is still resolving.
            while(turns.TurnSerial==serial && !turns.IsResidualTimeActive && turns.IsActionPending)yield return null;
            if(turns.TurnSerial==serial && turns.IsResidualTimeActive && shot.settings.effectType!=SkillEffectType.HealBurst)
                yield return Retreat(actor,shot.impact,shot.settings.explosionRadiusWorld,serial);
        }
        actor.SetNetworkInput(0,false);
        FinishDecision(serial);
    }

    private IEnumerator MoveToFiringPosition(TurnCharacterController actor,TurnCharacterController target,int serial)
    {
        var config=ObjectHeadContent.Load();
        var terrain=FindAnyObjectByType<TerrainManager>();
        if(config==null || terrain==null)yield break;
        var navigation=new ObjectHeadAINavigation(actor,terrain);
        float until=Time.time+ObjectHeadAITuning.Load().movementSearchSeconds;
        Vector2 previous=actor.transform.position;
        while(Time.time<until && StillCurrent(actor,serial) && target!=null)
        {
            Vector2 position=actor.transform.position;
            float moved=Mathf.Abs(position.x-previous.x);
            DistanceMoved+=moved;
            previous=position;
            float dx=movementGoal.x-position.x;
            float direction=Mathf.Sign(dx);
            if(Mathf.Abs(dx)<.25f && actor.IsGrounded)break;
            int before=navigation.Jumps;
            if(!navigation.Step(direction))break;
            JumpsRequested+=navigation.Jumps-before;
            yield return null;
        }
        // Finish an already committed jump instead of cutting its horizontal input mid-flight.
        float landingDeadline=Time.time+ObjectHeadAITuning.Load().landingWaitSeconds;
        while(StillCurrent(actor,serial) && !actor.IsGrounded && Time.time<landingDeadline)
        {
            navigation.Step(Mathf.Sign(movementGoal.x-actor.transform.position.x));
            yield return null;
        }
        if(actor!=null)actor.SetNetworkInput(0,false);
    }

    private IEnumerator FindFiringPosition(TurnCharacterController actor,TurnCharacterController target,TerrainManager terrain,ObjectHeadAIDifficulty difficulty,int serial)
    {
        var tuning=ObjectHeadAITuning.Load();var nav=new ObjectHeadAINavigation(actor,terrain);
        Vector2 start=actor.transform.position;movementGoal=start;movementScore=float.NegativeInfinity;movementIsPickup=false;
        float deadline=Time.realtimeSinceStartup+tuning.navigationBudgetSeconds;
        for(int sample=1;sample<=tuning.navigationPositionSamples;sample++)foreach(float sign in new[]{-1f,1f})
        {
            if(!StillCurrent(actor,serial)||Time.realtimeSinceStartup>=deadline)yield break;
            float distance=sample*tuning.navigationStepWorld;
            if(!nav.GroundAt(start.x+sign*distance,start.y,out var candidate))continue;
            if(!nav.CanStartToward(sign))continue;
            // Never teleport the actor for planning. Trace from a virtual launch origin.
            var probe=new ObjectHeadAIPlanner(actor,turns.Characters,terrain,candidate);
            yield return probe.Search(difficulty,()=>StillCurrent(actor,serial)&&Time.realtimeSinceStartup<deadline,.12f);
            MovementCandidates++;CandidatesEvaluated+=probe.Evaluated;
            if(!probe.Best.valid)continue;
            float score=probe.Best.score-distance*tuning.travelCostPerWorldUnit;
            score+=Mathf.Clamp(candidate.y-start.y,-1,2)*tuning.heightAdvantageWeight;
            if(score>movementScore){movementScore=score;movementGoal=candidate;movementIsPickup=false;}
        }
    }

    private void FindPickupGoal(TurnCharacterController actor,TerrainManager terrain)
    {
        if(terrain==null)return;
        var manager=FindAnyObjectByType<PlayerInventoryManager>();
        var inventory=manager?.GetInventoryFor(actor.gameObject);
        if(inventory==null || inventory.Count>=CommonHeadInventory.SlotCount)return;
        var tuning=ObjectHeadAITuning.Load();
        var navigation=new ObjectHeadAINavigation(actor,terrain);
        Vector2 start=actor.transform.position;
        float travelLimit=actor.NavigationMoveSpeed*tuning.movementSearchSeconds*tuning.pickupTravelTimeFraction;
        foreach(var item in FindObjectsByType<CommonHeadItem>())
        {
            if(item==null || item.ItemType==CommonHeadType.None || item.IsVacuumPulled)continue;
            float distance=Mathf.Abs(item.transform.position.x-start.x);
            if(distance<tuning.minimumPickupDistance || distance>travelLimit)continue;
            if(!navigation.GroundAt(item.transform.position.x,start.y,out var landing))continue;
            if(!navigation.CanStartToward(Mathf.Sign(item.transform.position.x-start.x)))continue;
            if(Mathf.Abs(landing.y-item.transform.position.y)>tuning.pickupVerticalReach)continue;
            bool duplicate=false;
            for(int slot=0;slot<CommonHeadInventory.SlotCount;slot++)
                if(inventory.GetSlot(slot)==item.ItemType){duplicate=true;break;}
            MovementCandidates++;
            float score=tuning.pickupUtility*(duplicate?tuning.duplicatePickupMultiplier:1f)-distance*tuning.travelCostPerWorldUnit;
            if(score>movementScore){movementScore=score;movementGoal=landing;movementIsPickup=true;}
        }
    }

    private void FindAdvanceGoal(TurnCharacterController actor,TurnCharacterController target,TerrainManager terrain)
    {
        if(actor==null || target==null || terrain==null)return;
        var tuning=ObjectHeadAITuning.Load();
        var navigation=new ObjectHeadAINavigation(actor,terrain);
        Vector2 start=actor.transform.position;
        Vector2 destination=target.transform.position;
        float originalDistance=Vector2.Distance(start,destination);
        float maxTravel=actor.NavigationMoveSpeed*tuning.movementSearchSeconds*.8f;
        float direction=Mathf.Sign(destination.x-start.x);
        if(Mathf.Abs(destination.x-start.x)<.1f)direction=1f;
        if(!navigation.CanStartToward(direction))return;

        for(int sample=1;sample<=tuning.navigationPositionSamples;sample++)
        {
            float distance=sample*tuning.navigationStepWorld;
            if(distance>maxTravel)break;
            if(!navigation.GroundAt(start.x+direction*distance,start.y,out var waypoint))continue;
            float progress=originalDistance-Vector2.Distance(waypoint,destination);
            if(progress<=.1f)continue;
            float score=tuning.moveDecisionMargin+1f+progress;
            if(score>movementScore){movementScore=score;movementGoal=waypoint;movementIsPickup=false;}
        }
        if(movementScore>tuning.moveDecisionMargin)return;
        // Gaps may have no walkable intermediate probe, but a tested jump can cross them.
        if(navigation.TryJump(direction,out var landing) &&
            Vector2.Distance(landing,destination)<originalDistance-.1f)
        {
            movementGoal=landing;
            movementScore=tuning.moveDecisionMargin+1f;
            movementIsPickup=false;
        }
    }

    private IEnumerator Retreat(TurnCharacterController actor,Vector2 impact,float radius,int serial)
    {
        var config=ObjectHeadContent.Load();var tuning=ObjectHeadAITuning.Load();
        var terrain=FindAnyObjectByType<TerrainManager>();
        if(config==null || terrain==null)yield break;
        var navigation=new ObjectHeadAINavigation(actor,terrain);
        float until=Time.time+tuning.retreatSeconds;
        while(actor!=null && turns.TurnSerial==serial && turns.CanCharacterMove(actor) && Time.time<until)
        {
            Vector2 point=actor.transform.position;
            if(Vector2.Distance(point,impact)>radius+tuning.dangerClearance)break;
            float direction=point.x>=impact.x?1:-1;
            int before=navigation.Jumps;if(!navigation.Step(direction))break;JumpsRequested+=navigation.Jumps-before;
            yield return null;
        }
        if(actor!=null)actor.SetNetworkInput(0,false);
    }

    private ObjectHeadPlayerAssignment Assignment(TurnCharacterController character)
    {
        int player=character?.GetComponent<ObjectHeadTeamMember>()?.PlayerIndex??0;
        return GameStartData.Instance?.players?.FirstOrDefault(entry=>entry.playerIndex==player);
    }

    // EndCurrentTurn invokes TurnStarted synchronously. An old coroutine must not
    // erase the new turn's coroutine handle after that callback has installed it.
    private void FinishDecision(int serial){if(turns==null || turns.TurnSerial==serial)decision=null;}
    private bool StillCurrent(TurnCharacterController actor,int serial)=>HasAuthority && turns!=null && !turns.IsMatchOver && turns.TurnSerial==serial && turns.CurrentCharacter==actor && turns.CanCharacterFire(actor);
}

using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Local turn driver for computer-controlled player seats. Decisions use only
/// public gameplay APIs so the same brain can later be hosted by the authority
/// worker without changing skill or combat code.
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded-=SceneLoaded;
        SceneManager.sceneLoaded+=SceneLoaded;
    }

    private static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        GameStartData data=GameStartData.Instance;
        if(data==null || !data.localMatch || data.players==null || !data.players.Any(player=>player.isAi) || FindAnyObjectByType<TurnManager>()==null)return;
        if(FindAnyObjectByType<ObjectHeadAIDirector>()==null)new GameObject("LocalAIDirector").AddComponent<ObjectHeadAIDirector>();
    }

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

    private void TurnStarted(TurnCharacterController character)
    {
        if(decision!=null)StopCoroutine(decision);
        decision=null;
        if(IsAi(character))decision=StartCoroutine(Act(character,turns.TurnSerial));
    }

    private bool IsAi(TurnCharacterController character)
    {
        int player=character?.GetComponent<ObjectHeadTeamMember>()?.PlayerIndex??0;
        return GameStartData.Instance?.players?.FirstOrDefault(entry=>entry.playerIndex==player)?.isAi==true;
    }

    private IEnumerator Act(TurnCharacterController actor,int serial)
    {
        ObjectHeadPlayerAssignment assignment=Assignment(actor);
        ObjectHeadAIDifficulty difficulty=assignment?.aiDifficulty??ObjectHeadAIDifficulty.Normal;
        ObjectHeadAIProfile profile=ObjectHeadAISettings.Profile(difficulty);
        actor.UseNetworkInput=true;
        yield return new WaitForSeconds(profile.decisionDelay*UnityEngine.Random.Range(.9f,1.1f));
        if(!StillCurrent(actor,serial)){decision=null;yield break;}

        var tuning=ObjectHeadAITuning.Load();
        var terrain=FindAnyObjectByType<TerrainManager>();
        DemoSkillSelector selector=actor.GetComponent<DemoSkillSelector>();
        TurnCharacterController target=turns.Characters.Where(c=>c!=null && c!=actor &&
            c.GetComponent<CharacterCombat>()?.IsDead==false && !ObjectHeadAIPlanner.SameTeam(actor,c))
            .OrderBy(c=>Vector2.Distance(actor.transform.position,c.transform.position)).FirstOrDefault();
        if(target==null){turns.EndCurrentTurn();decision=null;yield break;}
        var planner=new ObjectHeadAIPlanner(actor,turns.Characters,terrain);
        yield return planner.Search(difficulty,()=>StillCurrent(actor,serial));
        CandidatesEvaluated+=planner.Evaluated;
        // A valid safe shot should not be thrown away by walking first.
        if(!planner.Best.valid)
        {
            yield return FindFiringPosition(actor,target,terrain,difficulty,serial);
            yield return MoveToFiringPosition(actor,target,serial);
            float landingDeadline=Time.time+tuning.landingWaitSeconds;
            while(StillCurrent(actor,serial) && !actor.IsGrounded && Time.time<landingDeadline)yield return null;
            yield return planner.Search(difficulty,()=>StillCurrent(actor,serial));
            CandidatesEvaluated+=planner.Evaluated;
        }
        if(!StillCurrent(actor,serial)){decision=null;yield break;}
        var shot=planner.Best;
        if(!shot.valid)
        {
            if(ObjectHeadAIHover.TryPlan(actor,target,terrain,out var hoverGoal))
            {yield return ObjectHeadAIHover.Move(actor,hoverGoal,turns,serial);decision=null;yield break;}
            actor.SetNetworkInput(0,false);turns.EndCurrentTurn();decision=null;yield break;
        }
        selector.SetSkillIndex(shot.skill,false);
        if(shot.gourdChoice!=null)actor.GetComponent<ObjectHeadGourd>()?.Select(shot.gourdChoice);
        AimController aim=actor.GetComponent<AimController>();
        float error=((float)random.NextDouble()*2f-1f)*profile.aimErrorDegrees;
        Vector2 direction=Quaternion.Euler(0,0,error)*shot.direction;
        float power=Mathf.Clamp01(shot.power+((float)random.NextDouble()*2f-1f)*profile.powerError);
        // Difficulty adds bounded error, never hidden damage or a larger legal launch speed.
        // Normal/pro reject an error sample that turns a good shot into friendly fire.
        if(difficulty!=ObjectHeadAIDifficulty.Beginner && !ObjectHeadGourd.IsMeta(shot.settings.effectType) && shot.settings.effectType!=SkillEffectType.Airflow &&
            (!planner.Trace(shot.settings,direction,power,out var noisyImpact) || planner.Score(shot.settings,noisyImpact)<=0))
        { direction=shot.direction;power=shot.power; }
        aim.SetAimDirection(direction);
        yield return new WaitForSeconds(profile.decisionDelay*.45f);
        if(!StillCurrent(actor,serial)){decision=null;yield break;}
        actor.GetComponent<SkillFireController>().Fire(power);
        if(turns.ActionUsedThisTurn)
        {
            ActionsTaken++;
            // Wait for the legal escape window, not while a projectile is still resolving.
            while(turns.TurnSerial==serial && !turns.IsResidualTimeActive && turns.IsActionPending)yield return null;
            if(turns.TurnSerial==serial && turns.IsResidualTimeActive && shot.settings.effectType!=SkillEffectType.HealBurst)
                yield return Retreat(actor,shot.impact,shot.settings.explosionRadiusWorld,serial);
        }
        actor.SetNetworkInput(0,false);
        decision=null;
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
        Vector2 start=actor.transform.position;movementGoal=start;float best=float.NegativeInfinity;
        float deadline=Time.realtimeSinceStartup+tuning.navigationBudgetSeconds;
        for(int sample=1;sample<=tuning.navigationPositionSamples;sample++)foreach(float sign in new[]{-1f,1f})
        {
            if(!StillCurrent(actor,serial)||Time.realtimeSinceStartup>=deadline)yield break;
            float distance=sample*tuning.navigationStepWorld;
            if(!nav.GroundAt(start.x+sign*distance,start.y,out var candidate))continue;
            // Never teleport the actor for planning. Trace from a virtual launch origin.
            var probe=new ObjectHeadAIPlanner(actor,turns.Characters,terrain,candidate);
            yield return probe.Search(difficulty,()=>StillCurrent(actor,serial)&&Time.realtimeSinceStartup<deadline,.12f);
            MovementCandidates++;CandidatesEvaluated+=probe.Evaluated;
            float score=probe.Best.valid?100+probe.Best.score:0;
            score-=Vector2.Distance(candidate,target.transform.position)*.4f+distance*.25f;
            score+=Mathf.Clamp(candidate.y-start.y,-1,2)*.3f;
            if(score>best){best=score;movementGoal=candidate;}
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

    private bool StillCurrent(TurnCharacterController actor,int serial)=>turns!=null && !turns.IsMatchOver && turns.TurnSerial==serial && turns.CurrentCharacter==actor && turns.CanCharacterFire(actor);
}

using System.Collections;
using UnityEngine;

/// <summary>Flight is a legal skill action, never an AI-only teleport or jump boost.</summary>
public static class ObjectHeadAIHover
{
    public static int Uses {get;private set;}
    public static bool TryPlan(TurnCharacterController actor,TurnCharacterController target,TerrainManager terrain,out Vector2 goal)
    {
        goal=actor.transform.position;var selector=actor.GetComponent<DemoSkillSelector>();
        if(terrain==null||target==null||selector.GetRemainingCooldown(2)>0)return false;
        var skill=selector.GetSkillSettings(2);if(skill.effectType!=SkillEffectType.Hover||skill.vacuum==null)return false;
        var nav=new ObjectHeadAINavigation(actor,terrain);var start=(Vector2)actor.GetComponent<Collider2D>().bounds.center;
        float direction=Mathf.Sign(target.transform.position.x-start.x),best=Vector2.Distance(start,target.transform.position);
        var config=ObjectHeadAITuning.Load();
        float takeoffSeconds=skill.vacuum.hoverHeight.y/Mathf.Max(.1f,skill.vacuum.verticalSpeed)+.3f;
        float max=actor.NavigationMoveSpeed*Mathf.Max(0,skill.vacuum.hoverSeconds-takeoffSeconds)*.65f;
        for(int i=1;i<=config.navigationPositionSamples;i++)
        {
            float distance=i*config.navigationStepWorld;if(distance>max)break;
            if(!nav.GroundAt(start.x+direction*distance,start.y,out var landing))continue;
            float flightY=Mathf.Max(start.y,landing.y)+skill.vacuum.hoverHeight.y;
            bool clear=true;
            for(float y=start.y;y<=flightY;y+=.15f)if(!nav.Clear(new Vector2(start.x,y))){clear=false;break;}
            for(float x=0;x<=distance && clear;x+=.15f)if(!nav.Clear(new Vector2(start.x+direction*x,flightY)))clear=false;
            float cost=Vector2.Distance(landing,target.transform.position);
            if(clear && cost<best-.5f){best=cost;goal=landing;}
        }
        return Mathf.Abs(goal.x-start.x)>.5f;
    }
    public static IEnumerator Move(TurnCharacterController actor,Vector2 goal,TurnManager turns,int serial)
    {
        var tuning=actor.GetComponent<DemoSkillSelector>().GetSkillSettings(2).vacuum;
        float takeoffY=actor.transform.position.y+tuning.hoverHeight.y;
        actor.GetComponent<DemoSkillSelector>().SetSkillIndex(2,false);actor.GetComponent<SkillFireController>().Fire(1);
        if(!actor.IsVacuumHovering)yield break;Uses++;
        // Execute the same vertical-then-horizontal path that TryPlan checked.
        float takeoffDeadline=Time.time+tuning.hoverHeight.y/Mathf.Max(.1f,tuning.verticalSpeed)+.5f;
        while(actor!=null && actor.IsVacuumHovering && turns.TurnSerial==serial && turns.CanCharacterMove(actor) && actor.transform.position.y<takeoffY-.15f && Time.time<takeoffDeadline)
        {actor.SetNetworkInput(0,false);yield return null;}
        while(actor!=null && actor.IsVacuumHovering && turns.TurnSerial==serial && turns.CanCharacterMove(actor))
        {
            float dx=goal.x-actor.transform.position.x;
            actor.SetNetworkInput(Mathf.Abs(dx)<.15f?0:Mathf.Sign(dx),false);yield return null;
        }
        if(actor!=null)actor.SetNetworkInput(0,false);
    }
}

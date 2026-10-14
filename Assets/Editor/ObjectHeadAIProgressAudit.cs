using System;
using UnityEngine;

/// <summary>Planning checks plus isolated physics regression; does not alter a match.</summary>
public static class ObjectHeadAIProgressAudit
{
    public static void Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,label)=>
        {if(!ok)throw new InvalidOperationException("[AI_PROGRESS_FAIL] "+label);checks++;};
        check(Mathf.Approximately(ObjectHeadAIPlanner.SearchPower(0,7,.2f),1f),"first round tries full reach for every target");
        check(Mathf.Approximately(ObjectHeadAIPlanner.SearchPower(1,7,.2f),.6f),"second round covers medium throws");
        check(Mathf.Approximately(ObjectHeadAIPlanner.SearchPower(2,7,.2f),.2f),"third round covers short throws before head budget ends");
        check(ObjectHeadAIPlanner.HeadCheckBudget(18,3)==18,"two-player target coverage retains the old head cap");
        check(ObjectHeadAIPlanner.HeadCheckBudget(18,9)==54,"four-player FFA covers three powers and two arcs per enemy");
        check(ObjectHeadAIPlanner.HeadCheckBudget(80,9)==80,"larger authored head cap is preserved");
        foreach(int targets in new[]{3,6,9})
        {
            var coverage=new bool[3,2,targets];int count=0;
            foreach(var sample in ObjectHeadAIPlanner.TrajectorySamples(targets,7,false,18))
            {
                if(sample.powerIndex<3)coverage[sample.powerIndex,sample.arc,sample.targetIndex]=true;
                count++;
            }
            bool complete=true;
            for(int power=0;power<3;power++)for(int arc=0;arc<2;arc++)for(int targetIndex=0;targetIndex<targets;targetIndex++)
                complete&=coverage[power,arc,targetIndex];
            check(complete,"actual search order covers high/medium/low and both arcs for every target: "+targets);
            check(count==ObjectHeadAIPlanner.HeadCheckBudget(18,targets),"actual search order respects the bounded head cap: "+targets);
        }
        for(int count=3;count<=18;count++)
        {
            var seen=new System.Collections.Generic.HashSet<int>();
            for(int sample=0;sample<count;sample++)seen.Add(Mathf.RoundToInt(ObjectHeadAIPlanner.SearchPower(sample,count,0)*(count-1)));
            check(seen.Count==count,"bounded power ordering visits every sample exactly once: "+count);
        }
        var shot=new ObjectHeadSkillSettings{effectType=SkillEffectType.DamageExplosion,terrainRadiusPx=32,explosionRadiusWorld=.7f};
        check(Mathf.Approximately(ObjectHeadAIPlanner.DestructiveProgressRadius(shot,32),1),"uses actual terrain brush radius");
        shot.effectType=SkillEffectType.CreateTerrainCircle;
        check(ObjectHeadAIPlanner.DestructiveProgressRadius(shot,32)==0,"terrain creation cannot clear an obstruction");
        shot.effectType=SkillEffectType.CreateHazardZone;
        check(ObjectHeadAIPlanner.DestructiveProgressRadius(shot,32)==0,"hazard zones are not excavation");
        shot.effectType=SkillEffectType.ChainExplosion;shot.chainCount=5;shot.chainSpacingWorld=.4f;
        check(ObjectHeadAIPlanner.DestructiveProgressRadius(shot,32)>.9f,"stationary chain guarantees a central cut");
        shot.useRollingChainPath=true;
        check(ObjectHeadAIPlanner.DestructiveProgressRadius(shot,32)==0,"unknown rolling path is not accepted as safe excavation");
        var origin=Vector2.zero;var target=new Vector2(10,0);var impact=new Vector2(5,2);
        check(ObjectHeadAIPlanner.ObstructionProgress(origin,target,impact,1,p=>false)==0,"empty air earns no progress");
        check(ObjectHeadAIPlanner.ObstructionProgress(origin,target,impact,1,p=>p.x>=5)>.1f,"real blocked high arc earns measurable terrain progress");
        check(ObjectHeadAIPlanner.ObstructionProgress(origin,target,new Vector2(-2,0),1,p=>true)==0,"terrain behind actor is rejected");
        check(ObjectHeadAIPlanner.ObstructionProgress(origin,target,new Vector2(12,0),1,p=>true)==0,"terrain beyond enemy is rejected");
        check(ObjectHeadAIPlanner.ObstructionProgress(origin,target,new Vector2(1,12),1,p=>true)==0,"removing distant unrelated ceiling is rejected");
        CheckLaunchOverlap(check);
        Debug.Log("[AI_PROGRESS_AUDIT_PASS] "+checks+" bounded AI progress checks");
    }

    private static void CheckLaunchOverlap(Action<bool,string> check)
    {
        var ally=new GameObject("TidalSeedLaunchOverlap");
        try
        {
            Vector2 offset=new Vector2(10000,10000);
            ally.transform.position=offset+new Vector2(-14.36f,-1.17f);
            var capsule=ally.AddComponent<CapsuleCollider2D>();
            capsule.direction=CapsuleDirection2D.Vertical;
            capsule.size=new Vector2(.72f,1.15f);
            capsule.offset=new Vector2(0,.02f);
            var physics=Physics2D.defaultPhysicsScene;
            Physics2D.SyncTransforms();
            Vector2 spawn=offset+new Vector2(-15.0705f,-1.2735f)+new Vector2(.4993f,.8664f).normalized*.7f;
            var results=new Collider2D[8];
            check(ObjectHeadAIPlanner.TryLaunchOverlap(physics,spawn,.0951f,null,results,out var contact),
                "real capsule overlap at tidal launch is reported before an outward shape cast");
            check((contact-capsule.ClosestPoint(spawn)).sqrMagnitude<.000001f,
                "launch overlap uses the real collider contact point");
            check(!ObjectHeadAIPlanner.TryLaunchOverlap(physics,spawn,.0951f,ally.transform,results,out _),
                "projectile owner remains excluded from launch contact prediction");
            check(!ObjectHeadAIPlanner.TryLaunchOverlap(physics,offset+new Vector2(10,10),.0951f,null,results,out _),
                "clear launch space does not produce an imaginary impact");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ally);
            Physics2D.SyncTransforms();
        }
    }
}

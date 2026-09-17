using System.Collections;
using System.Linq;
using UnityEngine;

public static class ObjectHeadVacuum
{
    public static IEnumerator PresentHover(TurnCharacterController actor,ObjectHeadVacuumDefinition tuning)
    {
        if(actor==null || ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)yield break;
        var turns=Object.FindAnyObjectByType<TurnManager>();int serial=turns!=null?turns.TurnSerial:-1;
        var collider=actor.GetComponent<Collider2D>();float until=Time.time+tuning.hoverSeconds;
        while(actor!=null && Time.time<until && turns!=null && turns.TurnSerial==serial)
        {
            if(collider!=null)ObjectHeadActionCues.Hover(new Vector2(collider.bounds.center.x,collider.bounds.min.y));
            yield return new WaitForSeconds(ObjectHeadActionCues.Load()?.hoverInterval??.18f);
        }
    }
    public static bool InBeam(Vector2 origin,Vector2 direction,Vector2 point,float range,float width)
    {
        if(direction.sqrMagnitude<.0001f)return false;
        direction.Normalize();var delta=point-origin;float along=Vector2.Dot(delta,direction);
        return along>=0 && along<=range && Mathf.Abs(delta.x*direction.y-delta.y*direction.x)<=width*.5f;
    }
    public static bool Clear(TerrainManager terrain,Vector2 origin,Vector2 end) =>
        terrain==null || !terrain.TryCheckTerrainHit(origin,end,out _);

    public static int Apply(CharacterCombat owner,Vector2 origin,Vector2 direction,float charge,bool pull,ObjectHeadVacuumDefinition tuning)
    {
        if(!ObjectHeadCommonAuthority.CanWrite || owner==null || tuning==null)return 0;
        direction=direction.sqrMagnitude>.001f?direction.normalized:Vector2.right;
        float range=tuning.Range(charge);var terrain=Object.FindAnyObjectByType<TerrainManager>();int affected=0;
        foreach(var target in Object.FindObjectsByType<CharacterCombat>(FindObjectsSortMode.None))
        {
            if(target==owner || target.IsDead || !InBeam(origin,direction,target.KnockbackCenter,range,tuning.beamWidth) || !Clear(terrain,origin,target.KnockbackCenter))continue;
            // No faction filter: both teammates and enemies are displaced; no direct damage.
            target.ApplyKnockback(direction*tuning.Force(charge)*(pull?-1:1));affected++;
        }
        if(pull)
        {
            var loot=Object.FindObjectsByType<CommonHeadItem>(FindObjectsSortMode.None)
                .Where(x=>InBeam(origin,direction,x.transform.position,range,tuning.beamWidth) && Clear(terrain,origin,x.transform.position))
                .OrderBy(x=>((Vector2)x.transform.position-origin).sqrMagnitude).ThenBy(x=>x.NetworkId)
                .Take(Mathf.Max(0,tuning.maximumLoot));
            foreach(var item in loot)item.BeginVacuumPull(owner,tuning.lootPullSeconds,tuning.lootSpeed*Mathf.Lerp(.5f,1,Mathf.Clamp01(charge)));
        }
        return affected;
    }
    public static IEnumerator Present(Vector2 origin,Vector2 direction,float charge,bool pull,ObjectHeadVacuumDefinition tuning)
    {
        if(tuning==null || ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)yield break;
        direction=direction.normalized;var side=new Vector2(-direction.y,direction.x);
        var terrain=Object.FindAnyObjectByType<TerrainManager>();float range=tuning.Range(charge);
        if(terrain!=null && terrain.TryCheckTerrainHit(origin,origin+direction*range,out var hit))range=Vector2.Distance(origin,hit.point);
        var root=new GameObject("VacuumWind");var material=new Material(Shader.Find("Sprites/Default"));
        var lines=new LineRenderer[Mathf.Clamp(tuning.windStreaks,1,32)];
        var outlines=new LineRenderer[lines.Length];var cues=ObjectHeadActionCues.Load();
        for(int i=0;i<lines.Length;i++)
        {
            var go=new GameObject("WindStreak");go.transform.SetParent(root.transform);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=3;line.useWorldSpace=true;
            line.startWidth=tuning.windWidth;line.endWidth=tuning.windWidth*.3f;line.sortingOrder=35;lines[i]=line;
            var ink=new GameObject("InkEdge");ink.transform.SetParent(root.transform);var outline=ink.AddComponent<LineRenderer>();
            outline.sharedMaterial=material;outline.positionCount=3;outline.useWorldSpace=true;outline.startWidth=tuning.windWidth*1.8f;outline.endWidth=tuning.windWidth*.6f;outline.sortingOrder=34;outlines[i]=outline;
        }
        try
        {
            float duration=Mathf.Max(.05f,tuning.effectSeconds);
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                for(int i=0;i<lines.Length;i++)
                {
                    float progress=Mathf.Repeat(t/duration*2+i*.618f,1);if(pull)progress=1-progress;
                    float lateral=(Mathf.Repeat(i*.381f,1)-.5f)*tuning.beamWidth;
                    Vector2 p=origin+direction*(progress*range)+side*lateral;
                    float length=(cues?.windStreakLength??.55f)*(pull?-1:1);
                    Vector2 mid=p+direction*length*.5f+side*(cues?.windBend??.07f);
                    lines[i].SetPosition(0,p);lines[i].SetPosition(1,mid);lines[i].SetPosition(2,p+direction*length);
                    for(int n=0;n<3;n++)outlines[i].SetPosition(n,lines[i].GetPosition(n));
                    var color=tuning.windColor;color.a*=Mathf.Sin(Mathf.PI*t/duration);lines[i].startColor=lines[i].endColor=color;
                    outlines[i].startColor=outlines[i].endColor=new Color(0,0,0,color.a);
                }
                yield return null;
            }
        }
        finally {Object.Destroy(root);Object.Destroy(material);}
    }
}

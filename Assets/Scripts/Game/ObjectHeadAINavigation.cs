using UnityEngine;

/// <summary>Terrain-only local movement probe. Uses the character's real jump parameters, never teleports.</summary>
public sealed class ObjectHeadAINavigation
{
    private readonly TurnCharacterController actor;private readonly TerrainManager terrain;
    private readonly ObjectHeadAITuning tuning;private readonly ObjectHeadContent content;
    private readonly Vector2 extent;private float nextJump;private float airborneDirection,landingX;private bool jumping;
    public int Jumps {get;private set;}
    public ObjectHeadAINavigation(TurnCharacterController actor,TerrainManager terrain)
    {this.actor=actor;this.terrain=terrain;tuning=ObjectHeadAITuning.Load();content=ObjectHeadContent.Load();extent=actor.GetComponent<Collider2D>().bounds.extents;}
    public bool Clear(Vector2 center)
    {
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
            if(terrain.IsSolidWorld(center+new Vector2(x*(extent.x+.035f),y*(extent.y-.04f))))return false;
        return true;
    }
    public bool GroundAt(float x,float referenceY,out Vector2 point)
    {
        point=default;
        float rise=actor.NavigationJumpSpeed*actor.NavigationJumpSpeed/(2*Mathf.Max(.1f,actor.NavigationHeldGravity));
        if(!terrain.TryCheckTerrainHit(new Vector2(x,referenceY+rise),new Vector2(x,referenceY-extent.y-content.aiMaxSafeDrop),out var hit))return false;
        point=hit.point+Vector2.up*(extent.y+.07f);
        return hit.point.y>terrain.TerrainOriginWorld.y+.6f && Clear(point);
    }
    public bool TryJump(float direction,out Vector2 landing)
    {
        landing=default;Vector2 start=actor.GetComponent<Collider2D>().bounds.center,p=start;
        float vy=actor.NavigationJumpSpeed,step=tuning.jumpTraceStep;
        for(float t=0;t<tuning.jumpTraceSeconds;t+=step)
        {
            vy-= (vy>0?actor.NavigationHeldGravity:actor.NavigationGravity)*step;
            Vector2 next=p+new Vector2(direction*actor.NavigationMoveSpeed,vy)*step;
            if(!Clear(next))
            {
                if(vy>=0)return false;
                if(!terrain.TryCheckTerrainHit(new Vector2(next.x,p.y),new Vector2(next.x,next.y-extent.y-.15f),out var hit))return false;
                landing=hit.point+Vector2.up*(extent.y+.07f);
                return hit.point.y>terrain.TerrainOriginWorld.y+.6f && landing.y>=start.y-content.aiMaxSafeDrop &&
                    Mathf.Abs(landing.x-start.x)>.5f && Clear(landing);
            }
            p=next;
            if(p.y<start.y-content.aiMaxSafeDrop-extent.y)return false;
        }
        return false;
    }
    public bool Step(float direction)
    {
        if(actor==null||terrain==null||Mathf.Abs(direction)<.1f){Stop();return false;}
        direction=Mathf.Sign(direction);
        if(jumping && Time.time<=nextJump-content.aiJumpCooldown+.15f)
        {actor.SetNetworkInput(airborneDirection,true);return true;}
        if(jumping && !actor.IsGrounded)
        {
            float remaining=(landingX-actor.transform.position.x)*airborneDirection;
            actor.SetNetworkInput(remaining>.12f?airborneDirection:0,true);return true;
        }
        if(jumping && Time.time>nextJump-content.aiJumpCooldown+.15f)jumping=false;
        Vector2 p=actor.GetComponent<Collider2D>().bounds.center;
        float ahead=extent.x+content.aiGroundLookAhead;
        bool floor=terrain.TryCheckTerrainHit(p+new Vector2(direction*ahead,.15f),p+new Vector2(direction*ahead,-extent.y-content.aiMaxSafeDrop),out var ground);
        bool blocked=!Clear(p+Vector2.right*direction*(extent.x+.18f));
        bool stepUp=floor && ground.point.y>p.y-extent.y+tuning.walkStepHeight;
        if(actor.IsGrounded && (blocked || !floor || stepUp))
        {
            if(Time.time>=nextJump && TryJump(direction,out var landing))
            {
                jumping=true;airborneDirection=direction;landingX=landing.x;nextJump=Time.time+content.aiJumpCooldown;Jumps++;
                actor.SetNetworkInput(direction,true,true);return true;
            }
            Stop();return false;
        }
        if(!floor && actor.IsGrounded){Stop();return false;}
        actor.SetNetworkInput(direction,jumping);return true;
    }
    public bool CanStartToward(float direction)
    {
        if(actor==null || terrain==null || Mathf.Abs(direction)<.1f)return false;
        if(!actor.IsGrounded)return true;
        direction=Mathf.Sign(direction);
        Vector2 center=actor.GetComponent<Collider2D>().bounds.center;
        float ahead=extent.x+content.aiGroundLookAhead;
        bool floor=terrain.TryCheckTerrainHit(center+new Vector2(direction*ahead,.15f),
            center+new Vector2(direction*ahead,-extent.y-content.aiMaxSafeDrop),out var ground);
        bool blocked=!Clear(center+Vector2.right*direction*(extent.x+.18f));
        bool stepUp=floor && ground.point.y>center.y-extent.y+tuning.walkStepHeight;
        return (!blocked && floor && !stepUp) || TryJump(direction,out _);
    }
    public void Stop(){if(actor!=null)actor.SetNetworkInput(0,false);}
}

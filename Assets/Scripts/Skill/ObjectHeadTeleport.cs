using UnityEngine;

/// <summary>Authority chooses the nearest clear destination; never destroys terrain to make room.</summary>
public static class ObjectHeadTeleport
{
    public static bool TryDestination(CharacterCombat actor,TerrainManager terrain,Vector2 impact,out Vector2 destination)
    {
        destination=actor!=null?(Vector2)actor.transform.position:impact;
        if(actor==null || terrain==null)return false;
        var collider=actor.GetComponent<Collider2D>();
        var bounds=collider.bounds;var ext=(Vector2)bounds.extents;
        var offset=(Vector2)bounds.center-(Vector2)actor.transform.position;
        var config=ObjectHeadPresentation.Load();
        float range=config!=null?config.teleportSearchRadius:2;
        float gap=config!=null?config.teleportClearance:.12f;
        float step=1f/terrain.PixelsPerUnit;
        var area=terrain.GetTerrainBounds();
        var water=Object.FindAnyObjectByType<WaterZone>();
        float minimum=water!=null?water.GetComponent<Collider2D>().bounds.max.y:area.min.y;
        // Search small concentric rings, up first. No top-of-map ray that teleports through a cave roof.
        for(float radius=0;radius<=range;radius+=Mathf.Max(step*4,.125f))
        for(int i=0;i<(radius==0?1:32);i++)
        {
            float angle=Mathf.PI*.5f+i*Mathf.PI*2/32;
            Vector2 center=impact+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            if(center.y-ext.y<=minimum+gap || center.x-ext.x<area.min.x || center.x+ext.x>area.max.x || center.y+ext.y>area.max.y)continue;
            bool clear=true;
            for(float x=-ext.x-gap;x<=ext.x+gap && clear;x+=step)
            for(float y=-ext.y-gap;y<=ext.y+gap;y+=step)
                if(terrain.IsSolidWorld(center+new Vector2(x,y))){clear=false;break;}
            if(!clear)continue;
            foreach(var hit in Physics2D.OverlapBoxAll(center,(ext+Vector2.one*gap)*2,0))
            {
                var other=hit.GetComponentInParent<CharacterCombat>();
                if(other!=null && other!=actor && !other.IsDead){clear=false;break;}
            }
            if(clear){destination=center-offset;return true;}
        }
        return false;
    }
    public static bool Apply(CharacterCombat actor,TerrainManager terrain,Vector2 impact)
    {
        if(!ObjectHeadCommonAuthority.CanWrite || !TryDestination(actor,terrain,impact,out var destination))return false;
        ObjectHeadPresentation.Impact(110,actor.transform.position,1);
        var body=actor.GetComponent<Rigidbody2D>();body.position=destination;body.linearVelocity=Vector2.zero;body.angularVelocity=0;
        actor.transform.position=destination;Physics2D.SyncTransforms();
        ObjectHeadPresentation.Impact(110,destination,1);
        return true;
    }
}

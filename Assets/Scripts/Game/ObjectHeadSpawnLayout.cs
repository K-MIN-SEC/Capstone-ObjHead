using System;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class ObjectHeadSpawnSeat { public Transform[] characterSlots; }
[Serializable] public sealed class ObjectHeadModeSpawnLayout { public ObjectHeadMatchMode mode; public ObjectHeadSpawnSeat[] seats; }

/// <summary>Designer-authored spawn stations, validated against the terrain mask.</summary>
public sealed class ObjectHeadSpawnLayout : MonoBehaviour
{
    public ObjectHeadModeSpawnLayout[] layouts;
    [Min(.01f)] public float heightTolerance = .18f;
    [Min(.01f)] public float horizontalTolerance = .03f;
    public bool randomizeSeats = true;
    [Tooltip("Use the floor immediately below each marker, including cave floors, rather than the highest surface at that X.")]
    public bool useMarkerLocalSurface;
    [Header("Authored spawn distribution checks (world units)")]
    [Min(.01f)] public float maximumSpawnHeightSpread = .15f;
    [Min(.01f)] public float maximumPlayerMeanHeightDifference = .15f;
    public bool Place(TerrainManager terrain, TurnCharacterController[] characters, int seed)
    {
        var rule = ObjectHeadMatchRules.Current;
        var layout = layouts.FirstOrDefault(l => l.mode == rule.mode);
        if (layout == null || layout.seats.Length != rule.players) throw new InvalidOperationException("Missing spawn layout: " + rule.mode);
        int[] seats = (int[])rule.spawnSeats.Clone();
        var random = new System.Random(seed);
        if (randomizeSeats && rule.mode == ObjectHeadMatchMode.FreeForAll)
            for (int i = seats.Length - 1; i > 0; i--) { int j = random.Next(i + 1); (seats[i], seats[j]) = (seats[j], seats[i]); }
        else if (randomizeSeats && random.Next(2) == 1)
            for (int i = 0; i < seats.Length; i++) seats[i] = seats.Length - 1 - seats[i];
        foreach (var character in characters)
        {
            var member = character.GetComponent<ObjectHeadTeamMember>();
            var slots = layout.seats[seats[member.PlayerIndex - 1]].characterSlots;
            if (member.TeamSlotIndex > slots.Length) throw new InvalidOperationException("Missing character spawn slot");
            Vector2 marker = slots[member.TeamSlotIndex - 1].position;
            var request = new TerrainCharacterSpawnRequest {
                minWorldX=marker.x-horizontalTolerance,maxWorldX=marker.x+horizontalTolerance,
                colliderExtents=character.GetComponent<Collider2D>().bounds.extents,
                spawnLiftWorld=.06f,waterPaddingWorld=1.5f,maximumSurfaceHeightDifference=heightTolerance,
                clearanceSampleStepWorld=.18f,randomAttempts=4,
                exclusions=new System.Collections.Generic.List<TerrainSpawnExclusion>() };
            Vector2 point;
            bool valid=useMarkerLocalSurface
                ? TryLocalSurface(terrain,marker,request.colliderExtents,heightTolerance,out point)
                : terrain.FindValidCharacterSpawn(request,new System.Random(seed),out point,out _);
            if (!valid || Mathf.Abs(point.y-marker.y)>heightTolerance)
                throw new InvalidOperationException($"Unsafe spawn marker: {slots[member.TeamSlotIndex-1].name}, marker={marker}, extents={request.colliderExtents}, candidate={point}");
            character.transform.position=point;
            character.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        }
        return true;
    }
    public static bool TryLocalSurface(TerrainManager terrain,Vector2 marker,Vector2 extents,float tolerance,out Vector2 point)
    {
        point=default;
        float foot=marker.y-extents.y-.06f;
        if(!terrain.TryCheckTerrainHit(new Vector2(marker.x,foot+tolerance),new Vector2(marker.x,foot-tolerance),out var hit))return false;
        if(hit.point.y<=terrain.TerrainOriginWorld.y+1.5f)return false;
        float highest=hit.point.y,lowest=hit.point.y;
        for(int i=0;i<5;i++)
        {
            float x=marker.x+Mathf.Lerp(-extents.x-.12f,extents.x+.12f,i/4f);
            if(!terrain.TryCheckTerrainHit(new Vector2(x,hit.point.y+tolerance),new Vector2(x,hit.point.y-tolerance),out var support))return false;
            if(Mathf.Abs(support.point.y-hit.point.y)>tolerance)return false;
            highest=Mathf.Max(highest,support.point.y);lowest=Mathf.Min(lowest,support.point.y);
        }
        if(highest-lowest>tolerance)return false;
        // Lift above the highest pixel under the footprint, not just the centre of a rough painted roof.
        point=new Vector2(marker.x,highest+extents.y+.06f);
        for(float x=-extents.x;x<=extents.x+.001f;x+=.08f)
        for(float y=-extents.y+.02f;y<=extents.y+.001f;y+=.08f)
            if(terrain.IsSolidWorld(point+new Vector2(x,y)))return false;
        return true;
    }

    public bool HasValidHeightDistribution(TurnCharacterController[] characters)
    {
        if(characters==null||characters.Length==0)return false;
        float spread=characters.Max(c=>c.transform.position.y)-characters.Min(c=>c.transform.position.y);
        var averages=characters.GroupBy(c=>c.GetComponent<ObjectHeadTeamMember>().PlayerIndex)
            .Select(group=>group.Average(c=>c.transform.position.y)).ToArray();
        return spread<=maximumSpawnHeightSpread && averages.Max()-averages.Min()<=maximumPlayerMeanHeightDifference;
    }
    private void OnDrawGizmosSelected()
    {
        if(layouts==null)return;
        foreach(var layout in layouts)foreach(var seat in layout.seats)foreach(var marker in seat.characterSlots)
        {
            if(marker==null)continue;
            Gizmos.color=Color.cyan;Gizmos.DrawWireCube(marker.position,new Vector3(.8f,1.2f,.1f));
        }
    }
}

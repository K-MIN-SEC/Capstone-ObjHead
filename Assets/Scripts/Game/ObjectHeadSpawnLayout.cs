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
            if (!terrain.FindValidCharacterSpawn(request,new System.Random(seed),out Vector2 point,out _) || Mathf.Abs(point.y-marker.y)>heightTolerance)
                throw new InvalidOperationException("Unsafe spawn marker: "+slots[member.TeamSlotIndex-1].name);
            character.transform.position=point;
            character.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        }
        return true;
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

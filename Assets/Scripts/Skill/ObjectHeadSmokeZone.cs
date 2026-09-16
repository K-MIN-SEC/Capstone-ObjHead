using System.Collections.Generic;
using UnityEngine;

public sealed class ObjectHeadSmokeZone:MonoBehaviour
{
    private static readonly List<ObjectHeadSmokeZone> zones=new List<ObjectHeadSmokeZone>();
    private float radius;
    private int expiresRound;
    private TurnManager turns;
    public static bool Contains(Vector2 point)
    {foreach(var z in zones)if(z!=null && Vector2.Distance(point,z.transform.position)<=z.radius)return true;return false;}
    public static void Create(Vector2 point,float radius,int rounds,TurnManager manager)
    {
        var go=new GameObject("SmokeZone");go.transform.position=point;var z=go.AddComponent<ObjectHeadSmokeZone>();
        z.radius=radius;z.turns=manager;z.expiresRound=(manager!=null?manager.RoundSerial:0)+Mathf.Max(1,rounds);zones.Add(z);
        var sprite=ObjectHeadPresentation.Load()?.smokeSprite;
        if(sprite!=null){var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=35;r.color=new Color(1,1,1,.65f);go.transform.localScale=new Vector3(radius*2/sprite.bounds.size.x,radius*1.3f/sprite.bounds.size.y,1);}
    }
    private void Update(){if(turns!=null && turns.RoundSerial>=expiresRound)Destroy(gameObject);}
    private void OnDestroy(){zones.Remove(this);}
}

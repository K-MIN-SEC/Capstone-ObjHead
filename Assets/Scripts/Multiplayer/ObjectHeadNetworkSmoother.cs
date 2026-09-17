using UnityEngine;

/// <summary>Presentation smoothing for authoritative character snapshots.</summary>
[DisallowMultipleComponent]
public sealed class ObjectHeadNetworkSmoother : MonoBehaviour
{
    [SerializeField,Min(1f)]private float followSharpness=16f;
    [SerializeField,Min(.1f)]private float snapDistance=2.5f;
    [SerializeField,Range(0f,.2f)]private float predictionSeconds=.06f;
    private Rigidbody2D body;
    private Vector2 targetPosition,targetVelocity;
    private bool hasSnapshot;

    private void Awake(){body=GetComponent<Rigidbody2D>();}
    public void Push(Vector2 position,Vector2 velocity)
    {
        if(!Finite(position)||!Finite(velocity))return;
        targetPosition=position;targetVelocity=velocity;
        Vector2 current=body!=null?body.position:(Vector2)transform.position;
        if(!hasSnapshot || Vector2.Distance(current,position)>=snapDistance)
        {
            if(body!=null){body.position=position;body.linearVelocity=velocity;}
            else transform.position=new Vector3(position.x,position.y,transform.position.z);
        }
        hasSnapshot=true;
    }
    private void FixedUpdate()
    {
        if(!hasSnapshot || body==null || (body.constraints&RigidbodyConstraints2D.FreezePosition)==RigidbodyConstraints2D.FreezePosition)return;
        Vector2 predicted=targetPosition+targetVelocity*predictionSeconds;
        float blend=1f-Mathf.Exp(-followSharpness*Time.fixedDeltaTime);
        body.MovePosition(Vector2.Lerp(body.position,predicted,blend));
        body.linearVelocity=Vector2.Lerp(body.linearVelocity,targetVelocity,blend);
    }
    private static bool Finite(Vector2 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y);
}

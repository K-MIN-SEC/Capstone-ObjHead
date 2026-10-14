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
    private float receivedAt;

    private void Awake(){body=GetComponent<Rigidbody2D>();}
    public void Push(Vector2 position,Vector2 velocity)
    {
        if(!Finite(position)||!Finite(velocity))return;
        targetPosition=position;targetVelocity=velocity;
        receivedAt=Time.unscaledTime;
        Vector2 current=body!=null?body.position:(Vector2)transform.position;
        bool frozen = body != null && (body.constraints & RigidbodyConstraints2D.FreezePosition) == RigidbodyConstraints2D.FreezePosition;
        if(!hasSnapshot || frozen || Vector2.Distance(current,position)>=snapDistance)
        {
            if(body!=null){body.position=position;body.linearVelocity=Vector2.zero;}
            else transform.position=new Vector3(position.x,position.y,transform.position.z);
        }
        hasSnapshot=true;
    }
    private void FixedUpdate()
    {
        if(!hasSnapshot || body==null || !body.simulated)return;
        // Never keep predicting a fall forever when the connection stalls.
        float lead=Time.unscaledTime-receivedAt<.25f?predictionSeconds:0;
        Vector2 predicted=targetPosition+targetVelocity*lead;
        float blend=1f-Mathf.Exp(-followSharpness*Time.fixedDeltaTime);
        body.MovePosition(Vector2.Lerp(body.position,predicted,blend));
    }
    private static bool Finite(Vector2 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y);
}

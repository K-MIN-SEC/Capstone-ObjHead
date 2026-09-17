using UnityEngine;

/// <summary>Observes local or replicated movement; never changes a character's physics.</summary>
[DisallowMultipleComponent]
public sealed class ObjectHeadMotionFeedback:MonoBehaviour
{
    private TurnCharacterController controller;
    private CharacterCombat combat;
    private Rigidbody2D body;
    private Collider2D shape;
    private ObjectHeadMicroFeedback config;
    private Vector2 previous;
    private float distance,lastStep,airTime,fallSpeed;
    private bool ready,airborne;
    private void Awake()
    {controller=GetComponent<TurnCharacterController>();combat=GetComponent<CharacterCombat>();body=GetComponent<Rigidbody2D>();shape=GetComponent<Collider2D>();config=ObjectHeadMicroFeedback.Load();}
    private void LateUpdate()
    {
        if(controller==null || body==null || config==null || !config.effectsEnabled || combat?.IsDead==true || ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true){ready=false;return;}
        Vector2 position=transform.position;
        if(!ready){previous=position;ready=true;return;}
        float moved=Vector2.Distance(previous,position);previous=position;
        if(moved>2){airTime=0;airborne=false;fallSpeed=0;distance=0;return;} // Spawn/teleport, not a footstep.
        Vector2 foot=shape!=null?new Vector2(shape.bounds.center.x,shape.bounds.min.y):position;
        if(!controller.IsGrounded)
        {
            airTime+=Time.deltaTime;fallSpeed=Mathf.Max(fallSpeed,-body.linearVelocity.y);
            if(!airborne && airTime>=config.airborneGrace)
            {airborne=true;if(body.linearVelocity.y>1)ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Jump,foot,Vector2.up);}
            distance=0;return;
        }
        if(airborne && fallSpeed>=config.landingSpeed)
            ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Land,foot,Vector2.up,Mathf.Clamp(fallSpeed/5,.6f,1.5f));
        airTime=0;fallSpeed=0;airborne=false;
        distance+=moved;
        if(Mathf.Abs(body.linearVelocity.x)>.2f && distance>=config.footstepDistance && Time.time-lastStep>=config.footstepInterval)
        {distance=0;lastStep=Time.time;ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Step,foot,new Vector2(-Mathf.Sign(body.linearVelocity.x),.6f));}
    }
}

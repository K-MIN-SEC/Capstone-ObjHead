using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Captivity is authoritative gameplay state; cage art is a replaceable prefab.
[DisallowMultipleComponent]
public sealed class ObjectHeadCaptivity : MonoBehaviour
{
    public int RemainingOwnTurns {get;private set;}
    public bool IsCaptured=>RemainingOwnTurns>0;
    public bool IsSkippingTurn=>IsCaptured && turns!=null && turns.CurrentCharacter==controller && turns.TurnSerial==lastTurn;
    private CharacterCombat combat;
    private TurnCharacterController controller;
    private TurnManager turns;
    private ObjectHeadCaptivityDefinition definition;
    private ObjectHeadCageVisual visual;
    private Coroutine tick;
    private int lastTurn=-1;
    private int damage;
    private RigidbodyConstraints2D originalConstraints;
    private bool frozen;
    public static bool Captured(Component target)=>target!=null && target.GetComponent<ObjectHeadCaptivity>()?.IsCaptured==true;
    private void Awake()
    {
        combat=GetComponent<CharacterCombat>();controller=GetComponent<TurnCharacterController>();
        turns=FindAnyObjectByType<TurnManager>();definition=ObjectHeadCaptivityDefinition.Load();
        if(turns!=null)turns.TurnStarted+=TurnStarted;
        if(combat!=null)combat.Died+=Died;
    }
    private void OnDestroy()
    {
        RestorePhysics();
        if(turns!=null)turns.TurnStarted-=TurnStarted;
        if(combat!=null)combat.Died-=Died;
    }
    public static int CaptureArea(Vector2 point,float radius)
    {
        if(!ObjectHeadCommonAuthority.CanWrite || radius<=0)return 0;
        var unique=new HashSet<CharacterCombat>();int count=0;
        foreach(var hit in Physics2D.OverlapCircleAll(point,radius))
        {
            var target=hit.GetComponentInParent<CharacterCombat>();
            if(target==null || target.IsDead || !unique.Add(target))continue;
            var status=target.GetComponent<ObjectHeadCaptivity>()??target.gameObject.AddComponent<ObjectHeadCaptivity>();
            if(status.Capture())count++;
        }
        return count;
    }
    public bool Capture()
    {
        if(!ObjectHeadCommonAuthority.CanWrite || combat==null || combat.IsDead || IsCaptured || definition==null)return false;
        damage=definition.Damage;RemainingOwnTurns=definition.Turns;
        Enter();return true;
    }
    private void Enter()
    {
        controller.SetControlEnabled(false);
        var body=GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.zero;body.angularVelocity=0;
        originalConstraints=body.constraints;body.constraints=RigidbodyConstraints2D.FreezeAll;frozen=true;
        GetComponent<PowerChargeController>()?.CancelCharge();
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker!=true && definition?.cagePrefab!=null)
            visual=Instantiate(definition.cagePrefab,transform,false);
    }
    public static void ApplyReplica(TurnCharacterController target,int remaining)
    {
        if(ObjectHeadCommonAuthority.CanWrite)return;
        var status=target.GetComponent<ObjectHeadCaptivity>();
        if(status==null && remaining<=0)return;
        if(status==null)status=target.gameObject.AddComponent<ObjectHeadCaptivity>();
        bool wasCaptured=status.IsCaptured;
        int previous=status.RemainingOwnTurns;
        status.RemainingOwnTurns=Mathf.Clamp(remaining,0,2);
        if(!wasCaptured && status.IsCaptured)status.Enter();
        else if(wasCaptured && !status.IsCaptured)status.ReleaseVisual();
        else if(status.IsCaptured && remaining<previous)status.visual?.Pulse();
    }
    private void TurnStarted(TurnCharacterController current)
    {
        if(!ObjectHeadCommonAuthority.CanWrite || !IsCaptured || current!=controller || turns.TurnSerial==lastTurn)return;
        lastTurn=turns.TurnSerial;tick=StartCoroutine(SkipTurn(lastTurn));
    }
    private IEnumerator SkipTurn(int serial)
    {
        visual?.Pulse();
        yield return new WaitForSeconds(definition.skippedTurnPresentationSeconds);
        if(!IsCaptured || combat.IsDead || turns.TurnSerial!=serial){tick=null;yield break;}
        combat.ApplyCaptivityDamage(damage);
        if(!combat.IsDead)
        {
            RemainingOwnTurns--;
            if(!IsCaptured)ReleaseVisual();
            turns.CompleteCapturedTurn(controller,serial);
        }
        tick=null;
    }
    private void Died(CharacterCombat target)
    {
        RemainingOwnTurns=0;ReleaseVisual();
    }
    private void ReleaseVisual()
    {
        RestorePhysics();
        visual?.Release();visual=null;
    }
    private void RestorePhysics()
    {
        if(!frozen)return;
        var body=GetComponent<Rigidbody2D>();if(body!=null)body.constraints=originalConstraints;
        frozen=false;
    }
}

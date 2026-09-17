using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ObjectHeadMicroSmoke:MonoBehaviour
{
    private bool failed;
    private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadMicroSmoke"))return;
        var root=new GameObject("MicroSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadMicroSmoke>();
    }
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[MICRO_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private void Check(bool value,string label){Debug.Log("[MICRO_DETAIL] "+label+"="+value);if(!value)failed=true;}
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(.4f);
        var button=FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.IsInteractable());
        var feedback=button.GetComponent<ObjectHeadButtonFeedback>();
        Check(feedback!=null,"title button authored feedback");
        if(feedback!=null)
        {
            var before=button.transform.localScale;int clicks=0;button.onClick.AddListener(()=>clicks++);
            feedback.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.15f);
            Check(button.transform.localScale.x>before.x && clicks==0,"hover responds without triggering click");
            feedback.enabled=false;Check(Vector3.Distance(button.transform.localScale,before)<.001f,"disable restores authored scale");feedback.enabled=true;
        }
        var content=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=content.maps[0].id,mapSeed=916,
            characterSpawnSeed=916,startingPlayerIndex=1,players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment{
                playerIndex=p,allianceId=p,characters=content.DefaultSelection(2)}).ToArray()});
        SceneManager.LoadScene(content.maps[0].sceneName);yield return new WaitForSeconds(.7f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        foreach(var actor in turns.Characters){actor.UseNetworkInput=true;actor.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
        var terrain=FindAnyObjectByType<TerrainManager>();var camera=Camera.main;
        camera.GetComponent<ObjectHeadCameraController>().enabled=false;
        Vector2 surface=default;bool found=false;
        for(float x=-20;x<20;x+=.5f)
            if(terrain.FindTerrainSurface(x,out var p) && terrain.FindTerrainSurface(x+.4f,out var q) && Mathf.Abs(p.y-q.y)<.08f){surface=p;found=true;break;}
        Check(found,"flat land fixture found");
        camera.transform.position=new Vector3(surface.x,surface.y+1,-10);camera.orthographicSize=3.5f;
        yield return new WaitForSeconds(1);
        var config=ObjectHeadMicroFeedback.Load();
        Check(config!=null && config.profiles.Length>=8 && config.profiles.All(p=>p.sprite!=null),"small feedback profiles have art");
        Check(ObjectHeadMicroParticles.Capacity==config.particleBudget,"fixed prewarmed capacity");
        var pool=FindAnyObjectByType<ObjectHeadMicroParticles>();
        Check(pool.GetComponentsInChildren<Collider2D>().Length==0 && pool.GetComponentsInChildren<Rigidbody2D>().Length==0,"particles have no gameplay physics");
        int emitted=ObjectHeadMicroParticles.TotalEmitted;
        Vector2 crater=surface+Vector2.down*.25f;
        Check(terrain.DestroyCircle(crater,35),"actual terrain removed");
        Check(ObjectHeadMicroParticles.TotalEmitted>emitted,"terrain operation produces debris");
        emitted=ObjectHeadMicroParticles.TotalEmitted;
        Check(!terrain.DestroyCircle(crater,35) && ObjectHeadMicroParticles.TotalEmitted==emitted,"unchanged terrain creates no duplicate debris");
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");
        string folder=at>=0?args[at+1]:null;
        yield return new WaitForSeconds(.12f);
        if(folder!=null){Directory.CreateDirectory(folder);yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"micro-terrain.png"));}
        // Authored debris now lives up to 1.35s; a fixed one-second assertion tests the wrong contract.
        float maximumLifetime=config.profiles.Where(p=>p.cue==ObjectHeadMicroCue.Debris || p.cue==ObjectHeadMicroCue.Dust)
            .Max(p=>Mathf.Max(p.lifetime.x,p.lifetime.y));
        Debug.Log("[MICRO_DETAIL] authored maximum terrain-particle lifetime="+maximumLifetime);
        yield return new WaitForSeconds(maximumLifetime+.1f);
        Check(ObjectHeadMicroParticles.ActiveCount==0,"particles expire");
        var rng=UnityEngine.Random.state;
        float expected=UnityEngine.Random.value;UnityEngine.Random.state=rng;
        ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Debris,surface,Vector2.up);
        Check(UnityEngine.Random.value==expected,"cosmetic random does not consume gameplay random");UnityEngine.Random.state=rng;
        emitted=ObjectHeadMicroParticles.TotalEmitted;
        for(int i=0;i<100;i++)ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Debris,surface,Vector2.up,2);
        Check(ObjectHeadMicroParticles.TotalEmitted-emitted<=config.emissionsPerFrame && ObjectHeadMicroParticles.ActiveCount<=config.particleBudget,"burst and pool budgets enforced");
        yield return new WaitForSeconds(1);
        // Real falling actor, with all other actors held still.
        terrain.CreateCircle(crater,40,TerrainType.Created,null);
        var moving=turns.CurrentCharacter;
        moving.transform.position=surface+Vector2.up*3;
        var rigid=moving.GetComponent<Rigidbody2D>();rigid.constraints=RigidbodyConstraints2D.FreezeRotation;
        rigid.linearVelocity=Vector2.down*2;Physics2D.SyncTransforms();
        emitted=ObjectHeadMicroParticles.TotalEmitted;
        yield return new WaitForSeconds(2);
        Check(ObjectHeadMicroParticles.TotalEmitted>emitted && moving.IsGrounded,"real landing emits dust");
        rigid.constraints=RigidbodyConstraints2D.FreezeAll;
        emitted=ObjectHeadMicroParticles.TotalEmitted;yield return new WaitForSeconds(.5f);
        Check(ObjectHeadMicroParticles.TotalEmitted==emitted,"standing still does not spam footsteps");
        var canvas=FindAnyObjectByType<ObjectHeadBattleScreen>();
        Check(canvas.endTurnButton.GetComponent<ObjectHeadButtonFeedback>()!=null,"battle button feedback authored");
        foreach(var ui in FindObjectsByType<Canvas>(FindObjectsSortMode.None))ui.gameObject.SetActive(false);
        ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Water,surface+Vector2.right*2,Vector2.up,1.5f);
        ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Hit,surface+Vector2.up*1.2f,Vector2.up);
        yield return new WaitForSeconds(.1f);
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"micro-details.png"));
        config.effectsEnabled=false;yield return null;
        Check(ObjectHeadMicroParticles.ActiveCount==0 && ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Hit,surface,Vector2.up)==0,"effects can be disabled without touching gameplay");
        config.effectsEnabled=true;
        SceneManager.LoadScene("ObjectHeadTitle");yield return new WaitForSeconds(.3f);
        Check(ObjectHeadMicroParticles.ActiveCount==0 && FindAnyObjectByType<ObjectHeadMicroParticles>()==null,"scene unload releases pool");
        Debug.Log(failed?"[MICRO_FAIL]":"[MICRO_PASS] terrain, motion, budgets, RNG isolation, UI feedback and lifecycle");
        Application.Quit(failed?2:0);
    }
}

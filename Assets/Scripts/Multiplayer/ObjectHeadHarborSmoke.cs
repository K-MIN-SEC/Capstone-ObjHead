using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in functional and actual-renderer review of the revised harbor.</summary>
public sealed class ObjectHeadHarborSmoke:MonoBehaviour
{
    private bool failed;private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(!Environment.GetCommandLineArgs().Contains("-objectHeadHarborSmoke"))return;var go=new GameObject("HarborSmoke");DontDestroyOnLoad(go);go.AddComponent<ObjectHeadHarborSmoke>();}
    private void Awake(){deadline=Time.realtimeSinceStartup+90;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[HARBOR_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private void Check(bool condition,string label){Debug.Log("[HARBOR_DETAIL] "+label+"="+condition);if(!condition)failed=true;}
    private IEnumerator Start()
    {
        yield return null;var catalog=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.FreeForAll,playerCount=4,mapId="twin_citadels",mapSeed=917,characterSpawnSeed=917,startingPlayerIndex=1,
            players=Enumerable.Range(1,4).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=catalog.DefaultSelection(4)}).ToArray()});
        SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.6f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        if(turns.Characters.Length!=8){Debug.LogError("[HARBOR_FAIL] map did not spawn eight characters");Application.Quit(2);yield break;}
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
        var terrain=FindAnyObjectByType<TerrainManager>();var layout=FindAnyObjectByType<ObjectHeadSpawnLayout>();
        Check(turns.Characters.Length==8 && layout.HasValidHeightDistribution(turns.Characters),"eight distributed safe starts");
        float gap=turns.Characters.Max(c=>c.transform.position.y)-turns.Characters.Min(c=>c.transform.position.y);
        Check(gap>4 && gap<7,"starts no longer form one flat row");
        Check(turns.Characters.All(c=>c.transform.position.y>4),"no starting character sealed in a lower cave");
        bool caveClear=true;
        for(float x=8;x<=12.5f;x+=.1f)for(float y=1.2f;y<=2.25f;y+=.1f)caveClear&=!terrain.IsSolidWorld(new Vector2(x,y));
        Check(caveClear,"right cave mouth has full character clearance");
        Check(terrain.IsSolidWorld(new Vector2(-8,-3)) && terrain.IsSolidWorld(new Vector2(8,-3)),"thick continuous land mass, not a detached lower strip");
        var scenery=GameObject.Find("Distant Scenery - non playable");Check(scenery!=null && scenery.GetComponentsInChildren<Collider2D>().Length==0,"distant scenery remains nonphysical");
        var cam=Camera.main;cam.GetComponent<ObjectHeadCameraController>().enabled=false;Screen.SetResolution(1600,900,false);yield return new WaitForSeconds(.2f);
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");string folder=at>=0?args[at+1]:null;
        cam.transform.position=new Vector3(0,4,-10);cam.orthographicSize=13.4f;
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"harbor-0917-play.png"));
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"harbor-0917-overview.png"));
        cam.transform.position=new Vector3(-12,6,-10);cam.orthographicSize=7;
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"harbor-0917-detail.png"));
        var actor=turns.CurrentCharacter;var body=actor.GetComponent<Rigidbody2D>();
        actor.transform.position=new Vector3(8.5f,1.35f,0);body.constraints=RigidbodyConstraints2D.FreezeRotation;body.linearVelocity=Vector2.zero;
        yield return new WaitForSeconds(.3f);float start=actor.transform.position.x;
        for(float until=Time.time+1.25f;Time.time<until;){actor.SetNetworkInput(1,false);yield return null;}
        actor.SetNetworkInput(0,false);
        Check(actor.transform.position.x>start+2.5f && !actor.GetComponent<CharacterCombat>().IsDead,"character physically walks through cave entrance");
        actor.transform.position=new Vector3(-17.0f,1.8f,0);body.linearVelocity=Vector2.zero;
        yield return new WaitForSeconds(.3f);start=actor.transform.position.x;
        for(float until=Time.time+1.1f;Time.time<until;){actor.SetNetworkInput(1,false);yield return null;}
        actor.SetNetworkInput(0,false);
        Check(actor.transform.position.x>start+2 && !actor.GetComponent<CharacterCombat>().IsDead,"character physically walks inside left cave mouth");
        body.constraints=RigidbodyConstraints2D.FreezeAll;
        bool hit=terrain.TryCheckTerrainHit(new Vector2(-13,15),new Vector2(-13,6),out var roof);
        Check(hit,"boathouse roof has terrain collision");
        if(hit){Vector2 p=roof.point+Vector2.down*.1f;Check(terrain.IsSolidWorld(p),"roof initially solid");terrain.DestroyCircle(p,18);Check(!terrain.IsSolidWorld(p),"boathouse destructible like ground");}
        Debug.Log(failed?"[HARBOR_FAIL]":"[HARBOR_PASS] safe distributed starts, walkable cave, cohesive terrain, destructible building and render captures");Application.Quit(failed?2:0);
    }
}

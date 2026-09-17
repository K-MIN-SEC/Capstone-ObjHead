using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in tactical regression and actual renderer captures; never runs in normal play.</summary>
public sealed class ObjectHeadTacticsSmoke:MonoBehaviour
{
    private bool failed;
    private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadTacticsSmoke"))return;
        var root=new GameObject("TacticsSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadTacticsSmoke>();
    }
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[TACTICS_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private void Check(bool condition,string message){Debug.Log("[TACTICS_DETAIL] "+message+"="+condition);if(!condition)failed=true;}
    private IEnumerator Start()
    {
        yield return null;
        var content=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Teams,playerCount=4,mapId=content.maps[0].id,mapSeed=916,
            characterSpawnSeed=916,startingPlayerIndex=1,players=Enumerable.Range(1,4).Select(p=>new ObjectHeadPlayerAssignment{
                playerIndex=p,allianceId=p%2==1?1:2,characters=new[]{ObjectHeadCharacterKind.Revolver,ObjectHeadCharacterKind.Bomb}}).ToArray()});
        SceneManager.LoadScene(content.maps[0].sceneName);yield return new WaitForSeconds(.4f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        var terrain=FindAnyObjectByType<TerrainManager>();
        // Isolate the shot fixture from authored towers; map geometry is verified by the map suite.
        terrain.DestroyCircle(new Vector2(0,14),400);
        var actor=turns.CurrentCharacter;
        var enemy=turns.Characters.First(c=>!ObjectHeadAIPlanner.SameTeam(actor,c));
        var ally=turns.Characters.First(c=>c!=actor && ObjectHeadAIPlanner.SameTeam(actor,c));
        foreach(var c in turns.Characters)
        {c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;c.transform.position=new Vector3(-20+c.GetComponent<ObjectHeadTeamMember>().PlayerIndex,22,0);}
        actor.transform.position=new Vector3(-4,14,0);enemy.transform.position=new Vector3(4,14,0);ally.transform.position=new Vector3(-7,14,0);
        Physics2D.SyncTransforms();
        Check(ObjectHeadAIPlanner.SolveArc(new Vector2(8,2),18,9.81f,false,out var low),"low arc reachable");
        Check(ObjectHeadAIPlanner.SolveArc(new Vector2(8,2),18,9.81f,true,out var high) && high.y>low.y,"high arc distinct");
        Check(!ObjectHeadAIPlanner.SolveArc(new Vector2(100,100),6,9.81f,false,out _),"unreachable rejected");
        var planner=new ObjectHeadAIPlanner(actor,turns.Characters,terrain);
        yield return planner.Search(ObjectHeadAIDifficulty.Pro,()=>true);
        Check(planner.Best.valid && planner.Best.score>0,"reachable enemy gets positive legal shot");
        Vector3 planningStart=actor.transform.position;
        var virtualProbe=new ObjectHeadAIPlanner(actor,turns.Characters,terrain,(Vector2)planningStart+Vector2.right);
        yield return virtualProbe.Search(ObjectHeadAIDifficulty.Pro,()=>true,.12f);
        Check(actor.transform.position==planningStart && virtualProbe.Evaluated>0,"candidate position search does not teleport actor");
        var settings=actor.GetComponent<DemoSkillSelector>().GetSkillSettings(0);
        float isolated=planner.Score(settings,enemy.transform.position);
        ally.transform.position=enemy.transform.position+Vector3.right*.2f;Physics2D.SyncTransforms();
        Check(planner.Score(settings,enemy.transform.position)<isolated,"friendly fire lowers utility");
        ally.GetComponent<CharacterCombat>().TakeDamage(30);enemy.GetComponent<CharacterCombat>().TakeDamage(30);
        var healing=actor.GetComponent<DemoSkillSelector>().GetSkillSettings(1);
        float mixed=planner.Score(healing,ally.transform.position);
        enemy.transform.position+=Vector3.right*6;Physics2D.SyncTransforms();
        Check(planner.Score(healing,ally.transform.position)>mixed,"enemy healing lowers utility");
        Check(planner.Score(healing,new Vector2(10,25))==0,"empty healing scores zero");
        // A real collider in the line of fire must block a straight shot.
        enemy.transform.position=new Vector3(4,14,0);ally.transform.position=new Vector3(-7,14,0);
        var wall=new GameObject("TestCover",typeof(BoxCollider2D));wall.transform.position=new Vector3(0,14,0);wall.GetComponent<BoxCollider2D>().size=new Vector2(.8f,4);
        Physics2D.SyncTransforms();
        Check(planner.Trace(settings,Vector2.right,1,out var blocked) && blocked.x<1,"straight shot respects cover");
        Destroy(wall);yield return null;
        var config=ObjectHeadPresentation.Load();
        Check(config.skills.All(p=>p.impactPrefab!=null),"all registered skills have impact art");
        Check(new[]{42,43,108}.All(id=>config.Find(id).warningPrefab!=null),"three deferred abilities have warnings");
        Check(config.groundZoneArtHeight>=.5f && config.groundZoneTerrainOverlap>0,"larger inset zone art");
        Check(terrain.WidthPx/(float)terrain.PixelsPerUnit==44 && terrain.HeightPx/(float)terrain.PixelsPerUnit>=32,"narrower vertical islands");
        var cam=Camera.main;var controller=cam.GetComponent<ObjectHeadCameraController>();
        cam.transform.position=new Vector3(0,14,-10);
        Check(ObjectHeadCameraTuning.Load()!=null,"editable camera preset exists");
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
        controller.enabled=false;cam.orthographicSize=6;
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");
        string directory=at>=0?args[at+1]:null;
        int[] ids={104,51,63,107,11,21,108,41,43};
        for(int i=0;i<ids.Length;i++)ObjectHeadPresentation.PresentConfirmedImpact(ids[i],new Vector2((i%3-1)*5,17-i/3*3),2.5f);
        yield return new WaitForSeconds(.06f);
        if(directory!=null){Directory.CreateDirectory(directory);yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,"tactical-effects.png"));}
        yield return new WaitForSeconds(2);
        Check(FindObjectsByType<ObjectHeadEffectLifetime>(FindObjectsSortMode.None).Length==0,"one-shot effects clean up");
        for(int i=0;i<3;i++)ObjectHeadPresentation.Warning(new[]{42,43,108}[i],new Vector2((i-1)*5,14),1.8f);
        yield return null;
        if(directory!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,"landing-warnings.png"));
        Debug.Log(failed?"[TACTICS_FAIL]":"[TACTICS_PASS] Ballistics, cover, ally safety, healing, VFX and map size");
        Application.Quit(failed?2:0);
    }
}

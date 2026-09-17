using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadEscapeSmoke:MonoBehaviour
{
    private bool failed;private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(!Environment.GetCommandLineArgs().Contains("-objectHeadEscapeSmoke"))return;var root=new GameObject("EscapeSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadEscapeSmoke>();}
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[ESCAPE_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private void Check(bool ok,string label){Debug.Log("[ESCAPE_DETAIL] "+label+"="+ok);if(!ok)failed=true;}
    private IEnumerator Start()
    {
        yield return null;var catalog=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=catalog.maps[0].id,mapSeed=916,characterSpawnSeed=916,startingPlayerIndex=1,
            players=new[]{new ObjectHeadPlayerAssignment{playerIndex=1,allianceId=1,characters=catalog.DefaultSelection(2)},new ObjectHeadPlayerAssignment{playerIndex=2,allianceId=2,characters=catalog.DefaultSelection(2)}}});
        SceneManager.LoadScene(catalog.maps[0].sceneName);yield return new WaitForSeconds(.6f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);var terrain=FindAnyObjectByType<TerrainManager>();var actor=turns.CurrentCharacter;
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;c.transform.position=new Vector3(18,30,0);}
        Check(catalog.maps.Length>=5,"five distinct maps registered");
        Check(terrain.WidthPx/(float)terrain.PixelsPerUnit==44,"narrower playable width");
        var scenery=GameObject.Find("Distant Scenery - non playable");Check(scenery!=null && scenery.GetComponentsInChildren<Collider2D>().Length==0,"scenery has no physical collision");
        terrain.DestroyCircle(new Vector2(-5,27),360);
        for(float x=-13;x<4;x+=.5f)terrain.CreateCircle(new Vector2(x,24),48,TerrainType.Created,null);
        terrain.CreateCircle(new Vector2(-7.4f,25.7f),23,TerrainType.Created,null);
        actor.transform.position=new Vector3(-9,26.2f,0);var body=actor.GetComponent<Rigidbody2D>();body.constraints=RigidbodyConstraints2D.FreezeRotation;body.linearVelocity=Vector2.zero;
        yield return new WaitForSeconds(.4f);var nav=new ObjectHeadAINavigation(actor,terrain);Vector2 start=actor.transform.position;float maxY=start.y,until=Time.time+1.8f;
        while(Time.time<until){nav.Step(1);maxY=Mathf.Max(maxY,actor.transform.position.y);yield return null;}nav.Stop();
        Debug.Log($"[ESCAPE_DETAIL] jumps={nav.Jumps}, rise={maxY-start.y:F2}, travel={actor.transform.position.x-start.x:F2}");
        Check(nav.Jumps>0 && maxY>start.y+.45f && actor.transform.position.x>start.x+1,"AI physically jumps past an obstacle");
        body.constraints=RigidbodyConstraints2D.FreezeAll;body.linearVelocity=Vector2.zero;
        actor.transform.position=new Vector3(10,32,0);terrain.DestroyCircle(actor.transform.position,230);Physics2D.SyncTransforms();
        nav=new ObjectHeadAINavigation(actor,terrain);Check(!nav.TryJump(1,out _),"jump without safe landing rejected");
        Vector2 center=actor.GetComponent<AimController>().AimOrigin;
        terrain.CreateCircle(center,65,TerrainType.Created,null);
        terrain.CreateCircle(center+new Vector2(.55f,.35f),2,TerrainType.Created,null);
        Check(terrain.IsSolidWorld(center+Vector2.right*.5f),"test character enclosed in generated terrain");
        Check(ObjectHeadEraserVisual.Radius(1)>ObjectHeadEraserVisual.Radius(0)+2,"charging expands broad eraser radius");
        var inventories=FindAnyObjectByType<PlayerInventoryManager>();var inventory=inventories.GetInventory(actor.GetComponent<ObjectHeadTeamMember>().PlayerIndex);
        for(int i=0;i<3;i++)inventory.TryConsume(i,out _);
        Check(inventory.TryAdd(CommonHeadType.Eraser,out int slot),"eraser can enter shared inventory");
        var hp=turns.Characters.Select(c=>c.GetComponent<CharacterCombat>().CurrentHp).ToArray();
        Check(actor.GetComponent<CommonHeadUseController>().UseAuthoritative(slot,CommonHeadType.Eraser,.5f,Vector2.right),"real eraser action accepted");
        yield return new WaitForSeconds(.13f);
        Check(FindAnyObjectByType<ObjectHeadEraserVisual>()!=null,"scrubbing visual spawned");
        Check(GameObject.Find("EraseRange")==null,"no range preview");
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");
        if(at>=0)
        {
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
            var cam=Camera.main;cam.GetComponent<ObjectHeadCameraController>().enabled=false;cam.transform.position=new Vector3(center.x,center.y,-10);cam.orthographicSize=5;
            yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(args[at+1],"eraser-scrub.png"));
        }
        yield return new WaitForSeconds(1);
        bool empty=true;for(float x=-1;x<=1;x+=.1f)for(float y=-1;y<=1;y+=.1f)empty&=!terrain.IsSolidWorld(center+new Vector2(x,y));
        Check(empty,"filled erase clears body and tiny terrain fragments");
        Check(hp.SequenceEqual(turns.Characters.Select(c=>c.GetComponent<CharacterCombat>().CurrentHp)),"eraser deals zero damage to all characters");
        Check(FindAnyObjectByType<ObjectHeadEraserVisual>()==null,"eraser visual cleans up");
        Debug.Log(failed?"[ESCAPE_FAIL]":"[ESCAPE_PASS] real AI jump, unsafe gap rejection, charged eraser, no damage, scenery, visual cleanup");Application.Quit(failed?2:0);
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadReleaseSmoke : MonoBehaviour
{
    private bool failed;
    private float deadline;
    private void Awake(){deadline=Time.realtimeSinceStartup+Mathf.Max(90,(ObjectHeadContent.Load()?.maps.Length??6)*22);}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[RELEASE_FAIL] Timeout");Application.Quit(2);}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (!Environment.GetCommandLineArgs().Contains("-objectHeadReleaseSmoke")) return;
        var root = new GameObject("ReleaseSmoke");
        DontDestroyOnLoad(root);
        root.AddComponent<ObjectHeadReleaseSmoke>();
    }
    private static string Arg(string key)
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);
        return i>=0 && i+1<args.Length?args[i+1]:string.Empty;
    }
    private void OnEnable()=>Application.logMessageReceived+=OnLog;
    private void OnDisable()=>Application.logMessageReceived-=OnLog;
    private void OnLog(string message,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error) failed=true;
    }
    private void Check(bool condition,string detail)
    {
        if(!condition){failed=true;Debug.LogError("[RELEASE_FAIL] "+detail);}
    }
    public static IEnumerator Capture(string file)
    {
        var camera=Camera.main;
        var canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        // Capturing through a camera must preserve Overlay's priority over world sprites.
        var orders=canvases.Select(c=>c.sortingOrder).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder+=10000;}
        Canvas.ForceUpdateCanvases();
        yield return null;
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
        target.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
        File.WriteAllBytes(file,pixels.EncodeToPNG());
        RenderTexture.active=previous;target.Release();Destroy(target);Destroy(pixels);
        for(int i=0;i<canvases.Length;i++){var canvas=canvases[i];canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.sortingOrder=orders[i];}
    }

    private IEnumerator Start()
    {
        Application.runInBackground=true;
        var directory=Arg("-objectHeadCapture");
        if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
        yield return new WaitForSeconds(1);
        if(!string.IsNullOrEmpty(directory))yield return Capture(Path.Combine(directory,"title.png"));
        yield return new WaitForSeconds(.3f);
        var title=FindAnyObjectByType<ObjectHeadTitleScreen>();
        Check(title!=null,"title scene");
        if(title!=null)
        {
            title.localPlayButton.onClick.Invoke();
            yield return new WaitForSeconds(.2f);
            if(!string.IsNullOrEmpty(directory))yield return Capture(Path.Combine(directory,"selection.png"));
            yield return new WaitForSeconds(.3f);
        }
        var catalog=ObjectHeadContent.Load();
        Check(catalog.CharactersPerPlayer(3)==0,"3 players must be disabled");
        foreach(var map in catalog.maps)
        foreach(var mode in catalog.modes)
        {
            int seed=16342+(int)mode.mode;
            var players=Enumerable.Range(1,mode.players).Select(p=>new ObjectHeadPlayerAssignment {
                playerIndex=p,allianceId=mode.Alliance(p),username="P"+p,
                characters=catalog.DefaultSelection(mode.players)}).ToArray();
            GameStartData.Apply(new GameStartData {localMatch=true,mode=mode.mode,playerCount=mode.players,
                players=players,mapId=map.id,mapSeed=seed,characterSpawnSeed=seed,startingPlayerIndex=seed%mode.players+1});
            SceneManager.LoadScene(map.sceneName);
            yield return new WaitForSeconds(.3f);
            var turns=FindAnyObjectByType<TurnManager>();
            Check(turns!=null,"turn manager");
            if(turns==null)continue;
            int expected=mode.players*catalog.CharactersPerPlayer(mode.players);
            Check(turns.Characters.Length==expected,"roster count "+mode.mode);
            // A broken scene must fail the suite, not leave an unattended player running forever.
            if(turns.Characters.Length!=expected){Application.Quit(2);yield break;}
            Check(turns.Characters.All(c=>c!=null && c.gameObject.activeInHierarchy),"active spawns "+map.id);
            Check(turns.Characters.All(c=>!c.GetComponent<CharacterCombat>().IsDead),"living spawns "+map.id);
            // Exercise editable authored stations across deterministic seat permutations.
            var layout=FindAnyObjectByType<ObjectHeadSpawnLayout>();
            Check(layout.HasValidHeightDistribution(turns.Characters),"authored spawn height distribution "+map.id+" "+mode.mode);
            var terrain=FindAnyObjectByType<TerrainManager>();
            for(int sample=0;sample<24;sample++)
            {
                layout.Place(terrain,turns.Characters,sample*37);
                Check(layout.HasValidHeightDistribution(turns.Characters),"seed "+sample+" spawn distribution outside authored limits");
            }
            layout.Place(terrain,turns.Characters,seed);
            yield return new WaitForSeconds(.3f);
            if(!string.IsNullOrEmpty(directory))yield return Capture(Path.Combine(directory,map.id+"_"+mode.mode+".png"));
            yield return new WaitForSeconds(.3f);
            if(mode.mode==ObjectHeadMatchMode.Teams)
            {
                var inventoryManager=FindAnyObjectByType<PlayerInventoryManager>();
                Check(inventoryManager.GetInventory(1)==inventoryManager.GetInventory(3),"team one shares common-head inventory");
                Check(inventoryManager.GetInventory(2)==inventoryManager.GetInventory(4),"team two shares common-head inventory");
                Check(inventoryManager.GetInventory(1)!=inventoryManager.GetInventory(2),"opposing teams keep separate common-head inventories");
                int alliance=turns.CurrentCharacter.GetComponent<ObjectHeadTeamMember>().AllianceId;
                turns.EndCurrentTurn();
                Check(turns.CurrentCharacter.GetComponent<ObjectHeadTeamMember>().AllianceId!=alliance,"alternating teams");
                var enemy=turns.Characters.Where(c=>c.GetComponent<ObjectHeadTeamMember>().AllianceId==2).ToArray();
                for(int i=0;i<enemy.Length-1;i++)enemy[i].GetComponent<CharacterCombat>().Die();
                Check(!turns.IsMatchOver,"team survives while one allied character remains");
                enemy[enemy.Length-1].GetComponent<CharacterCombat>().Die();
                Check(turns.IsMatchOver && turns.WinningPlayerIndex==1,"team victory");
            }
            else
            {
                foreach(var character in turns.Characters.Where(c=>c.GetComponent<ObjectHeadTeamMember>().PlayerIndex!=1))
                    character.GetComponent<CharacterCombat>().Die();
                Check(turns.IsMatchOver && turns.WinningPlayerIndex==1,"individual victory");
            }
            Debug.Log("[RELEASE_CASE] "+map.id+" "+mode.mode+" completed");
        }
        Debug.Log(failed?"[RELEASE_FAIL] one or more checks failed":$"[RELEASE_PASS] {catalog.maps.Length*catalog.modes.Length} map/mode combinations, {catalog.maps.Length*catalog.modes.Length*24} spawn seeds, title/lobby, victory");
        Application.Quit(failed?2:0);
    }
}

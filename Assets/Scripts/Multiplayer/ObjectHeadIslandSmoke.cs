using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ObjectHeadIslandSmoke:MonoBehaviour
{
    private bool failed;private float deadline;private string folder;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadIslandSmoke"))return;
        var root=new GameObject("IslandSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadIslandSmoke>();
    }
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[ISLAND_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private void Check(bool ok,string label){Debug.Log("[ISLAND_DETAIL] "+label+"="+ok);if(!ok)failed=true;}
    private IEnumerator Capture(string name){if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,name+".png"));}
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(.4f);
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");folder=at>=0?args[at+1]:null;
        if(folder!=null)Directory.CreateDirectory(folder);
        var title=FindAnyObjectByType<ObjectHeadTitleScreen>();var front=title.frontEnd;
        front.Show(front.play);yield return null;
        var ai=(RectTransform)title.localPlayButton.transform;var training=(RectTransform)front.trainingButton.transform;
        Check(Vector2.Distance(ai.rect.size,training.rect.size)<.1f,"AI button equals other menu buttons");
        Check(title.localPlayButton.GetComponentInChildren<Text>().fontSize==front.trainingButton.GetComponentInChildren<Text>().fontSize,"AI menu label size matches");
        yield return Capture("island-title-menu");
        var content=ObjectHeadContent.Load();
        foreach(var map in content.maps)
        {
            GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.FreeForAll,playerCount=4,mapId=map.id,mapSeed=916,characterSpawnSeed=916,startingPlayerIndex=1,
                players=Enumerable.Range(1,4).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=new[]{ObjectHeadCharacterKind.Bulb,ObjectHeadCharacterKind.Seed}}).ToArray()});
            SceneManager.LoadScene(map.sceneName);yield return new WaitForSeconds(.6f);
            var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
            foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
            var actor=turns.CurrentCharacter;var aim=actor.GetComponent<AimController>();var body=actor.GetComponent<Rigidbody2D>();
            actor.UseNetworkInput=false;body.constraints=RigidbodyConstraints2D.FreezePositionY|RigidbodyConstraints2D.FreezeRotation;
#if ENABLE_INPUT_SYSTEM
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            Vector2 before=actor.transform.position;
            foreach(var key in new[]{UnityEngine.InputSystem.Key.LeftArrow,UnityEngine.InputSystem.Key.RightArrow})
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(key));UnityEngine.InputSystem.InputSystem.Update();
                actor.SendMessage("Update");
                Check(actor.InputMoveX==0 && aim.FacingSign==(key==UnityEngine.InputSystem.Key.LeftArrow?-1:1),map.id+" arrow turns only "+key);
                yield return new WaitForFixedUpdate();
            }
            Check(Mathf.Abs(actor.transform.position.x-before.x)<.01f,map.id+" arrows do not translate body");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.D));UnityEngine.InputSystem.InputSystem.Update();actor.SendMessage("Update");
            Check(actor.InputMoveX>.9f,map.id+" D still moves");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.Update();actor.SendMessage("Update");UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
#endif
            actor.UseNetworkInput=true;body.constraints=RigidbodyConstraints2D.FreezeAll;
            var terrain=FindAnyObjectByType<TerrainManager>();var camera=Camera.main;var controller=camera.GetComponent<ObjectHeadCameraController>();
            var art=ObjectHeadPresentation.Load();
            Check(art.airstrikeBombSize>=1.5f && art.healingSupplySize>=1.6f,"larger drop artwork");
            Vector2 landing=ObjectHeadSkyDrop.Landing(terrain,actor.transform.position);
            float top=controller.MaximumVisibleSkyY();
            Check(ObjectHeadSkyDrop.Origin(landing,art.airstrikeBombSize,terrain).y-art.airstrikeBombSize>top,"bomb starts beyond maximum zoom sky");
            camera.orthographicSize=3;
            Check(Vector2.Distance(landing,ObjectHeadSkyDrop.Landing(terrain,actor.transform.position))<.001f,"zoom cannot change landing calculation");
            var status=actor.gameObject.AddComponent<ObjectHeadCaptivity>();status.Capture();
            var cage=actor.GetComponentInChildren<ObjectHeadCageVisual>();
            Check(cage!=null && cage.cageRenderer.bounds.min.y>top,"maiden starts beyond maximum zoom sky");
            yield return new WaitForSeconds(1.1f);
            Check(cage!=null && cage.cageRenderer.bounds.size.y>=2.4f,"maiden enlarged and landed");
            controller.enabled=false;
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
            Screen.SetResolution(1600,900,false);yield return new WaitForSeconds(.15f);
            camera.transform.position=new Vector3(0,10,-10);camera.orthographicSize=19;
            yield return Capture("island-"+map.id);
            float structureX=(map.id=="twin_citadels"||map.id=="shattered_reef"||map.id=="sky_terraces")?-10:0;
            bool hitStructure=terrain.TryCheckTerrainHit(new Vector2(structureX,38),new Vector2(structureX,4),out var roof);
            Check(hitStructure,map.id+" structure has physical terrain pixels");
            if(hitStructure)
            {
                Vector2 inside=roof.point+Vector2.down*.08f;
                Check(terrain.IsSolidWorld(inside),map.id+" roof is solid before destruction");
                terrain.DestroyCircle(inside,12);
                Check(!terrain.IsSolidWorld(inside),map.id+" building uses destructible terrain");
            }
        }
        Debug.Log(failed?"[ISLAND_FAIL]":"[ISLAND_PASS] menu size, arrows versus movement, sky drops and all catalog maps");Application.Quit(failed?2:0);
    }
}

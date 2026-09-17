using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadSupportSmoke:MonoBehaviour
{
    private bool failed;private float deadline;private string folder;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(!Environment.GetCommandLineArgs().Contains("-objectHeadSupportSmoke"))return;var go=new GameObject("SupportSmoke");DontDestroyOnLoad(go);go.AddComponent<ObjectHeadSupportSmoke>();}
    private void Awake(){deadline=Time.realtimeSinceStartup+105;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-objectHeadCapture");folder=i>=0?args[i+1]:null;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[SUPPORT_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Log;
    private void OnDisable()=>Application.logMessageReceived-=Log;
    private void Log(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error)failed=true;}
    private void Check(bool ok,string label){Debug.Log("[SUPPORT_DETAIL] "+label+"="+ok);if(!ok)failed=true;}
    private IEnumerator Capture(string name){if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,name+".png"));}
    private IEnumerator LoadMap(string id,string scene)
    {
        var catalog=ObjectHeadContent.Load();GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.FreeForAll,playerCount=4,mapId=id,mapSeed=1717,characterSpawnSeed=1717,startingPlayerIndex=1,
            players=Enumerable.Range(1,4).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=catalog.DefaultSelection(4)}).ToArray()});
        SceneManager.LoadScene(scene);yield return new WaitForSeconds(.6f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        if(turns.Characters.Length!=8){Check(false,"eight characters loaded");Application.Quit(2);yield break;}
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
    }
    private IEnumerator Start()
    {
        yield return null;Screen.SetResolution(1600,900,false);
        yield return LoadMap("low_cavern","LowCavern");
        var turns=FindAnyObjectByType<TurnManager>();var terrain=FindAnyObjectByType<TerrainManager>();
        Check(turns.Characters.All(c=>terrain.TryCheckTerrainHit(c.transform.position+Vector3.up,c.transform.position+Vector3.up*15,out _)),"all cavern spawns have a real ceiling");
        var cam=Camera.main;var control=cam.GetComponent<ObjectHeadCameraController>();control.enabled=false;cam.transform.position=new Vector3(0,2,-10);cam.orthographicSize=13.8f;
        var hud=FindAnyObjectByType<ObjectHeadBattleScreen>();Check(hud.commonButtons.Length==6 && CommonHeadInventory.SlotCount==6,"six authored shared slots");
        var inventory=FindAnyObjectByType<PlayerInventoryManager>().GetInventory(turns.CurrentPlayerIndex);
        for(int i=0;i<6;i++)Check(inventory.TryAdd(i==4?CommonHeadType.CrystalOrb:i==5?CommonHeadType.HeavyWeapon:CommonHeadType.Attack,out _),"inventory slot "+i);
        Check(!inventory.TryAdd(CommonHeadType.Lock,out _),"seventh item rejected");Check(turns.CurrentCharacter.GetComponent<CommonHeadUseController>().TrySelectCommonHeadSlot(5),"sixth slot selectable");
        yield return new WaitForSeconds(.2f);yield return Capture("low-cavern-play");
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);yield return Capture("low-cavern-overview");
        var actor=turns.CurrentCharacter.GetComponent<CharacterCombat>();
        Check(ObjectHeadTeleport.TryDestination(actor,terrain,new Vector2(9,-.8f),out var destination),"crystal clear cavern destination");
        Check(ObjectHeadTeleport.Apply(actor,terrain,new Vector2(9,-.8f)) && Vector2.Distance(actor.transform.position,destination)<.1f,"crystal moves owner");
        // Build a known solid fixture: (0,10) in the art is not guaranteed to be inside its roof.
        var solidPoint=new Vector2(0,10);terrain.CreateCircle(solidPoint,128,TerrainType.Created,null);
        Check(terrain.IsSolidWorld(solidPoint) && !ObjectHeadTeleport.TryDestination(actor,terrain,solidPoint,out _),"enclosed solid destination rejected");
        int particles=ObjectHeadMicroParticles.TotalEmitted;terrain.DestroyCircle(new Vector2(10,-3),24);Check(ObjectHeadMicroParticles.TotalEmitted>particles,"terrain emits debris");
        yield return LoadMap("twin_citadels","TwinCitadels");turns=FindAnyObjectByType<TurnManager>();terrain=FindAnyObjectByType<TerrainManager>();
        cam=Camera.main;control=cam.GetComponent<ObjectHeadCameraController>();control.enabled=false;cam.transform.position=new Vector3(-9,1,-10);cam.orthographicSize=7.5f;
        bool passage=true;for(float x=-15;x<=-3;x+=.25f){float middle=1.7f-(x+14)*.25f;passage&=!terrain.IsSolidWorld(new Vector2(x,middle));}
        Check(passage,"harbor corridor open end to end");yield return Capture("harbor-through-cave");
        var a=turns.Characters[0].GetComponent<CharacterCombat>();var b=turns.Characters[1].GetComponent<CharacterCombat>();
        Vector2 point=new Vector2(0,15);a.transform.position=point+Vector2.left*.4f;b.transform.position=point+Vector2.right*.4f;Physics2D.SyncTransforms();cam.transform.position=new Vector3(0,17,-10);cam.orthographicSize=8;
        var settings=ObjectHeadContent.Load().Common(CommonHeadType.HeavyWeapon).skill.Resolve(null);settings.resolveAtTurnEnd=false;
        int hp=a.CurrentHp;Check(turns.TryBeginAction(turns.CurrentCharacter),"heavy real action begins");
        var shot=new GameObject("HeavySupportTest").AddComponent<SkillProjectile>();shot.transform.position=point;
        shot.Initialize(Vector2.zero,.1f,0,20,Color.white,a,turns,settings.maxDamage,settings.explosionRadiusWorld,.2f,Color.white,settings.knockbackForce,settings);shot.SendMessage("ResolveImpact",point);
        yield return new WaitForSeconds(1.5f);Check(a.CurrentHp==hp && a.PendingDamage==0,"fake opening and silence deal no damage");
        yield return new WaitForSeconds(1.1f);yield return Capture("helicopter-support");while(shot!=null)yield return null;
        Check(a.CurrentHp==hp && a.PendingDamage>0 && a.PendingDamage<=settings.maxDamage,"one deferred final attack");
        int serial=turns.TurnSerial;turns.SetTrainingTimerPaused(false);while(turns.TurnSerial==serial)yield return null;turns.SetTrainingTimerPaused(true);Check(a.CurrentHp<hp,"damage settled before next turn");
        control.enabled=true;control.FocusOnCurrentCharacterImmediate();yield return null;a.SendMessage("ShowDamagePopup",12);yield return new WaitForSeconds(.15f);turns.EndCurrentTurn();yield return new WaitForSeconds(.2f);
        Check(Vector2.Distance(cam.transform.position,point)<5,"camera holds damage across turn change");
        // Exercise both authored weapon animations, independently of which RNG variant the real shot selected.
        control.enabled=false;cam.transform.position=new Vector3(0,20,-10);cam.orthographicSize=3;
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
        var presentation=ObjectHeadPresentation.Load();
        for(int variant=0;variant<2;variant++)
        {
            int cosmeticSerial=0;
            while(new System.Random(unchecked(cosmeticSerial*73856093+Mathf.RoundToInt(point.x*32)*19349663+Mathf.RoundToInt(point.y*32)*83492791)).Next(2)!=variant)cosmeticSerial++;
            int before=ObjectHeadSupportPilotAnimation.ShotsPresented;bool finished=false;
            IEnumerator Present(){yield return ObjectHeadHelicopterSupport.Play(point,settings,cosmeticSerial);finished=true;}
            StartCoroutine(Present());
            while(!finished && ObjectHeadSupportPilotAnimation.ShotsPresented==before)yield return null;
            var pilot=FindAnyObjectByType<ObjectHeadSupportPilotAnimation>();
            var poses=variant==0?presentation.supportMachineGunFrames:presentation.supportLauncherFrames;
            Check(ObjectHeadHelicopterSupport.LastVariant==variant && poses.Length==3 && poses.Distinct().Count()==3 && ObjectHeadSupportPilotAnimation.ShotsPresented>before,"support variant "+variant+" fires with three distinct poses");
            Check(pilot!=null && pilot.GetComponent<SpriteRenderer>().maskInteraction==SpriteMaskInteraction.VisibleInsideMask && pilot.GetComponentInParent<ObjectHeadSupportRotor>()!=null,"support variant "+variant+" seated in helicopter with spinning rotor");
            yield return Capture(variant==0?"support-mg-fire":"support-launcher-fire");
            while(!finished)yield return null;yield return new WaitForSeconds(2.1f);
        }
        Debug.Log(failed?"[SUPPORT_FAIL]":"[SUPPORT_PASS] cavern, six slots, teleport, helicopter, camera, debris");Application.Quit(failed?2:0);
    }
}

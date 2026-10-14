using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in background verification of the real camera and pooled audio.</summary>
public sealed class ObjectHeadFeelSmoke : MonoBehaviour
{
    private bool failed;
    private float deadline;
    private float savedVolume;
    private bool hadVolume;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Environment.GetCommandLineArgs().Contains("-objectHeadFeelSmoke")) return;
        var root = new GameObject("FeelSmoke"); DontDestroyOnLoad(root); root.AddComponent<ObjectHeadFeelSmoke>();
    }
    private void Awake()
    {
        deadline = Time.realtimeSinceStartup + 90;
        savedVolume = ObjectHeadAudio.SfxVolume; hadVolume = PlayerPrefs.HasKey("Audio.SFX");
        Application.logMessageReceived += Log;
    }
    private void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error) failed = true; }
    private void Update()
    { if (Time.realtimeSinceStartup > deadline) { Debug.LogError("[FEEL_FAIL] Timeout"); Application.Quit(2); } }
    private void OnDestroy()
    {
        ObjectHeadAudio.SfxVolume = savedVolume;
        if (!hadVolume) PlayerPrefs.DeleteKey("Audio.SFX");
        Application.logMessageReceived -= Log;
    }
    private void Check(bool ok, string label)
    { if (!ok) { failed = true; Debug.LogError("[FEEL_FAIL] " + label); } else Debug.Log("[FEEL_CHECK] " + label); }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        string[] args = Environment.GetCommandLineArgs(); int arg = Array.IndexOf(args, "-objectHeadCapture");
        string directory = arg >= 0 && arg + 1 < args.Length ? args[arg + 1] : null;
        if (directory != null) Directory.CreateDirectory(directory);
        var catalog = ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData { localMatch=true, mode=ObjectHeadMatchMode.Duel,playerCount=2,
            mapId=catalog.maps[0].id,mapSeed=41,characterSpawnSeed=41,startingPlayerIndex=1,
            players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment { playerIndex=p,allianceId=p,
                username="P"+p,characters=catalog.DefaultSelection(2)}).ToArray() });
        SceneManager.LoadScene(catalog.maps[0].sceneName);
        yield return new WaitForSeconds(.8f);
        var camera = Camera.main; var follow = camera.GetComponent<ObjectHeadCameraController>();
        var terrain = FindAnyObjectByType<TerrainManager>();
        var hashes = terrain.CaptureNetworkTileHashes();
        foreach (float size in new[] { 5.5f, 18f, -1f })
        {
            follow.FitToTerrainOverview();
            if (size > 0) camera.orthographicSize = size;
            yield return null; yield return null;
            Vector3 before = camera.transform.position;
            float actualSize = camera.orthographicSize;
            string label = size < 0 ? "full_overview" : "zoom_" + size;
            if (directory != null) yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,label+"_before.png"));
            ObjectHeadPresentation.PresentConfirmedImpact(32, before, 2);
            float peak = 0;
            for (int i = 0; i < 16; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, (camera.transform.position-before).magnitude/(actualSize*2)*900);
            }
            Check(peak > 3f && peak < 25f, label+" actual camera displacement: "+peak.ToString("F2")+" px @900p");
            Check(Mathf.Abs(camera.orthographicSize-actualSize)<.001f,label+" preserves manual zoom");
            if (directory != null)
            {
                ObjectHeadPresentation.PresentConfirmedImpact(32,before,2);
                yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,label+"_impact.png"));
            }
            yield return new WaitForSeconds(.6f);
            Check(Vector3.Distance(camera.transform.position,before)<.001f,label+" no camera drift after shake");
        }
        var audio = FindAnyObjectByType<ObjectHeadCombatAudio>();
        Check(audio != null && audio.GetComponents<AudioSource>().Length==ObjectHeadCombatAudio.VoiceLimit,"fixed sound voice budget");
        foreach(var voice in audio.GetComponents<AudioSource>())voice.Stop();
        ObjectHeadAudio.SfxVolume=.6f;
        ObjectHeadCombatAudio.Play("heavy",camera.transform.position);
        var heavy=audio.GetComponents<AudioSource>().FirstOrDefault(s=>s.isPlaying && s.clip.name==ObjectHeadCombatAudio.ClipName("heavy"));
        Check(heavy!=null && heavy.volume<=.38f,"first audio buffer already has mix headroom");
        ObjectHeadCombatAudio.Play("land",camera.transform.position);
        var land=audio.GetComponents<AudioSource>().FirstOrDefault(s=>s.isPlaying && s.clip.name==ObjectHeadCombatAudio.ClipName("land"));
        float ducked=land!=null?land.volume:0;
        ObjectHeadCombatAudio.Play("charge_full",camera.transform.position);
        var charge=audio.GetComponents<AudioSource>().FirstOrDefault(s=>s.isPlaying && s.clip.name==ObjectHeadCombatAudio.ClipName("charge_full"));
        Check(charge!=null && charge.volume>.1f,"full-charge cue remains audible during impact");
        yield return new WaitForSecondsRealtime(.32f);
        Check(heavy!=null && heavy.isPlaying && land!=null && land.isPlaying && land.volume>ducked*2,
            "background sound recovers before the long explosion finishes");
        yield return null;
        Check(audio.GetComponents<AudioSource>().Any(s=>s.isPlaying && s.volume>0),"supplied heavy SFX playing");
        ObjectHeadAudio.SfxVolume=0;
        yield return null; yield return null;
        Check(audio.GetComponents<AudioSource>().All(s=>s.volume==0),"SFX slider mutes active voices");
        ObjectHeadCombatAudio.Play("final",camera.transform.position);
        ObjectHeadCombatAudio.FadeCue("final",.06f);
        yield return new WaitForSeconds(.12f);
        Check(audio.GetComponents<AudioSource>().All(s=>!s.isPlaying || s.clip.name!=ObjectHeadCombatAudio.ClipName("final")),
            "heavy support pause silences the opening tail");
        Check(hashes.SequenceEqual(terrain.CaptureNetworkTileHashes()),"cosmetic impacts leave terrain unchanged");
        Check(Time.timeScale==1f,"feedback never pauses simulation");
        yield return CheckControls(camera,follow);
        ObjectHeadPresentation.PresentConfirmedImpact(41,camera.transform.position,1);
        Check(ObjectHeadImpactAccent.ActiveCount>0,"confirmed hit emits outlined contact accent");
        if(directory!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,"contact_accent.png"));
        yield return new WaitForSecondsRealtime(.3f);
        Check(ObjectHeadImpactAccent.ActiveCount==0,"contact accent clears without lingering clutter");
        Check(hashes.SequenceEqual(terrain.CaptureNetworkTileHashes()),"control and presentation tests leave terrain unchanged");
        Debug.Log(failed?"[FEEL_FAIL]":"[FEEL_PASS] actual camera close/max/overview, pooled supplied SFX, volume and cosmetic isolation");
        Application.Quit(failed?2:0);
    }

    private IEnumerator CheckControls(Camera camera,ObjectHeadCameraController follow)
    {
        follow.FitToTerrainOverview(); camera.orthographicSize=7;
        // Center the anchor away from terrain clamps for an unambiguous projection test.
        var anchor=new Vector2(.56f,.53f);
        Vector3 world=camera.ViewportToWorldPoint(new Vector3(anchor.x,anchor.y,0));
        follow.RequestManualZoom(5,anchor);
        Check(Mathf.Abs(camera.orthographicSize-7)<.001f,"wheel request does not snap zoom");
        yield return null;
        Check(camera.orthographicSize<7 && camera.orthographicSize>5,"zoom eases toward target");
        yield return new WaitForSecondsRealtime(.65f);
        Check(Mathf.Abs(camera.orthographicSize-5)<.01f,"smooth zoom reaches target");
        Vector3 after=camera.ViewportToWorldPoint(new Vector3(anchor.x,anchor.y,0));
        Check(Vector2.Distance(world,after)<.02f,"actual camera preserves cursor anchor");
        Vector3 position=camera.transform.position;
        follow.RequestCharacterFocus();
        Check(Vector3.Distance(position,camera.transform.position)<.001f,"Home focus does not teleport camera");
        Check(!follow.IsManualView && !follow.IsZoomSettling,"Home restores follow and cancels zoom");
        follow.RequestManualZoom(6,new Vector2(.5f,.5f));
        Check(!follow.IsManualView,"zooming in follow mode does not enter free camera");
        yield return new WaitForSecondsRealtime(.7f);
        Check(Mathf.Abs(camera.orthographicSize-6)<.01f,"follow camera keeps chosen zoom");
        var turns=FindAnyObjectByType<TurnManager>();
        int serial=turns.TurnSerial;
        Vector3 beforeTurn=camera.transform.position;
        turns.EndCurrentTurn();
        yield return new WaitForSecondsRealtime(.6f);
        Check(turns.TurnSerial>serial && !follow.IsManualView &&
            Vector2.Distance(beforeTurn,camera.transform.position)>.2f,
            "zoomed camera follows the next turn character");
        Check(Mathf.Abs(camera.orthographicSize-6)<.01f,"turn change preserves chosen zoom amount");
        ObjectHeadPresentation.PresentConfirmedImpact(32,camera.transform.position,2);
        yield return new WaitForSecondsRealtime(.1f);
        Check(Mathf.Abs(camera.orthographicSize-6)<.01f,"impact framing cannot override user zoom");
        yield return new WaitForSecondsRealtime(.4f);

        var floor=new GameObject("FeelTestFloor"); floor.transform.position=new Vector3(1000,0,0);
        floor.AddComponent<BoxCollider2D>().size=new Vector2(10,1);
        var actor=new GameObject("FeelTestActor"); actor.transform.position=new Vector3(1000,1.02f,0);
        var body=actor.AddComponent<Rigidbody2D>();body.freezeRotation=true;
        actor.AddComponent<BoxCollider2D>();
        var controller=actor.AddComponent<TurnCharacterController>();
        controller.ConfigureMovement(3.15f,6);controller.UseNetworkInput=true;controller.SetControlEnabled(true);
        Physics2D.SyncTransforms();
        controller.SetNetworkInput(1,false);
        yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(body.linearVelocity.x-3.15f)<.01f,"movement authority uses reduced walk speed immediately");
        controller.SetNetworkInput(0,false);
        yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(body.linearVelocity.x)<.01f,"movement stops without sliding");
        body.position=new Vector2(1020,1.02f);body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        controller.SetNetworkInput(0,true,true);
        yield return new WaitForFixedUpdate();
        Check(body.linearVelocity.y>4,"real controller jumps just after leaving ledge");
        controller.ResetTurnStatus();
        body.position=new Vector2(1000,1.14f);body.linearVelocity=Vector2.down;Physics2D.SyncTransforms();
        controller.SetNetworkInput(0,false,true);
        yield return new WaitForFixedUpdate();
        body.position=new Vector2(1000,1.01f);body.linearVelocity=Vector2.down;Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Check(body.linearVelocity.y>1 && body.linearVelocity.y<4,"released buffered tap becomes short landing jump");
        controller.PreserveExternalMotion(.2f);
        body.linearVelocity=new Vector2(7,5);
        controller.SetNetworkInput(-1,false);
        yield return new WaitForFixedUpdate();
        Check(body.linearVelocity.x>6,"input grace does not cancel knockback");
        Destroy(actor);Destroy(floor);
    }
}

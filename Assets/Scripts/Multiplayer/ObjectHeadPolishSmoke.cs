using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in QA of manual AI squads, slope artwork and visual head swaps.</summary>
public sealed class ObjectHeadPolishSmoke : MonoBehaviour
{
    private bool failed;
    private string directory;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadPolishSmoke"))return;
        var root=new GameObject("PolishSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadPolishSmoke>();
    }
    private void Check(bool ok,string detail)
    { if(!ok){failed=true;Debug.LogError("[POLISH_FAIL] "+detail);} }
    private void OnEnable()=>Application.logMessageReceived+=Logged;
    private void OnDisable()=>Application.logMessageReceived-=Logged;
    private void Logged(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private IEnumerator Capture(string name)
    {
        if(!string.IsNullOrEmpty(directory))yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,name+".png"));
    }
    private IEnumerator Start()
    {
        Application.runInBackground=true;
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");
        if(at>=0 && at+1<args.Length){directory=args[at+1];Directory.CreateDirectory(directory);}
        yield return new WaitForSeconds(.7f);
        var catalog=ObjectHeadContent.Load();
        var title=FindAnyObjectByType<ObjectHeadTitleScreen>();
        title.localPlayButton.onClick.Invoke();
        title.lobbySizePlusButton.onClick.Invoke();
        title.lobbySizePlusButton.onClick.Invoke(); // four player 2v2, includes an allied AI
        title.aiRosterModeButton.onClick.Invoke();
        Check(title.aiRosterModeText.text.Contains("직접 선택"),"manual option is localized");
        Check(title.editLocalSquadButton.gameObject.activeInHierarchy,"editable squad selector visible");
        var expected=new ObjectHeadCharacterKind[4][];
        for(int p=0;p<4;p++)
        {
            Check(title.editLocalSquadText.text.Contains("P"+(p+1)),"editing seat "+(p+1));
            expected[p]=new[]{catalog.characters[(p+2)%catalog.characters.Length].kind,catalog.characters[(p+3)%catalog.characters.Length].kind};
            for(int s=0;s<2;s++){title.slotButtons[s].onClick.Invoke();title.SelectCharacter(expected[p][s]);}
            title.readyButton.onClick.Invoke();
        }
        title.aiRosterModeButton.onClick.Invoke(); // random
        title.aiRosterModeButton.onClick.Invoke(); // restore manual choices
        for(int p=0;p<4;p++)
        {
            for(int s=0;s<2;s++)Check(title.slotPortraits[s].sprite==catalog.Character(expected[p][s]).portrait,"manual roster preserved P"+(p+1));
            title.editLocalSquadButton.onClick.Invoke();
        }
        yield return Capture("ai-manual-squads");
        Check(title.startGameButton.interactable,"manual roster can start");
        title.startGameButton.onClick.Invoke();
        Check(GameStartData.Instance.players.Select((p,i)=>p.characters.SequenceEqual(expected[i])).All(x=>x),"chosen squads reach match data");
        foreach(var player in GameStartData.Instance.players)player.isAi=false; // isolate geometry QA from automated actions
        yield return new WaitForSeconds(.7f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        var camera=Camera.main;
        foreach(var behaviour in camera.GetComponents<MonoBehaviour>())
            if(behaviour is ObjectHeadCameraController || behaviour is TerrainCameraFitter)behaviour.enabled=false;
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))canvas.gameObject.SetActive(false);
        var terrain=FindAnyObjectByType<TerrainManager>();
        Vector2 slope=default;bool found=false;
        for(float x=terrain.TerrainOriginWorld.x+1;x<terrain.TerrainOriginWorld.x+terrain.WidthPx/(float)terrain.PixelsPerUnit-1;x+=.25f)
        {
            if(terrain.FindTerrainSurface(x,out var p) && terrain.FindTerrainSurface(x+.25f,out var next) && Mathf.Abs(next.y-p.y)>.25f && Mathf.Abs(next.y-p.y)<.65f)
            {slope=p;found=true;break;}
        }
        Check(found,"steep test slope found");
        var zone=GroundHazardZone.Create(slope,4,.2f,3,0,1,Color.white,null,turns);
        zone.SetArtwork(ObjectHeadPresentation.Load().Find(11).zoneSprite);
        var artwork=zone.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="ZoneArtwork").ToArray();
        Check(artwork.Length>0,"slope artwork created");
        Check(artwork.All(r=>Mathf.Abs(r.transform.lossyScale.x-r.transform.lossyScale.y)<.001f),"artwork keeps aspect ratio");
        Check(artwork.Any(r=>Mathf.Abs(Mathf.DeltaAngle(0,r.transform.eulerAngles.z))>20f),"artwork follows slope angle");
        camera.transform.position=new Vector3(slope.x,slope.y+.6f,-10);camera.orthographicSize=2.3f;
        yield return Capture("slope-zone");
        // Render actual CharacterVisual output for all skill swaps, then equipped common heads.
        foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))renderer.enabled=false;
        camera.backgroundColor=new Color(.10f,.19f,.24f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transform.position=new Vector3(0,1.6f,-10);camera.orthographicSize=4.5f;
        for(int c=0;c<catalog.characters.Length;c++)
        {
            var actor=Instantiate(catalog.characters[c].prefab,new Vector3(0,100,0),Quaternion.identity);
            actor.GetComponent<Rigidbody2D>().simulated=false;
            foreach(var script in actor.GetComponents<MonoBehaviour>())script.enabled=false;
            var visual=actor.GetComponent<CharacterVisual>();
            for(int s=0;s<3;s++)
            {
                visual.SetSkillIndex(s);
                CopyCharacter(actor,new Vector2(-5.75f+c*2.3f,4.5f-s*1.65f),catalog.characters[c].kind+" "+(s+1),catalog.uiFont);
            }
            actor.SetActive(false);Destroy(actor);
        }
        for(int i=0;i<catalog.commonHeads.Length;i++)
        {
            var actor=Instantiate(catalog.characters[0].prefab,new Vector3(0,100,0),Quaternion.identity);
            actor.GetComponent<Rigidbody2D>().simulated=false;
            foreach(var script in actor.GetComponents<MonoBehaviour>())script.enabled=false;
            actor.GetComponent<CharacterVisual>().SetTemporaryCommonHead(catalog.commonHeads[i].sprite);
            CopyCharacter(actor,new Vector2(-6.3f+i*1.8f,-.9f),catalog.commonHeads[i].type.ToString(),catalog.uiFont);
            actor.SetActive(false);Destroy(actor);
        }
        yield return Capture("head-sizes");
        Debug.Log(failed?"[POLISH_FAIL]":"[POLISH_PASS] manual AI squads persist and launch, allied AI selectable, slope artwork keeps aspect and rotation; screenshots captured");
        Application.Quit(failed?2:0);
    }
    private static void CopyCharacter(GameObject actor,Vector2 anchor,string label,Font font)
    {
        foreach(string child in new[]{"BodyRenderer","HeadRenderer"})
        {
            var source=actor.transform.Find(child).GetComponent<SpriteRenderer>();
            var go=new GameObject(label+"_"+child,typeof(SpriteRenderer));
            go.transform.position=(Vector3)anchor+source.transform.localPosition;
            go.transform.localScale=source.transform.localScale;
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=source.sprite;renderer.sharedMaterial=source.sharedMaterial;renderer.sortingOrder=source.sortingOrder;
        }
        var text=new GameObject(label,typeof(TextMesh));text.transform.position=(Vector3)anchor+Vector3.down*.7f;
        var mesh=text.GetComponent<TextMesh>();mesh.text=label;mesh.font=font;mesh.fontSize=30;mesh.characterSize=.065f;mesh.anchor=TextAnchor.MiddleCenter;
        text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
}

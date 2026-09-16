using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in executable QA; never runs in ordinary play.</summary>
public sealed class ObjectHeadPresentationSmoke : MonoBehaviour
{
    private bool failed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadPresentationSmoke"))return;
        var go=new GameObject("PresentationSmoke");DontDestroyOnLoad(go);go.AddComponent<ObjectHeadPresentationSmoke>();
    }
    private void OnEnable()=>Application.logMessageReceived+=Log;
    private void OnDisable()=>Application.logMessageReceived-=Log;
    private void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)failed=true;}
    private IEnumerator Start()
    {
        Application.runInBackground=true;
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadCapture");
        string directory=at>=0?args[at+1]:Application.persistentDataPath;
        Directory.CreateDirectory(directory);
        var catalog=ObjectHeadContent.Load();
        foreach(var kind in new[]{ObjectHeadCharacterKind.Bulb,ObjectHeadCharacterKind.Seed,ObjectHeadCharacterKind.Bomb})
        for(int slot=0;slot<3;slot++)
        {
            GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=catalog.maps[0].id,
                mapSeed=41,characterSpawnSeed=41,startingPlayerIndex=1,players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment{
                    playerIndex=p,allianceId=p,username="P"+p,characters=new[]{kind,kind,kind}}).ToArray()});
            SceneManager.LoadScene(catalog.maps[0].sceneName);
            yield return new WaitForSeconds(.6f);
            var turns=FindAnyObjectByType<TurnManager>();
            var camera=Camera.main;camera.orthographicSize=4.5f;
            var player=turns.CurrentCharacter;
            player.GetComponent<DemoSkillSelector>().SetSkillIndex(slot);
            player.GetComponent<AimController>().SetAimDirection(new Vector2(1,-.7f));
            player.GetComponent<SkillFireController>().Fire(.15f);
            yield return new WaitForSeconds(.16f);
            yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,kind+"_"+(slot+1)+"_flight.png"));
            // Keep a repeatable visual sample of the exact authored impact used by the projectile.
            int id=((int)kind+1)*10+slot+1;
            var point=(Vector2)camera.transform.position+Vector2.up;
            if(!ObjectHeadPresentation.Impact(id,point,1.2f)){failed=true;Debug.LogError("Missing effect "+id);}
            yield return null;
            yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(directory,kind+"_"+(slot+1)+"_impact.png"));
            yield return new WaitForSeconds(3);
            Debug.Log("[PRESENTATION_CASE] "+id+" fire/impact processed");
        }
        Debug.Log(failed?"[PRESENTATION_FAIL]":"[PRESENTATION_PASS] Nine skill firing paths and effect profiles");
        Application.Quit(failed?2:0);
    }
}

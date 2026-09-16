using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in QA only. Restores the user's volume preference when finished.
public sealed class ObjectHeadAudioSmoke : MonoBehaviour
{
    private bool failed;
    private bool hadPreference;
    private float previousVolume;
    private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadAudioSmoke"))return;
        var root=new GameObject("AudioSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadAudioSmoke>();
    }
    private void Awake()
    {
        hadPreference=PlayerPrefs.HasKey("Audio.BGM");previousVolume=ObjectHeadAudio.BgmVolume;
        deadline=Time.realtimeSinceStartup+45;Application.logMessageReceived+=OnLog;
    }
    private void OnDestroy()
    {
        ObjectHeadAudio.BgmVolume=previousVolume;
        if(!hadPreference)PlayerPrefs.DeleteKey("Audio.BGM");PlayerPrefs.Save();
        Application.logMessageReceived-=OnLog;
    }
    private void OnLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)failed=true;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[AUDIO_FAIL] Timeout");Application.Quit(2);}}
    private void Check(bool condition,string message)
    {
        if(!condition){failed=true;Debug.LogError("[AUDIO_FAIL] "+message);}else Debug.Log("[AUDIO_CHECK] "+message);
    }
    private IEnumerator Start()
    {
        Application.runInBackground=true;
        yield return new WaitForSeconds(.5f);
        string titleScene=SceneManager.GetActiveScene().name;
        var audio=FindAnyObjectByType<ObjectHeadAudio>();
        if(audio==null){Check(false,"audio service exists");Application.Quit(2);yield break;}
        var source=audio.GetComponent<AudioSource>();
        Check(source.clip!=null && source.clip.name=="IslandLoop","title uses WAV asset");
        Check(source.isPlaying,"title BGM playing");
        var settings=FindAnyObjectByType<ObjectHeadSettingsPanel>();
        if(settings==null){Check(false,"settings panel exists");Application.Quit(2);yield break;}
        settings.bgm.value=.37f;yield return null;
        Check(Mathf.Abs(source.volume-.37f)<.001f,"settings slider changes actual volume");
        settings.bgm.value=0;yield return null;
        Check(source.volume==0,"zero slider mutes actual source");
        settings.bgm.value=.37f;
        var catalog=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=catalog.maps[0].id,mapSeed=916,characterSpawnSeed=916,startingPlayerIndex=1,
            players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=catalog.DefaultSelection(2)}).ToArray()});
        SceneManager.LoadScene(catalog.maps[0].sceneName);yield return new WaitForSeconds(.4f);
        Check(!source.isPlaying,"BGM stopped in battle");
        Check(audio.GetComponents<AudioSource>().Length==1,"no provisional SFX source");
        SceneManager.LoadScene(titleScene);yield return new WaitForSeconds(.4f);
        Check(source.isPlaying,"BGM resumes on title return");
        Check(Mathf.Abs(source.volume-.37f)<.001f,"volume preserved across scenes");
        Check(FindObjectsByType<ObjectHeadAudio>(FindObjectsSortMode.None).Length==1,"no duplicate music source");
        Debug.Log(failed?"[AUDIO_FAIL] checks failed":"[AUDIO_PASS] title/battle/title routing and actual settings volume");
        Application.Quit(failed?2:0);
    }
}

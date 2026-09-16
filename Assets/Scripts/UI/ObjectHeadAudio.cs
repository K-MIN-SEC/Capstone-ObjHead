using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadAudio : MonoBehaviour
{
    private static ObjectHeadAudio instance;
    private AudioSource music;
    private ObjectHeadAudioLibrary library;
    public static float BgmVolume {get=>PlayerPrefs.GetFloat("Audio.BGM",.3f);set{float volume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Audio.BGM",volume);if(instance!=null && instance.music!=null)instance.music.volume=volume;}}
    // Retain the user's SFX preference for future clips; the provisional throw/impact clips were removed.
    public static float SfxVolume {get=>PlayerPrefs.GetFloat("Audio.SFX",.65f);set=>PlayerPrefs.SetFloat("Audio.SFX",Mathf.Clamp01(value));}
    public static void Save()=>PlayerPrefs.Save();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(instance==null)new GameObject("Audio").AddComponent<ObjectHeadAudio>();}
    private void Awake()
    {
        if(instance!=null){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);
        library=Resources.Load<ObjectHeadAudioLibrary>("ObjectHeadAudioLibrary");
        music=gameObject.AddComponent<AudioSource>();
        music.playOnAwake=false;music.volume=BgmVolume;music.loop=true;
        if(library!=null)music.clip=library.music;
        SceneManager.sceneLoaded+=SceneLoaded;
        RefreshMusic();
    }
    private void SceneLoaded(Scene scene,LoadSceneMode mode)=>RefreshMusic();
    private void RefreshMusic()
    {
        // The title component marks a front-end scene; scene renames do not break music routing.
        bool inTitle=FindAnyObjectByType<ObjectHeadTitleScreen>()!=null;
        if(inTitle && music.clip!=null){if(!music.isPlaying)music.Play();}
        else music.Stop();
    }
    private void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(instance==this)instance=null;}
}

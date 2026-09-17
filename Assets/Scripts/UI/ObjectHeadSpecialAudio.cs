using UnityEngine;

// Plays authored WAV assets only. The removed generic throw/impact sounds are not reintroduced.
public sealed class ObjectHeadSpecialAudio:MonoBehaviour
{
    private AudioSource source;private bool loop;
    public static AudioSource Play(string cue,Transform parent=null,bool loop=false)
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return null;
        var library=Resources.Load<ObjectHeadAudioLibrary>("ObjectHeadAudioLibrary");if(library==null)return null;
        var clip=cue=="rotor"?library.helicopterLoop:cue=="mg"?library.supportMachineGun:cue=="rocket"?library.supportRocket:cue=="final"?library.supportFinal:library.rainbowLoop;
        if(clip==null)return null;
        var go=new GameObject("SkillAudio_"+cue);if(parent!=null)go.transform.SetParent(parent,false);
        var player=go.AddComponent<ObjectHeadSpecialAudio>();player.loop=loop;player.source=go.AddComponent<AudioSource>();
        player.source.playOnAwake=false;player.source.clip=clip;player.source.loop=loop;player.source.volume=ObjectHeadAudio.SfxVolume;player.source.Play();return player.source;
    }
    private void Update(){source.volume=ObjectHeadAudio.SfxVolume;if(!loop && !source.isPlaying)Destroy(gameObject);}
    private void OnDisable(){if(source!=null)source.Stop();}
}

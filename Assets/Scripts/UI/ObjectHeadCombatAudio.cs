using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>User-supplied clips, unmodified. Presentation only; never consumes gameplay RNG.</summary>
public sealed class ObjectHeadCombatAudio : MonoBehaviour
{
    public const int VoiceLimit = 12;
    private static ObjectHeadCombatAudio instance;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private readonly AudioSource[] voices = new AudioSource[VoiceLimit];
    private readonly float[] gains = new float[VoiceLimit];
    private readonly int[] priorities = new int[VoiceLimit];
    private readonly float[] started = new float[VoiceLimit];
    private readonly float[] fadeEnds = new float[VoiceLimit];
    private readonly float[] fadeLengths = new float[VoiceLimit];
    private float lastImpactTime = float.NegativeInfinity;
    public const float ImpactDuckDuration = .18f;

    public static string ClipName(string cue)
    {
        switch (cue)
        {
            case "throw": return "머리 던지기 사운드";
            case "shot": return "0490_ShotgunFire_01";
            case "glass": return "0094_MingVaseSmash_01";
            case "dirt": case "land": return "0346_GrenadeImpactSoil_01";
            case "hit": return "0423_OldLadyPunch_01";
            case "explosion": return "0314_Explosion_02";
            case "heavy": return "0245_BunkerBusterExpl";
            case "metal": return "0048_BaseballBatImpactMetal";
            case "aircraft_lr": return "0166_AirplanePassBy_LR";
            case "aircraft_rl": return "0167_AirplanePassBy_RL";
            case "whistle": return "0164_AirStrikeWhistle";
            case "charge_tick": return "0445_SentryEquip";
            case "charge_full": return "0274_CratePickUpCog";
            case "mg": return "0448_SentryFireLoop_03";
            case "rocket": return "0526_StaticWeaponSniperFire_02";
            case "final": return "0284_DPB_EndExpl";
            default: return null;
        }
    }

    public static string ImpactCue(int skillId)
    {
        if (skillId >= 11 && skillId <= 13) return "glass";
        if (skillId >= 21 && skillId <= 23) return "dirt";
        switch (skillId)
        {
            case 31: case 33: case 63: case 101: return "explosion";
            case 32: case 43: return "heavy";
            case 41: return "hit";
            case 105: return "glass";
            case 106: case 108: return "metal";
            case 111: return "final";
            default: return null; // Healing, teleport, erasing and utility are not explosions.
        }
    }

    public static void Play(string cue, Vector2 point, float volume = 1f)
    {
        if (cue == null || ObjectHeadNetworkManager.Instance?.IsDedicatedWorker == true) return;
        if (instance == null) new GameObject("CombatAudio").AddComponent<ObjectHeadCombatAudio>();
        instance.PlayCue(cue, point, volume);
    }

    public static string FlybyCue(Vector2 from, Vector2 to) => to.x >= from.x ? "aircraft_lr" : "aircraft_rl";
    public static void PlayFlyby(Vector2 from, Vector2 to) => Play(FlybyCue(from,to),(from+to)*.5f);

    public static void FadeCue(string cue, float seconds = .06f)
    {
        if (instance == null) return;
        string clipName = ClipName(cue);
        for (int i = 0; i < VoiceLimit; i++)
            if (instance.voices[i].isPlaying && instance.voices[i].clip.name == clipName)
            {
                instance.fadeLengths[i] = Mathf.Max(.01f,seconds);
                instance.fadeEnds[i] = Time.unscaledTime + instance.fadeLengths[i];
            }
    }

    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        for (int i = 0; i < VoiceLimit; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0; // Same weight at close-up and overview zoom.
            source.dopplerLevel = 0;
            voices[i] = source;
        }
        // Decode before the first shot, not on the impact frame.
        foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Combat")) clips[clip.name] = clip;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance == null && ObjectHeadNetworkManager.Instance?.IsDedicatedWorker != true)
            new GameObject("CombatAudio").AddComponent<ObjectHeadCombatAudio>();
    }

    private void PlayCue(string cue, Vector2 point, float volume)
    {
        string name = ClipName(cue);
        if (name == null || !clips.TryGetValue(name, out var clip) || clip == null) return;
        float now = Time.unscaledTime;
        float cooldown = cue == "land" || cue == "dirt" ? .12f : .055f;
        if (lastPlayed.TryGetValue(cue, out float last) && now - last < cooldown) return;
        int priority = CuePriority(cue);
        int slot = -1;
        for (int i = 0; i < VoiceLimit; i++) if (!voices[i].isPlaying) { slot = i; break; }
        if (slot < 0)
            for (int i = 0; i < VoiceLimit; i++)
                if (priorities[i] <= priority && (slot < 0 || priorities[i] < priorities[slot] ||
                    (priorities[i] == priorities[slot] && started[i] < started[slot]))) slot = i;
        if (slot < 0) return;
        lastPlayed[cue] = now;
        float gain = cue == "land" ? .23f : cue == "charge_tick" ? .22f :
            cue == "charge_full" ? .42f : cue == "dirt" ? .35f : cue.StartsWith("aircraft_") ? .38f : .7f;
        var camera = Camera.main;
        // Stereo follows the screen, but distance/zoom never mutes important hits.
        float pan = camera == null ? 0f : Mathf.Clamp((camera.WorldToViewportPoint(point).x - .5f) * .55f, -.3f, .3f);
        // Original stereo already contains the author's left/right balance and movement.
        // Only mono point sounds receive positional panning; never double-pan a stereo flyby.
        if (clip.channels > 1 || cue.StartsWith("charge_")) pan = 0;
        var source = voices[slot];
        source.Stop(); source.clip = clip; source.panStereo = pan;
        priorities[slot] = priority; started[slot] = now;
        fadeEnds[slot] = float.PositiveInfinity;
        fadeLengths[slot] = 1;
        gains[slot] = gain * Mathf.Clamp01(volume);
        if (gains[slot] > 0 && CueTriggersImpactDuck(cue)) lastImpactTime = now;
        // Include the new voice before Play, so even its first audio buffer uses
        // the correct headroom. Existing voices cannot briefly overshoot either.
        source.volume = 0;
        ApplyMix(now, slot);
        source.Play();
    }

    private void Update() => ApplyMix(Time.unscaledTime);

    private void ApplyMix(float now, int startingSlot = -1)
    {
        int foreground = 0, background = 0;
        for (int i=0;i<VoiceLimit;i++)
            if ((voices[i].isPlaying || i == startingSlot) && now<fadeEnds[i])
            { if(priorities[i]>=2) foreground++; else background++; }
        float duck = ImpactDuckEnvelope(now - lastImpactTime);
        float master = Mathf.Clamp01(ObjectHeadAudio.SfxVolume);
        for (int i = 0; i < VoiceLimit; i++)
        {
            if (master <= 0) voices[i].volume = 0;
            float remaining=fadeEnds[i]-now;
            if (remaining<=0) { voices[i].Stop(); voices[i].volume=0; continue; }
            if (!voices[i].isPlaying && i != startingSlot) continue;
            // Important firing/charge cues only share foreground headroom. Dust
            // ducks for the impact transient, never for an entire explosion tail.
            float target = gains[i] * master * MixGain(priorities[i],foreground,background,duck) *
                Mathf.Clamp01(remaining/Mathf.Max(.01f,fadeLengths[i]));
            if (master <= 0 || i == startingSlot) voices[i].volume = target;
            else if (startingSlot >= 0) voices[i].volume = Mathf.Min(voices[i].volume,target);
            else voices[i].volume = Mathf.Lerp(voices[i].volume,target,
                1f-Mathf.Exp(-Time.unscaledDeltaTime*(target<voices[i].volume?60f:12f)));
        }
    }

    public static int CuePriority(string cue)
    {
        switch (cue)
        {
            case "heavy": case "final": return 3;
            case "explosion": case "shot": case "metal": case "glass": case "hit":
            case "mg": case "rocket": case "throw": case "charge_full":
            case "aircraft_lr": case "aircraft_rl": case "whistle": return 2;
            default: return 1;
        }
    }

    public static bool CueTriggersImpactDuck(string cue) => cue=="heavy" || cue=="final" ||
        cue=="explosion" || cue=="metal" || cue=="glass" || cue=="hit";

    public static float ImpactDuckEnvelope(float secondsSinceImpact)
    {
        if (secondsSinceImpact < 0 || secondsSinceImpact >= ImpactDuckDuration) return 0;
        // A brief hold keeps the attack distinct; recovery finishes independently
        // of clip length. A newly accepted impact naturally restarts this window.
        return 1f - Mathf.SmoothStep(0,1,(secondsSinceImpact-.025f)/(ImpactDuckDuration-.025f));
    }

    public static float MixGain(int priority,int foreground,int background,float impactEnvelope = 1f)
        => priority>=2 ? .9f/Mathf.Sqrt(Mathf.Max(1,foreground)) :
            (foreground>0?Mathf.Lerp(1f,.28f,Mathf.Clamp01(impactEnvelope)):1f)/Mathf.Sqrt(Mathf.Max(1,background));

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var voice in voices) voice.Stop();
        lastPlayed.Clear();
        lastImpactTime = float.NegativeInfinity;
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) instance = null;
    }
}

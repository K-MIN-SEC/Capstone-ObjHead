using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadFeelAudit
{
    private static int checks;
    public static void BuildPlaytest()
    {
        Run();
        ObjectHeadAIProgressAudit.Run();
        ObjectHeadTitleLayoutAudit.Run();
        ObjectHeadNetworkSyncAudit.Run();
        ObjectHeadTitleSceneBuilder.BuildWindowsPlaytest();
    }
    public static void Run()
    {
        checks = 0;
        var jump = new ObjectHeadJumpWindow();
        Check(!jump.Tick(0, false, true, true), "cannot jump without ground history");
        Check(jump.Tick(.1f, true, true, false), "pre-landing press is buffered");
        Check(!jump.Tick(.12f, false, true, false), "buffer is consumed once");
        jump.Reset(); jump.Tick(1, true, true, false);
        Check(jump.Tick(1.10f, false, true, true), "late ledge jump within 110 ms");
        Check(!jump.Tick(1.12f, false, true, true), "no second airborne jump");
        jump.Reset(); jump.Tick(2, true, true, false);
        Check(!jump.Tick(2.13f, false, true, true), "coyote grace expires");
        jump.Reset(); jump.Tick(3, false, true, true);
        Check(!jump.Tick(3.16f, true, true, false), "old press does not autojump on landing");
        jump.Reset(); jump.Tick(4, true, true, false); jump.Tick(4.01f, false, false, true);
        Check(!jump.Tick(4.02f, false, true, false), "knockback/control loss clears grace");
        jump.Reset();
        Check(jump.Tick(5,true,true,true),"grounded jump fires");
        Check(!jump.Tick(5.01f,true,true,true),"same ground contact cannot rearm a consumed jump");
        jump.Reset(); jump.Tick(6,false,true,true);
        Check(jump.Tick(6.13f,true,true,false),"140 ms landing buffer accepts early tap");
        var anchor=new Vector2(.72f,.61f);
        var worldBefore=new Vector2((anchor.x-.5f)*20f*1.777f,(anchor.y-.5f)*20f);
        var worldAfter=ObjectHeadCameraController.ZoomAnchorShift(10,6,1.777f,anchor)+
            new Vector2((anchor.x-.5f)*12f*1.777f,(anchor.y-.5f)*12f);
        Check(Vector2.Distance(worldBefore,worldAfter)<.0001f,"zoom keeps the cursor world anchor");
        Check(ObjectHeadCameraController.ZoomAnchorShift(10,6,1.777f,new Vector2(.5f,.5f))==Vector2.zero,
            "keyboard zoom preserves center");
        float one=ObjectHeadCameraController.ResponseBlend(18,.1f);
        float halves=1-Mathf.Pow(1-ObjectHeadCameraController.ResponseBlend(18,.05f),2);
        Check(Mathf.Abs(one-halves)<.00001f,"camera response is framerate independent");
        Check(!ObjectHeadCameraController.AllowsEdgePan(false,false,true,false),
            "edge pan cannot cancel an unfinished zoom");
        Check(!ObjectHeadCameraController.AllowsEdgePan(false,false,false,true),
            "wheel input at the edge does not enter free camera");
        Check(!ObjectHeadCameraController.AllowsEdgePan(true,false,false,false) &&
            !ObjectHeadCameraController.AllowsEdgePan(false,true,false,false),
            "charging and dragging suppress edge pan");
        Check(ObjectHeadCameraController.AllowsEdgePan(false,false,false,false),
            "edge navigation remains available otherwise");
        Check(ObjectHeadCombatAudio.MixGain(3,1,0)==ObjectHeadCombatAudio.MixGain(3,1,10),
            "background voices cannot dilute impact audio");
        Check(ObjectHeadCombatAudio.MixGain(1,1,4)<ObjectHeadCombatAudio.MixGain(1,0,4),
            "background cues duck under impacts");
        Check(ObjectHeadCombatAudio.MixGain(3,4,0)<ObjectHeadCombatAudio.MixGain(3,1,0),
            "multiple explosions retain headroom");
        Check(ObjectHeadCombatAudio.CuePriority("glass")==2 && ObjectHeadCombatAudio.CuePriority("land")==1,
            "confirmed glass contact outranks footsteps");
        Check(ObjectHeadCombatAudio.ImpactDuckEnvelope(0)==1 &&
            ObjectHeadCombatAudio.ImpactDuckEnvelope(.1f)>0 && ObjectHeadCombatAudio.ImpactDuckEnvelope(.1f)<1 &&
            ObjectHeadCombatAudio.ImpactDuckEnvelope(.18f)==0 && ObjectHeadCombatAudio.ImpactDuckEnvelope(4)==0,
            "impact duck ends after the transient, not the explosion file");
        foreach(var cue in new[]{"mg","rocket","throw","charge_full","aircraft_lr","aircraft_rl","whistle"})
            Check(ObjectHeadCombatAudio.CuePriority(cue)>=2 && !ObjectHeadCombatAudio.CueTriggersImpactDuck(cue),
                "important cue is protected and does not duck others: "+cue);
        Check(ObjectHeadCombatAudio.CueTriggersImpactDuck("heavy") && ObjectHeadCombatAudio.CueTriggersImpactDuck("glass"),
            "confirmed impacts trigger a short duck");
        Check(ObjectHeadCombatAudio.MixGain(2,2,4,0)==ObjectHeadCombatAudio.MixGain(2,2,4,1),
            "impact duck never attenuates foreground cues");
        Check(ObjectHeadCombatAudio.MixGain(1,1,1,0)==1 && ObjectHeadCombatAudio.MixGain(1,1,1,1)<.3f,
            "quiet ambience recovers while long impact tail is still playing");
        foreach (float size in new[] { 4f, 5.5f, 18f, 35f })
        {
            var offset = ObjectHeadCameraController.ScreenRelativeShake(size, .036f, .015f, .32f);
            var baseline = ObjectHeadCameraController.ScreenRelativeShake(5.5f, .036f, .015f, .32f);
            Check(Mathf.Abs(offset.magnitude / size - baseline.magnitude / 5.5f) < .00001f,
                "equal screen shake at view size " + size);
            Check(offset.magnitude / (size * 2) * 1080 > 8, "visible overview kick " + size);
        }
        Check(ObjectHeadCameraController.ScreenRelativeShake(18,.036f,.4f,.32f)==Vector3.zero,"shake ends exactly");
        Check(ObjectHeadCameraController.ScreenRelativeShake(18,0,0,.32f)==Vector3.zero,"zero strength disables shake");
        Check(ObjectHeadPresentation.FeedbackImpulse(21,.25f,.1f)==0,"growth stamps cannot shake");
        Check(ObjectHeadPresentation.FeedbackImpulse(11,1,0)>0,"bulb impact has feedback");
        Check(ObjectHeadCombatAudio.ImpactCue(104)==null && ObjectHeadCombatAudio.ImpactCue(110)==null,
            "healing and teleport do not sound like explosions");
        foreach (string cue in new[] {"throw","shot","glass","dirt","hit","explosion","heavy","metal",
            "aircraft_lr","aircraft_rl","whistle","charge_tick","charge_full","mg","rocket","final"})
        {
            string path = "Assets/Resources/Audio/Combat/" + ObjectHeadCombatAudio.ClipName(cue) + ".mp3";
            Check(File.Exists(path), "supplied clip exists: " + cue);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = false;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Check(clip != null && clip.length > .01f && clip.length < 30, "valid decoded audio: " + cue);
            Debug.Log($"[SFX_CHANNELS] {cue}: channels={clip.channels}, length={clip.length:F3}s, monoFold={importer.forceToMono}");
            if (cue.StartsWith("aircraft_"))
            {
                Check(clip.channels==2, cue+" retains two original channels");
                var data=new float[clip.samples*clip.channels];
                Check(clip.GetData(data,0),cue+" decoded samples readable");
                float first=StereoBalance(data,0,clip.samples/3);
                float last=StereoBalance(data,clip.samples*2/3,clip.samples);
                Debug.Log($"[SFX_DIRECTION] {cue}: early balance={first:F3}, late balance={last:F3} (-1 left, +1 right)");
                Check(cue=="aircraft_lr"?first<last:last<first,cue+" channel motion matches flight direction");
            }
        }
        Check(ObjectHeadCombatAudio.FlybyCue(Vector2.left,Vector2.right)=="aircraft_lr","left-to-right bomb sequence selects LR");
        Check(ObjectHeadCombatAudio.FlybyCue(Vector2.right,Vector2.left)=="aircraft_rl","right-to-left bomb sequence selects RL");
        Debug.Log("[FEEL_AUDIT_PASS] " + checks + " movement/camera/audio checks");
    }
    private static float StereoBalance(float[] data,int start,int end)
    {
        double left=0,right=0;
        for(int i=start;i<end;i++){left+=data[i*2]*data[i*2];right+=data[i*2+1]*data[i*2+1];}
        return (float)((right-left)/Math.Max(.0000001,left+right));
    }
    private static void Check(bool ok, string detail)
    {
        if (!ok) throw new Exception("[FEEL_AUDIT_FAIL] " + detail);
        checks++; Debug.Log("[FEEL_CHECK] " + detail);
    }
}

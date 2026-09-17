using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadCompletionAuthoring
{
    public static void FinalizeAndBuild()
    {
        var art=ObjectHeadPresentation.Load();
        if(art.supportTracerSprite==null){art.supportTracerSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/WhitePixel.png").OfType<Sprite>().First();EditorUtility.SetDirty(art);}
        // One-time migration of the clipped count label; preserve all other designer layout.
        foreach(var map in ObjectHeadContent.Load().maps)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var hud=UnityEngine.Object.FindAnyObjectByType<ObjectHeadBattleScreen>();
            var count=hud?.headInventory?.entryTemplate?.transform.Find("Count")?.GetComponent<UnityEngine.UI.Text>();
            if(count!=null && count.rectTransform.sizeDelta.y<=30)
            {count.rectTransform.sizeDelta=new Vector2(42,46);count.fontSize=22;UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);}
        }
        AssetDatabase.SaveAssets();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }
    public static void ApplyAndBuild(){ObjectHeadVacuumAuthoring.Apply();Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Install Gourd and TV")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();var catalog=ObjectHeadContent.Load();
        var heads=ObjectHeadExpansionAuthoring.Slice("TiltedGourdHeads0917",3,1,largestConnectedShape:true);
        const string prefab="Assets/Prefabs/Characters/gourd.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)==null)AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(catalog.Character(ObjectHeadCharacterKind.Bulb).prefab),prefab);
        var root=PrefabUtility.LoadPrefabContents(prefab);root.name="gourd";
        if(root.GetComponent<ObjectHeadGourd>()==null)root.AddComponent<ObjectHeadGourd>();
        var visual=new SerializedObject(root.GetComponent<CharacterVisual>());
        visual.FindProperty("characterKind").enumValueIndex=(int)ObjectHeadCharacterKind.Gourd;
        var images=visual.FindProperty("authoredSkillHeads");images.arraySize=3;
        for(int i=0;i<3;i++)images.GetArrayElementAtIndex(i).objectReferenceValue=heads[i];
        var renderer=(SpriteRenderer)visual.FindProperty("headRenderer").objectReferenceValue;renderer.sprite=heads[0];renderer.transform.localScale=Vector3.one*catalog.characterHeadVisualSize/Mathf.Max(heads[0].bounds.size.x,heads[0].bounds.size.y);
        visual.ApplyModifiedPropertiesWithoutUndo();
        var selector=new SerializedObject(root.GetComponent<DemoSkillSelector>());selector.FindProperty("characterKind").enumValueIndex=(int)ObjectHeadCharacterKind.Gourd;
        var definitions=selector.FindProperty("authoredSkills");definitions.arraySize=3;
        var types=new[]{SkillEffectType.GourdRandom,SkillEffectType.GourdRefill,SkillEffectType.GourdSelect};
        for(int i=0;i<3;i++)
        {
            var skill=Skill("gourd_"+(i+1));skill.nameKey="gourd_skill_"+(i+1);skill.descriptionKey=skill.nameKey+"_description";skill.balancePrefix="skill.gourd."+(i+1);skill.cooldown=i==1?5:0;
            var settings=ObjectHeadSkillSettings.CreateDefault(heads[i],Color.white,Color.white,0,0,0);settings.effectType=types[i];settings.skillId=71+i;skill.settings=settings;EditorUtility.SetDirty(skill);definitions.GetArrayElementAtIndex(i).objectReferenceValue=skill;
        }
        selector.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,prefab);PrefabUtility.UnloadPrefabContents(root);
        var entry=catalog.Character(ObjectHeadCharacterKind.Gourd);
        if(entry==null){entry=new ObjectHeadCharacterDefinition{kind=ObjectHeadCharacterKind.Gourd};catalog.characters=catalog.characters.Concat(new[]{entry}).ToArray();}
        entry.nameKey="gourd_name";entry.descriptionKey="gourd_description";entry.roleKey="role_control";entry.maxHp=95;entry.portrait=heads[0];entry.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        var tv=ObjectHeadSupportAuthoring.ImportSprite("FlatTV0917");var tvSkill=Skill("common_tv");tvSkill.nameKey="common_tv";tvSkill.descriptionKey="common_tv_description";tvSkill.balancePrefix="common.tv";
        var s=ObjectHeadSkillSettings.CreateDefault(tv,Color.white,Color.white,28,.8f,5);s.effectType=SkillEffectType.RainbowSweep;s.skillId=112;s.terrainRadiusPx=25;tvSkill.settings=s;EditorUtility.SetDirty(tvSkill);
        var common=catalog.Common(CommonHeadType.RetroTV);if(common==null){common=new ObjectHeadCommonDefinition{type=CommonHeadType.RetroTV};catalog.commonHeads=catalog.commonHeads.Concat(new[]{common}).ToArray();}
        common.nameKey="common_tv";common.sprite=tv;common.skill=tvSkill;common.use=ObjectHeadCommonUse.Projectile;common.worldVisualSize=.78f;common.spawnCount=1;
        var art=ObjectHeadPresentation.Load();art.rainbowCat=ObjectHeadSupportAuthoring.ImportSprite("FlatRainbowCat0917");
        var poses=ObjectHeadExpansionAuthoring.Slice("SupportFiringSheet0917",3,2,"Presentation",uniformCells:true);
        art.supportMachineGunFrames=poses.Take(3).ToArray();art.supportLauncherFrames=poses.Skip(3).Take(3).ToArray();
        art.supportPilot=poses[0];art.supportLauncherPilot=poses[3];art.supportHelicopter=ObjectHeadSupportAuthoring.ImportSprite("FlatHelicopter0917");
        art.supportPilotOffset=new Vector2(.88f,-.13f);art.supportPilotSize=1.28f;
        art.supportRotorOffset=new Vector2(.63f,.78f);art.supportRotorWidth=3.7f;
        art.supportDoorMaskSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/WhitePixel.png").OfType<Sprite>().First();
        var profiles=art.skills.ToList();foreach(int id in new[]{71,72,73,112})if(!profiles.Any(p=>p.skillId==id))
        {var basis=art.Find(id==112?12:42);profiles.Add(new ObjectHeadSkillPresentation{skillId=id,impactPrefab=basis.impactPrefab,impactScale=.65f,cameraImpulse=.08f});}
        art.skills=profiles.ToArray();EditorUtility.SetDirty(art);EditorUtility.SetDirty(catalog);
        Audio();AssetDatabase.SaveAssets();ObjectHeadActionCueAuthoring.Apply();ObjectHeadServerCatalogExporter.Export();Debug.Log("[COMPLETION_AUTHORING] Gourd, TV, support variants and authored audio installed.");
    }
    private static ObjectHeadSkillDefinition Skill(string name)
    {
        string path="Assets/GameData/Skills/"+name+".asset";var skill=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(path);
        if(skill==null){skill=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();AssetDatabase.CreateAsset(skill,path);}return skill;
    }
    private static void Audio()
    {
        var library=Resources.Load<ObjectHeadAudioLibrary>("ObjectHeadAudioLibrary");
        library.helicopterLoop=Wave("support_rotor",2,0);library.supportMachineGun=Wave("support_mg",.12f,1);
        library.supportRocket=Wave("support_rocket",.7f,2);library.supportFinal=Wave("support_final",1.1f,3);library.rainbowLoop=Wave("rainbow_cat",1.6f,4);EditorUtility.SetDirty(library);
    }
    // Offline sound design: files are ordinary replaceable PCM WAV assets, never runtime synthesis.
    private static AudioClip Wave(string name,float duration,int kind)
    {
        string path="Assets/Art/Audio/"+name+".wav";
        if(!File.Exists(path))
        {
            const int rate=44100;int count=(int)(duration*rate);var rng=new System.Random(917+kind);double low=0,phase=0;
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+count*2);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                stream.Write(16);stream.Write((short)1);stream.Write((short)1);stream.Write(rate);stream.Write(rate*2);stream.Write((short)2);stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(count*2);
                for(int i=0;i<count;i++)
                {
                    double t=i/(double)rate,noise=rng.NextDouble()*2-1;low=low*.93+noise*.07;
                    double fade=Math.Min(1,t*100)*Math.Min(1,(duration-t)*40),sample;
                    if(kind==0)sample=(low*2+Math.Sin(t*2*Math.PI*72)*.12)*(.45+.55*Math.Pow(.5+.5*Math.Sin(t*2*Math.PI*24),3))*.35;
                    else if(kind==1)sample=(noise*.35+Math.Sin(t*2*Math.PI*85)*.35)*Math.Exp(-t*45);
                    else if(kind==2)sample=(low*2+noise*.12)*Math.Sin(Math.PI*t/duration)*.5;
                    else if(kind==3)sample=(low*3+Math.Sin(2*Math.PI*(70*t-18*t*t))*.5)*Math.Exp(-t*4)*.55;
                    else {double note=t%.4/ .4,frequency=420+220*Math.Sin(note*Math.PI);phase+=frequency*2*Math.PI/rate;sample=(Math.Sin(phase)+.25*Math.Sin(phase*2))*.17*Math.Pow(Math.Sin(note*Math.PI),.6);}
                    stream.Write((short)(Math.Max(-.85,Math.Min(.85,sample*fade))*32767));
                }
            }
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}

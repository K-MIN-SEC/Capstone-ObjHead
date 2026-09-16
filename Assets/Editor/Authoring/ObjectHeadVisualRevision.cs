using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, one-off scene authoring pass; ordinary builds never call this.</summary>
public static class ObjectHeadVisualRevision
{
    private const string Art="Assets/Art/Presentation/";
    private static Material material;

    // Explicit authoring operation only. The saved Transform stays designer-editable.
    public static void ExpandBackgroundAndBuild()
    {
        foreach (var map in ObjectHeadContent.Load().maps)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var background=GameObject.Find("Background").GetComponent<SpriteRenderer>();
            var authoring=UnityEngine.Object.FindAnyObjectByType<ObjectHeadMapAuthoring>();
            if (!authoring.TryGetWaterSurfaceY(out float waterY)) throw new Exception("Missing water marker");
            var size=background.sprite.bounds.size;
            background.transform.localScale=new Vector3(140f/size.x,90f/size.y,1f);
            background.transform.position=new Vector3(0,waterY+90f*(.5f-.166f),8);
            EditorSceneManager.SaveScene(scene);
        }
        CheckZoom();
        ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }

    public static void ApplyAndBuild()
    {
        CreatePanel();
        Import(Art+"FlatEffects.png",true);
        Import(Art+"IslandSoil.png",true);
        Import("Assets/Art/Title/ObjectHeadKeyArt.png",false);
        CreateEffects();
        var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        ObjectHeadReleaseAuthoring.ReplaceVisualLayout(root.GetComponentInChildren<ObjectHeadTitleScreen>(true),"TitleLayout");
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        PrefabUtility.UnloadPrefabContents(root);
        var catalog=ObjectHeadContent.Load();
        foreach(var character in catalog.characters)
        {
            string path=AssetDatabase.GetAssetPath(character.prefab);
            var instance=PrefabUtility.LoadPrefabContents(path);
            var combat=new SerializedObject(instance.GetComponent<CharacterCombat>());
            combat.FindProperty("healthLabelCharacterSize").floatValue=.075f;
            combat.FindProperty("healthLabelShadowColor").colorValue=Color.black;
            combat.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance,path);PrefabUtility.UnloadPrefabContents(instance);
        }
        foreach(var map in catalog.maps)
        {
            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/"+map.id+".asset");
            JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Editor/Authoring/"+map.id+".json"),recipe);
            recipe.soilTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"IslandSoil.png");
            recipe.edgeColor=new Color32(40,38,26,255);
            ObjectHeadMapRecipeEditor.Bake(recipe);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var screen=UnityEngine.Object.FindAnyObjectByType<ObjectHeadBattleScreen>();
            ObjectHeadReleaseAuthoring.ReplaceVisualLayout(screen,"BattleLayout");
            var camera=UnityEngine.Object.FindAnyObjectByType<ObjectHeadCameraController>();
            var serialized=new SerializedObject(camera);
            serialized.FindProperty("wheelZoomFraction").floatValue=.16f;
            serialized.FindProperty("minSize").floatValue=3f;
            serialized.FindProperty("maxSize").floatValue=22f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        CheckZoom();
        ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }

    private static void CheckZoom()
    {
        if(ObjectHeadCameraController.NormalizeWheelNotches(1,true,120)!=1 ||
           ObjectHeadCameraController.NormalizeWheelNotches(120,false,120)!=1)
            throw new Exception("Normalized and raw wheel input must produce the same notch count");
        float a=ObjectHeadCameraController.WheelZoomSize(7.5f,1,.16f);
        if(Mathf.Abs(a-6.3f)>.0001f)throw new Exception("Wheel zoom step failed");
        float b=ObjectHeadCameraController.WheelZoomSize(a,-1,.16f);
        if(Mathf.Abs(b-7.5f)>.0001f)throw new Exception("Wheel zoom inverse failed");
        Debug.Log("[VISUAL_TEST] Wheel notch 7.5 -> 6.3 -> 7.5; no deltaTime dependence.");
    }

    private static void Import(string path,bool readable)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);
        if(i==null)throw new FileNotFoundException(path);
        i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;
        i.isReadable=readable;i.alphaIsTransparency=true;i.mipmapEnabled=false;
        i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;
        i.filterMode=FilterMode.Bilinear;i.wrapMode=path.Contains("Soil")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
        i.SaveAndReimport();
    }

    private static void CreatePanel()
    {
        Directory.CreateDirectory("Assets/Art/UI");
        var t=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-20),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-20);
            float d=Mathf.Sqrt(dx*dx+dy*dy);
            t.SetPixel(x,y,d>11?Color.clear:d>8?new Color(.08f,.09f,.09f,1):Color.white);
        }
        t.Apply();File.WriteAllBytes("Assets/Art/UI/InkPanel.png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
        Import("Assets/Art/UI/InkPanel.png",false);
        var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/UI/InkPanel.png");
        importer.spriteBorder=new Vector4(15,15,15,15);importer.SaveAndReimport();
    }

    private static void CreateEffects()
    {
        Directory.CreateDirectory("Assets/Prefabs/Effects");
        AssetDatabase.Refresh();
        material=AssetDatabase.LoadAssetAtPath<Material>(Art+"InkParticles.mat");
        if(material==null){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,Art+"InkParticles.mat");}
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"FlatEffects.png");
        // Slicing is metadata, not a raster edit. The generated alpha remains untouched.
        var importer=(TextureImporter)AssetImporter.GetAtPath(Art+"FlatEffects.png");
        importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritesheet=Enumerable.Range(0,4).Select(i=>new SpriteMetaData {
            name=new[]{"Spark","Leaves","Blast","Smoke"}[i],pivot=new Vector2(.5f,.5f),
            rect=new Rect((i%2)*texture.width/2,(1-i/2)*texture.height/2,texture.width/2,texture.height/2)
        }).ToArray();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Art+"FlatEffects.png").OfType<Sprite>().ToArray();
        Sprite Get(string name)=>sprites.First(s=>s.name==name);
        var launch=Effect("Launch",Get("Smoke"),Get("Spark"),.18f,.45f,5);
        var spark=Effect("ElectricImpact",Get("Spark"),Get("Spark"),.32f,2f,12);
        var leaves=Effect("SeedImpact",Get("Leaves"),Get("Leaves"),.5f,1.7f,8);
        var blast=Effect("BombImpact",Get("Blast"),Get("Smoke"),.38f,2.1f,9);
        var flight=new GameObject("FlightDust");
        var ps=Particle(flight,"Dust",Get("Smoke"),.3f,.11f,0,0);
        var main=ps.main;main.loop=true;main.duration=1;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=14;emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        var trail=PrefabUtility.SaveAsPrefabAsset(flight,"Assets/Prefabs/Effects/FlightDust.prefab");UnityEngine.Object.DestroyImmediate(flight);
        var library=AssetDatabase.LoadAssetAtPath<ObjectHeadPresentation>("Assets/Resources/ObjectHeadPresentation.asset");
        if(library==null){library=ScriptableObject.CreateInstance<ObjectHeadPresentation>();AssetDatabase.CreateAsset(library,"Assets/Resources/ObjectHeadPresentation.asset");}
        library.skills=Enumerable.Range(1,3).SelectMany(kind=>Enumerable.Range(1,3).Select(slot=>new ObjectHeadSkillPresentation{
            skillId=kind*10+slot,launchPrefab=launch,flightPrefab=trail,impactPrefab=kind==1?spark:kind==2?leaves:blast,
            impactScale=slot==3?1.05f:.9f
        })).ToArray();EditorUtility.SetDirty(library);
    }

    private static GameObject Effect(string name,Sprite center,Sprite scatter,float duration,float size,int count)
    {
        var root=new GameObject(name);
        Particle(root,"Impact",center,duration,size,0,1);
        Particle(root,"Fragments",scatter,duration*1.6f,size*.21f,2.6f,count);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Effects/"+name+".prefab");
        UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    private static ParticleSystem Particle(GameObject root,string name,Sprite sprite,float duration,float size,float speed,int count)
    {
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);
        var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=p.main;main.duration=duration;main.loop=false;main.startLifetime=duration;main.startSpeed=speed;main.startSize=size;
        main.startRotation=new ParticleSystem.MinMaxCurve(-.35f,.35f);main.maxParticles=32;main.playOnAwake=true;
        main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        var emission=p.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
        var shape=p.shape;shape.enabled=speed>0;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.07f;
        var sheet=p.textureSheetAnimation;sheet.enabled=true;sheet.mode=ParticleSystemAnimationMode.Sprites;sheet.AddSprite(sprite);
        var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=45;
        var sizeOver=p.sizeOverLifetime;sizeOver.enabled=true;
        sizeOver.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.18f,1),new Keyframe(1,.55f)));
        var color=p.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.55f),new GradientAlphaKey(0,1)});color.color=gradient;
        return p;
    }
}

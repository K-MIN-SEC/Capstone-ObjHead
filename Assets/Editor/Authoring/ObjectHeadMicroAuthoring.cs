using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

public static class ObjectHeadMicroAuthoring
{
    [MenuItem("Object Head/Visuals/Install Micro Feedback")]
    public static void Apply()
    {
        var microArt=SliceMicroArt();
        const string path="Assets/Resources/ObjectHeadMicroFeedback.asset";
        if(AssetDatabase.LoadAssetAtPath<ObjectHeadMicroFeedback>(path)==null)
        {
            var art=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Presentation/TacticalEffects.png").OfType<Sprite>().ToArray();
            Sprite Art(int i)=>art.Single(s=>s.name=="TacticalEffects_"+i);
            var config=ScriptableObject.CreateInstance<ObjectHeadMicroFeedback>();
            config.profiles=new[]{
                Profile(ObjectHeadMicroCue.Debris,microArt[0],12,new Vector2(.12f,.3f),new Vector2(.45f,.85f),new Vector2(1.6f,4),7,80,.45f,Color.white),
                Profile(ObjectHeadMicroCue.Dust,microArt[1],5,new Vector2(.24f,.5f),new Vector2(.4f,.7f),new Vector2(.3f,1),0,75,.5f,new Color(.78f,.69f,.53f,.5f)),
                Profile(ObjectHeadMicroCue.Step,microArt[1],2,new Vector2(.1f,.19f),new Vector2(.16f,.28f),new Vector2(.2f,.5f),0,25,.06f,new Color(.83f,.76f,.63f,.4f)),
                Profile(ObjectHeadMicroCue.Jump,microArt[1],4,new Vector2(.12f,.24f),new Vector2(.2f,.38f),new Vector2(.4f,1),.5f,90,.12f,new Color(.88f,.82f,.69f,.5f)),
                Profile(ObjectHeadMicroCue.Land,microArt[1],6,new Vector2(.14f,.28f),new Vector2(.22f,.42f),new Vector2(.5f,1.2f),1,85,.17f,new Color(.88f,.82f,.69f,.55f)),
                Profile(ObjectHeadMicroCue.Launch,microArt[1],3,new Vector2(.09f,.18f),new Vector2(.12f,.24f),new Vector2(.6f,1.4f),0,25,.06f,new Color(1,1,1,.55f)),
                Profile(ObjectHeadMicroCue.Hit,Art(7),3,new Vector2(.07f,.16f),new Vector2(.12f,.23f),new Vector2(.7f,1.6f),1,140,.14f,Color.white),
                Profile(ObjectHeadMicroCue.Water,microArt[2],10,new Vector2(.1f,.26f),new Vector2(.3f,.65f),new Vector2(1.3f,3),5,60,.3f,Color.white)
            };
            foreach(var p in config.profiles)if(p.cue!=ObjectHeadMicroCue.Debris&&p.cue!=ObjectHeadMicroCue.Water){p.spin=35;p.endScale=1.6f;}
            AssetDatabase.CreateAsset(config,path);
        }
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/UI"}))
        {
            string prefabPath=AssetDatabase.GUIDToAssetPath(guid);
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try{if(Attach(root)>0)PrefabUtility.SaveAsPrefabAsset(root,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var map in ObjectHeadContent.Load().maps)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            int added=0;foreach(var root in scene.GetRootGameObjects())added+=Attach(root);
            if(added>0)EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("[MICRO_AUTHORING] Editable small effects and authored button feedback installed.");
    }
    private static Sprite[] SliceMicroArt()
    {
        const string atlas="Assets/Art/Presentation/TacticalEffects.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(atlas);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var rects=provider.GetSpriteRects().ToList();
        string[] names={"MicroRock","MicroDust","MicroDrop"};
        // Alpha-component bounds measured in the source 1254px atlas; metadata only, no raster rewrite.
        Rect[] bounds={new Rect(1050,731,66,69),new Rect(307,552,73,71),new Rect(994,1065,82,127)};
        for(int i=0;i<names.Length;i++)if(rects.All(r=>r.name!=names[i]))
            rects.Add(new SpriteRect{name=names[i],rect=bounds[i],pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=GUID.Generate()});
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var art=AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().ToArray();
        return names.Select(n=>art.Single(s=>s.name==n)).ToArray();
    }
    public static void RefineAndBuild()
    {
        // Explicit one-time crop correction; Apply preserves subsequently authored rectangles.
        var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/Presentation/TacticalEffects.png");
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var rects=provider.GetSpriteRects();
        foreach(var r in rects){if(r.name=="MicroDust")r.rect=new Rect(307,552,73,71);if(r.name=="MicroRock")r.rect=new Rect(1050,731,66,69);if(r.name=="MicroDrop")r.rect=new Rect(994,1065,82,127);}
        provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
        var art=SliceMicroArt();
        var config=AssetDatabase.LoadAssetAtPath<ObjectHeadMicroFeedback>("Assets/Resources/ObjectHeadMicroFeedback.asset");
        foreach(var p in config.profiles)
            if(p.cue!=ObjectHeadMicroCue.Hit)p.sprite=p.cue==ObjectHeadMicroCue.Debris?art[0]:p.cue==ObjectHeadMicroCue.Water?art[2]:art[1];
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }
    private static int Attach(GameObject root)
    {
        int added=0;
        foreach(var button in root.GetComponentsInChildren<Button>(true))
            if(button.GetComponent<ObjectHeadButtonFeedback>()==null){button.gameObject.AddComponent<ObjectHeadButtonFeedback>();added++;}
        return added;
    }
    private static ObjectHeadMicroProfile Profile(ObjectHeadMicroCue cue,Sprite sprite,int count,Vector2 size,Vector2 life,Vector2 speed,float gravity,float spread,float scatter,Color color)
        =>new ObjectHeadMicroProfile{cue=cue,sprite=sprite,count=count,size=size,lifetime=life,speed=speed,gravity=gravity,spread=spread,scatter=scatter,tint=color};
    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Explicit one-time authoring migration. Ordinary builds never reset designer edits.</summary>
public static class ObjectHeadTacticsAuthoring
{
    private const string Atlas="Assets/Art/Presentation/TacticalEffects.png";
    [MenuItem("Object Head/Visuals/Install Tactical Effects and Camera")]
    public static void Apply()
    {
        Asset<ObjectHeadAITuning>("ObjectHeadAITuning");
        Asset<ObjectHeadCameraTuning>("ObjectHeadCameraTuning");
        Sprite[] sprites=Slice();
        var healing=Effect("HealingBloom",sprites[0],.8f,.55f,0);
        var magnet=Effect("MagneticWave",sprites[1],.65f,0,45);
        var water=Effect("WaterSplash",sprites[2],.6f,.12f,-8);
        var steam=Effect("SteamBurst",sprites[3],.9f,.7f,12);
        var glass=Effect("GlassShatter",sprites[4],.5f,.12f,24);
        var dirt=Effect("EarthBurst",sprites[5],.6f,.1f,-15);
        var seal=Effect("LockSnap",sprites[6],.5f,.2f,8);
        var muzzle=Effect("BulletImpact",sprites[7],.22f,0,0);
        var warning=Marker("StrikeWarning",sprites[8],Color.white);
        var healingWarning=Marker("HealingWarning",sprites[0],new Color(1,1,1,.65f));
        var lockWarning=Marker("LockWarning",sprites[6],new Color(1,1,1,.65f));
        var library=ObjectHeadPresentation.Load();var list=library.skills.ToList();
        var basis=list.First();
        foreach(int id in new[]{101,102,103})if(list.All(p=>p.skillId!=id))
            list.Add(new ObjectHeadSkillPresentation{skillId=id,launchPrefab=basis.launchPrefab,flightPrefab=basis.flightPrefab});
        foreach(var p in list)
        {
            switch(p.skillId)
            {
                case 11:p.impactPrefab=glass;break;
                case 21:case 22:case 102:p.impactPrefab=dirt;break;
                case 41:p.impactPrefab=muzzle;p.launchPrefab=muzzle;p.cameraImpulse=.04f;break;
                case 42:case 104:p.impactPrefab=healing;p.cameraImpulse=0;break;
                case 51:case 52:case 53:p.impactPrefab=magnet;p.cameraImpulse=.035f;break;
                case 61:p.impactPrefab=healing;break;
                case 62:p.impactPrefab=steam;break;
                case 63:p.impactPrefab=water;p.cameraImpulse=.1f;break;
                case 105:p.impactPrefab=glass;break;
                case 106:p.impactPrefab=seal;break;
                case 107:p.impactPrefab=steam;break;
                case 108:p.impactPrefab=seal;p.cameraImpulse=.07f;break;
                case 101:p.impactPrefab=list.First(x=>x.skillId==31).impactPrefab;p.cameraImpulse=.12f;break;
                case 103:p.impactPrefab=steam;break;
                case 31:case 32:case 33:case 43:p.cameraImpulse=.13f;break;
            }
            if(p.skillId==42)p.warningPrefab=healingWarning;
            if(p.skillId==43)p.warningPrefab=warning;
            if(p.skillId==108)p.warningPrefab=lockWarning;
        }
        library.skills=list.ToArray();EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("[TACTICS_AUTHORING] Editable VFX prefabs, 26 skill profiles, AI and camera assets saved.");
    }
    public static void ApplyAndBuild(){Apply();WidenIslands();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    public static void Build(){ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    private static T Asset<T>(string name) where T:ScriptableObject
    {
        string path="Assets/Resources/"+name+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);
        if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}
        return asset;
    }
    private static Sprite[] Slice()
    {
        AssetDatabase.ImportAsset(Atlas,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=256;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.isReadable=true;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
        if(!texture.GetPixels32().Any(c=>c.a==0))throw new Exception("Tactical art must have real transparent alpha.");
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();
        var rects=Enumerable.Range(0,9).Select(i=>new SpriteRect{
            name="TacticalEffects_"+i,rect=new Rect(i%3*texture.width/3f,(2-i/3)*texture.height/3f,texture.width/3f,texture.height/3f),
            pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,
            spriteID=old.FirstOrDefault(r=>r.name=="TacticalEffects_"+i)?.spriteID??GUID.Generate()}).ToArray();
        rects=rects.Concat(old.Where(r=>!r.name.StartsWith("TacticalEffects_"))).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var all=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();
        return Enumerable.Range(0,9).Select(i=>all.Single(s=>s.name=="TacticalEffects_"+i)).ToArray();
    }
    private static GameObject Effect(string name,Sprite sprite,float duration,float rise,float spin)
    {
        string path="Assets/Prefabs/Effects/"+name+".prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        var root=new GameObject(name);var child=new GameObject("Artwork",typeof(SpriteRenderer));child.transform.SetParent(root.transform,false);
        var renderer=child.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=40;
        child.transform.localScale=Vector3.one/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);
        var motion=child.AddComponent<ObjectHeadSpriteEffect>();motion.duration=duration;motion.rise=rise;motion.rotationDegrees=spin;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
    }
    private static GameObject Marker(string name,Sprite sprite,Color color)
    {
        string path="Assets/Prefabs/Effects/"+name+".prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        var root=new GameObject(name,typeof(ObjectHeadLandingMarker));var child=new GameObject("Area",typeof(SpriteRenderer));child.transform.SetParent(root.transform,false);
        var renderer=child.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=23;
        child.transform.localScale=Vector3.one/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
    }
    [MenuItem("Object Head/Maps/Widen Islands To 60 World Units")]
    public static void WidenIslands()
    {
        foreach(var map in ObjectHeadContent.Load().maps)
        {
            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/"+map.id+".asset");
            // One-time horizontal expansion only: preserve heights, slopes become more walkable.
            if(recipe.width!=1536 || recipe.pixelsPerUnit!=32)continue;
            float factor=1.25f;
            foreach(var polygon in recipe.polygons)for(int i=0;i<polygon.points.Length;i++)polygon.points[i].x*=factor;
            recipe.width=1920;recipe.origin.x*=factor;
            ObjectHeadMapRecipeEditor.Bake(recipe);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var terrain=Object.FindAnyObjectByType<TerrainManager>();
            var serialized=new SerializedObject(terrain);
            serialized.FindProperty("terrainOriginWorld").vector2Value=recipe.origin;
            serialized.ApplyModifiedPropertiesWithoutUndo();terrain.transform.position=recipe.origin;
            var layout=Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
            if(layout!=null)foreach(var mode in layout.layouts)foreach(var seat in mode.seats)foreach(var point in seat.characterSlots)
                if(point!=null)point.position=new Vector3(point.position.x*factor,point.position.y,point.position.z);
            foreach(var water in Object.FindObjectsByType<WaterZone>(FindObjectsSortMode.None))
                if(water.TryGetComponent<BoxCollider2D>(out var box))box.size=new Vector2(76,box.size.y);
            EditorSceneManager.SaveScene(scene);EditorUtility.SetDirty(recipe);
        }
        AssetDatabase.SaveAssets();
    }
}

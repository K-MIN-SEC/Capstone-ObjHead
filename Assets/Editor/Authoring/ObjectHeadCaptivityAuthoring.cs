using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ObjectHeadCaptivityAuthoring
{
    [MenuItem("Object Head/Content/Add Lock Common Head")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        var lockSprite=Import("Assets/Art/Characters/Padlock.png",1)[0];
        var frames=Import("Assets/Art/Presentation/IronMaiden.png",4);
        const string prefabPath="Assets/Prefabs/Effects/IronMaiden.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null)
        {
            var root=new GameObject("IronMaiden");root.transform.localPosition=new Vector3(0,.16f,0);
            var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=frames[0];renderer.sortingOrder=40;
            root.transform.localScale=Vector3.one*(1.8f/frames[0].bounds.size.y);
            var visual=root.AddComponent<ObjectHeadCageVisual>();visual.cageRenderer=renderer;
            visual.closed=frames[0];visual.halfOpen=frames[1];visual.open=frames[2];visual.impact=frames[3];
            prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Object.DestroyImmediate(root);
        }
        const string settingsPath="Assets/Resources/ObjectHeadCaptivity.asset";
        var settings=AssetDatabase.LoadAssetAtPath<ObjectHeadCaptivityDefinition>(settingsPath);
        if(settings==null){settings=ScriptableObject.CreateInstance<ObjectHeadCaptivityDefinition>();settings.cagePrefab=prefab.GetComponent<ObjectHeadCageVisual>();AssetDatabase.CreateAsset(settings,settingsPath);}
        const string skillPath="Assets/GameData/Skills/common_lock.asset";
        var skill=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(skillPath);
        if(skill==null)
        {
            skill=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();skill.balancePrefix="common.lock";skill.nameKey="common_lock";skill.descriptionKey="common_lock_description";
            var s=ObjectHeadSkillSettings.CreateDefault(lockSprite,Color.white,new Color(1,.72f,.2f),0,.85f,0);
            s.effectType=SkillEffectType.Captivity;s.skillId=108;s.projectileVisualDiameter=.42f;s.commonHeadTypeId=(int)CommonHeadType.Lock;
            skill.settings=s;AssetDatabase.CreateAsset(skill,skillPath);
        }
        var catalog=ObjectHeadContent.Load();
        if(catalog.Common(CommonHeadType.Lock)==null)
        {
            catalog.commonHeads=catalog.commonHeads.Concat(new[]{new ObjectHeadCommonDefinition{type=CommonHeadType.Lock,nameKey="common_lock",sprite=lockSprite,skill=skill,spawnCount=1}}).ToArray();
            EditorUtility.SetDirty(catalog);
        }
        var presentation=ObjectHeadPresentation.Load();
        if(presentation.Find(108)==null)
        {
            // Uses authored ink effects, never creates a competing procedural style.
            var basis=presentation.Find(31);
            presentation.skills=presentation.skills.Concat(new[]{new ObjectHeadSkillPresentation{skillId=108,
                launchPrefab=basis.launchPrefab,flightPrefab=basis.flightPrefab,impactPrefab=basis.impactPrefab,impactScale=.25f}}).ToArray();
            EditorUtility.SetDirty(presentation);
        }
        AssetDatabase.SaveAssets();
    }
    private static Sprite[] Import(string path,int columns)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=true;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects();
        var rects=Enumerable.Range(0,columns).Select(i=>new SpriteRect{name=System.IO.Path.GetFileNameWithoutExtension(path)+"_"+i,
            rect=new Rect(i*(texture.width/(float)columns),0,texture.width/(float)columns,texture.height),
            pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Custom,spriteID=i<previous.Length?previous[i].spriteID:GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
    }
}

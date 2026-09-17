using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit migration only. Normal play/build leaves all authored transforms editable.</summary>
public static class ObjectHeadSupportAuthoring
{
    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Install Cave and Support Revision")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        var content=ObjectHeadContent.Load();content.commonInventoryCapacity=6;
        var crystal=ImportSprite("CommonCrystal0917");var heavy=ImportSprite("CommonHeavy0917");
        AddCommon(content,CommonHeadType.CrystalOrb,"common_crystal",crystal,SkillEffectType.Teleport,110,0,1,0,0);
        AddCommon(content,CommonHeadType.HeavyWeapon,"common_heavy",heavy,SkillEffectType.HelicopterSupport,111,40,3.2f,75,10);
        var art=ObjectHeadPresentation.Load();art.supportHelicopter=ImportSprite("SupportHelicopter0917");art.supportPilot=ImportSprite("SupportPilot0917");art.supportHeavyHead=heavy;
        art.supportPilotOffset=new Vector2(.55f,-.35f);art.supportPilotSize=1.05f;art.airstrikeBombSize=2.1f;
        var profiles=art.skills.ToList();
        foreach(int id in new[]{43,108,110,111})
        {
            var p=profiles.FirstOrDefault(x=>x.skillId==id);
            if(p==null){p=new ObjectHeadSkillPresentation{skillId=id};profiles.Add(p);}
            var source=art.Find(id==110?42:43)??art.skills.First(s=>s.impactPrefab!=null);
            if(p.impactPrefab==null)p.impactPrefab=source.impactPrefab;
            p.impactScale=id==110?.6f:1.1f;p.cameraImpulse=id==110?.06f:.3f;
        }
        art.skills=profiles.ToArray();EditorUtility.SetDirty(art);
        var cage=ObjectHeadCaptivityDefinition.Load().cagePrefab;
        var cagePath=AssetDatabase.GetAssetPath(cage);var root=PrefabUtility.LoadPrefabContents(cagePath);
        var cageVisual=root.GetComponent<ObjectHeadCageVisual>();var r=cageVisual.cageRenderer;
        root.transform.localScale*=3.2f/Mathf.Max(.01f,r.bounds.size.y);
        cageVisual.fallSeconds=.65f;cageVisual.closeSeconds=.25f;
        PrefabUtility.SaveAsPrefabAsset(root,cagePath);PrefabUtility.UnloadPrefabContents(root);
        var micro=ObjectHeadMicroFeedback.Load();var debris=micro.Find(ObjectHeadMicroCue.Debris);
        debris.count=22;debris.size=new Vector2(.16f,.38f);debris.lifetime=new Vector2(.8f,1.35f);debris.speed=new Vector2(2,5);debris.gravity=8;
        micro.particleBudget=240;micro.emissionsPerFrame=80;EditorUtility.SetDirty(micro);
        var camera=ObjectHeadCameraTuning.Load();camera.settlementHoldSeconds=1.6f;camera.oceanViewPadding=7;camera.bottomHudSafeFraction=.22f;camera.shakeSeconds=.32f;camera.shakeStrength=.9f;EditorUtility.SetDirty(camera);
        ReviseHarbor();CreateCavern(content);
        foreach(var map in content.maps)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            ExpandHud(UnityEngine.Object.FindAnyObjectByType<ObjectHeadBattleScreen>());
            EditorSceneManager.SaveScene(scene);
        }
        var network=AssetDatabase.LoadAssetAtPath<ObjectHeadNetworkConfig>("Assets/Resources/ObjectHeadNetworkConfig.asset");var so=new SerializedObject(network);
        var pool=so.FindProperty("defaultRoomSettings").FindPropertyRelative("randomMapPool");pool.arraySize=content.maps.Length;
        var bindings=so.FindProperty("mapScenes");bindings.arraySize=content.maps.Length;
        for(int i=0;i<content.maps.Length;i++){pool.GetArrayElementAtIndex(i).stringValue=content.maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("mapId").stringValue=content.maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName").stringValue=content.maps[i].sceneName;}
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/ObjectHeadTitle.unity",true)}.Concat(content.maps.Select(m=>new EditorBuildSettingsScene("Assets/Scenes/"+m.sceneName+".unity",true))).ToArray();
        EditorUtility.SetDirty(content);AssetDatabase.SaveAssets();
        Debug.Log("[SUPPORT_AUTHORING] Cave map, six shared slots, editable support assets installed.");
    }
    private static void AddCommon(ObjectHeadContent c,CommonHeadType type,string key,Sprite sprite,SkillEffectType effect,int id,int damage,float radius,int terrain,float force)
    {
        string path="Assets/GameData/Skills/"+key+".asset";var skill=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(path);
        if(skill==null){skill=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();AssetDatabase.CreateAsset(skill,path);}
        skill.balancePrefix=key=="common_crystal"?"common.crystal":"common.heavy";skill.nameKey=key;skill.descriptionKey=key+"_description";
        skill.settings=ObjectHeadSkillSettings.CreateDefault(sprite,Color.white,new Color(1,.65f,.15f,.8f),damage,radius,force);
        var settings=skill.settings;settings.effectType=effect;settings.skillId=id;settings.terrainRadiusPx=terrain;settings.resolveAtTurnEnd=effect==SkillEffectType.HelicopterSupport;settings.projectileVisualDiameter=.85f;skill.settings=settings;EditorUtility.SetDirty(skill);
        var list=c.commonHeads.ToList();var item=list.FirstOrDefault(x=>x.type==type);if(item==null){item=new ObjectHeadCommonDefinition{type=type};list.Add(item);}
        item.nameKey=key;item.sprite=sprite;item.skill=skill;item.use=ObjectHeadCommonUse.Projectile;item.worldVisualSize=.78f;item.spawnCount=1;c.commonHeads=list.ToArray();
    }
    public static Sprite ImportSprite(string name)
    {
        string path="Assets/Art/Presentation/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
        for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>127){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var rect=provider.GetSpriteRects().FirstOrDefault(x=>x.name==name)??new SpriteRect{name=name,spriteID=GUID.Generate(),pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center};rect.rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
        provider.SetSpriteRects(new[]{rect});provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,rect.spriteID)});provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(x=>x.name==name);
    }
    private static ObjectHeadTerrainPolygon Cut(params Vector2[] points)=>new ObjectHeadTerrainPolygon{subtract=true,points=points.Select(p=>(p+new Vector2(22,6))*32).ToArray()};
    private static void ReviseHarbor()
    {
        var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/twin_citadels.asset");
        // Preserve the existing right cave edit and add the user's sloping left through-route.
        recipe.artworkCutouts=recipe.artworkCutouts.Take(1).Concat(new[]{Cut(
            new Vector2(-18,.9f),new Vector2(-14,.5f),new Vector2(-10,-.4f),new Vector2(-6,-1.4f),new Vector2(-1.5f,-2.6f),
            new Vector2(-1.5f,-.1f),new Vector2(-6,1.1f),new Vector2(-10,2.1f),new Vector2(-14,3),new Vector2(-18,3.4f))}).ToArray();
        var baked=ObjectHeadMapRecipeEditor.Bake(recipe);AssignTerrain("TwinCitadels",baked);EditorUtility.SetDirty(recipe);
    }
    private static void CreateCavern(ObjectHeadContent content)
    {
        const string id="low_cavern",sceneName="LowCavern";
        string source="Assets/Art/Maps/LowCavern0917.png";AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(source);importer.isReadable=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
        string path="Assets/GameData/Maps/"+id+".asset";var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>(path);
        if(recipe==null){recipe=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/twin_citadels.asset"));AssetDatabase.CreateAsset(recipe,path);}
        recipe.mapId=id;recipe.sceneName=sceneName;recipe.nameKey="map_low_cavern";recipe.descriptionKey="map_low_cavern_description";
        recipe.polygons=Array.Empty<ObjectHeadTerrainPolygon>();recipe.structures=new[]{new ObjectHeadTerrainStamp{artwork=AssetDatabase.LoadAssetAtPath<Texture2D>(source),bottomCenter=new Vector2(704,0),heightPixels=768,maximumWidthPixels=1344}};
        recipe.artworkCutouts=new[]{Cut(new Vector2(0,-.12f),new Vector2(1,0),new Vector2(2,.2f),new Vector2(3,.4f),new Vector2(4,.6f),new Vector2(5,.8f),new Vector2(6,.75f),
            new Vector2(6,3.4f),new Vector2(5,3.2f),new Vector2(4,3.15f),new Vector2(3,3.3f),new Vector2(2,3.25f),new Vector2(1,3.4f),new Vector2(0,3.5f))};
        var baked=ObjectHeadMapRecipeEditor.Bake(recipe);
        string scenePath="Assets/Scenes/"+sceneName+".unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null)AssetDatabase.CopyAsset("Assets/Scenes/TwinCitadels.unity",scenePath);
        AssignTerrain(sceneName,baked);
        var scene=EditorSceneManager.GetActiveScene();var layout=UnityEngine.Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
        layout.useMarkerLocalSurface=true;layout.heightTolerance=.25f;layout.maximumSpawnHeightSpread=4;layout.maximumPlayerMeanHeightDifference=2.8f;
        var four=new[]{new[]{-12f,-10f},new[]{-3f,0f},new[]{5.5f,9f},new[]{14f,16f}};
        var duel=new[]{new[]{-12f,-10f,-3f},new[]{5.5f,14f,16f}};
        foreach(var mode in layout.layouts){var xs=mode.seats.Length==2?duel:four;for(int seat=0;seat<mode.seats.Length;seat++)for(int slot=0;slot<mode.seats[seat].characterSlots.Length;slot++)mode.seats[seat].characterSlots[slot].position=CaveMarker(baked,xs[seat][slot]);}
        EditorUtility.SetDirty(layout);EditorSceneManager.SaveScene(scene);
        if(!content.maps.Any(m=>m.id==id))content.maps=content.maps.Concat(new[]{new ObjectHeadMapDefinition{id=id,sceneName=sceneName,nameKey=recipe.nameKey,descriptionKey=recipe.descriptionKey}}).ToArray();
        content.maps.First(m=>m.id==id).preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/"+id+".png");EditorUtility.SetDirty(recipe);
    }
    private static Vector2 CaveMarker(Texture2D texture,float targetX)
    {
        bool Solid(float x,float y){int px=Mathf.RoundToInt((x+22)*32),py=Mathf.RoundToInt((y+6)*32);return px>=0&&py>=0&&px<texture.width&&py<texture.height&&texture.GetPixel(px,py).a>.5f;}
        Vector2 best=default;float cost=float.PositiveInfinity;
        for(float x=targetX-.9f;x<=targetX+.9f;x+=.0625f)for(float y=-3.8f;y<2;y+=.03125f)
        {
            if(!Solid(x,y)||Solid(x,y+.04f))continue;bool safe=true;
            for(float dx=-.5f;dx<=.5f;dx+=.125f){bool support=false;for(float dy=-.12f;dy<=.12f;dy+=.03125f)if(Solid(x+dx,y+dy)&&!Solid(x+dx,y+dy+.04f))support=true;if(!support)safe=false;}
            for(float dx=-.4f;dx<=.4f;dx+=.1f)for(float dy=.15f;dy<=1.5f;dy+=.1f)if(Solid(x+dx,y+dy))safe=false;
            float candidate=Mathf.Abs(x-targetX);if(safe&&candidate<cost){cost=candidate;best=new Vector2(x,y+.66f);}
        }
        if(float.IsInfinity(cost))throw new Exception("No cavern floor for "+targetX);Debug.Log("[CAVE_MARKER] "+best);return best;
    }
    private static void AssignTerrain(string sceneName,Texture2D baked)
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+sceneName+".unity");var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();var so=new SerializedObject(terrain);
        so.FindProperty("visualSourceTexture").objectReferenceValue=baked;so.FindProperty("collisionMaskTexture").objectReferenceValue=baked;so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
    }
    private static void ExpandHud(ObjectHeadBattleScreen screen)
    {
        var buttons=screen.commonButtons.ToList();var icons=screen.commonIcons.ToList();var texts=screen.commonTexts.ToList();
        while(buttons.Count<6)
        {
            var clone=UnityEngine.Object.Instantiate(buttons[0].gameObject,buttons[0].transform.parent);clone.name="commonButton"+buttons.Count;
            buttons.Add(clone.GetComponent<Button>());icons.Add(clone.GetComponentsInChildren<Image>(true).First(x=>x.gameObject!=clone));
            texts.Add(clone.GetComponentsInChildren<Text>(true).First(x=>x.name.StartsWith("commonText")));
        }
        screen.commonButtons=buttons.ToArray();screen.commonIcons=icons.ToArray();screen.commonTexts=texts.ToArray();
        var bar=buttons[0].transform.parent.GetComponent<RectTransform>();bar.sizeDelta=new Vector2(930,100);
        for(int i=0;i<3;i++)screen.skillButtons[i].GetComponent<RectTransform>().anchoredPosition=new Vector2(-408+i*94,2);
        for(int i=0;i<6;i++)buttons[i].GetComponent<RectTransform>().anchoredPosition=new Vector2(-96+i*94,2);
        EditorUtility.SetDirty(screen);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit migration only: normal builds never regenerate layout, terrain or spawn markers.</summary>
public static class ObjectHeadIslandRevision
{
    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Maps/Install Island Revision")]
    public static void Apply()
    {
        JoystickAxis();TitleButton();SkyDrops();
        Texture2D soil=Import("Assets/Art/Presentation/IslandSoilV2.png");
        Texture2D grass=Import("Assets/Art/Presentation/IslandGrassV2.png");
        int variant=0;
        foreach(var map in ObjectHeadContent.Load().maps)
        {
            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/"+map.id+".asset");
            recipe.width=1920;recipe.height=640;recipe.pixelsPerUnit=32;recipe.origin=new Vector2(-30,-6);
            recipe.soilTexture=soil;recipe.grassTexture=grass;recipe.textureTilePixels=384;recipe.grassTilePixels=120;recipe.grassTextureStrength=.55f;
            recipe.textureTint=Color.white;recipe.grass=new Color32(112,151,58,255);recipe.grassDepth=14;recipe.edgeColor=new Color32(58,42,25,255);
            recipe.polygons=Geometry(variant++);ObjectHeadMapRecipeEditor.Bake(recipe);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();
            var serialized=new SerializedObject(terrain);
            serialized.FindProperty("visualSourceTexture").objectReferenceValue=recipe.bakedTerrain;
            serialized.FindProperty("collisionMaskTexture").objectReferenceValue=recipe.bakedTerrain;
            serialized.FindProperty("terrainOriginWorld").vector2Value=recipe.origin;
            serialized.ApplyModifiedPropertiesWithoutUndo();terrain.transform.position=recipe.origin;
            var layout=UnityEngine.Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
            foreach(var mode in layout.layouts)foreach(var seat in mode.seats)foreach(var marker in seat.characterSlots)
                marker.position=new Vector3(marker.position.x,recipe.origin.y+320f/32f+.62f,marker.position.z);
            EditorSceneManager.SaveScene(scene);EditorUtility.SetDirty(recipe);
        }
        AssetDatabase.SaveAssets();Debug.Log("[ISLAND_AUTHORING] 3 mirrored cave/island maps, equal-height spawn pads, UI and sky drops updated.");
    }
    private static ObjectHeadTerrainPolygon[] Geometry(int variant)
    {
        // Four equally elevated spawn plateaus; high ground is available AFTER moving out of spawn.
        Vector2[] half={new Vector2(0,95),new Vector2(64,145),new Vector2(112,235),new Vector2(144,320),new Vector2(336,320),
            new Vector2(384,362),new Vector2(448,variant==1?475:425),new Vector2(520,variant==2?335:420),new Vector2(588,310),
            new Vector2(630,292),new Vector2(656,320),new Vector2(784,320),new Vector2(834,370),new Vector2(894,variant==1?440:390),
            new Vector2(934,310),new Vector2(960,variant==1?300:260)};
        var keys=half.Concat(half.Take(half.Length-1).Reverse().Select(p=>new Vector2(1920-p.x,p.y))).ToArray();
        var outline=new List<Vector2>{Vector2.zero};
        for(int i=0;i<keys.Length-1;i++)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt((keys[i+1].x-keys[i].x)/10));
            for(int k=0;k<steps;k++)
            {float t=k/(float)steps;outline.Add(new Vector2(Mathf.Lerp(keys[i].x,keys[i+1].x,t),Mathf.Lerp(keys[i].y,keys[i+1].y,Mathf.SmoothStep(0,1,t))));}
        }
        outline.Add(keys[keys.Length-1]);outline.Add(new Vector2(1920,0));
        var shapes=new List<ObjectHeadTerrainPolygon>{new ObjectHeadTerrainPolygon{points=outline.ToArray()}};
        if(variant==0)
        {Hole(shapes,462,208,150,82);Hole(shapes,1458,208,150,82);Hole(shapes,960,126,180,63);}
        else if(variant==1)
        {Hole(shapes,465,202,170,110);Hole(shapes,1455,202,170,110);Hole(shapes,960,150,215,72);}
        else
        {
            foreach(float x in new[]{472f,960f,1448f})
            {
                var crack=new List<Vector2>();
                for(int y=0;y<=640;y+=16)crack.Add(new Vector2(x-CrackWidth(y),y));
                for(int y=640;y>=0;y-=16)crack.Add(new Vector2(x+CrackWidth(y),y));
                shapes.Add(new ObjectHeadTerrainPolygon{subtract=true,points=crack.ToArray()});
            }
            foreach(float x in new[]{245f,725f,1195f,1675f})Hole(shapes,x,155,112,66);
        }
        return shapes.ToArray();
    }
    private static void Hole(List<ObjectHeadTerrainPolygon> shapes,float x,float y,float rx,float ry)
    {
        shapes.Add(new ObjectHeadTerrainPolygon{subtract=true,points=Enumerable.Range(0,32).Select(i=>{
            float angle=i*Mathf.PI*2/32;float ripple=1+.08f*Mathf.Cos(angle*4)+.05f*Mathf.Sin(angle*3);
            return new Vector2(x+Mathf.Cos(angle)*rx*ripple,y+Mathf.Sin(angle)*ry*ripple);}).ToArray()});
    }
    private static float CrackWidth(float y)=>Mathf.Max(16,30+18*Mathf.Sin(y*.0147f)+8*Mathf.Cos(y*.055f));
    private static Texture2D Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);if(i==null)throw new Exception("Missing texture "+path);
        i.textureType=TextureImporterType.Default;i.isReadable=true;i.wrapMode=TextureWrapMode.Mirror;i.filterMode=FilterMode.Bilinear;
        i.textureCompression=TextureImporterCompression.Uncompressed;i.mipmapEnabled=false;i.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static void TitleButton()
    {
        const string path="Assets/Prefabs/UI/ObjectHeadTitle.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var title=root.GetComponentInChildren<ObjectHeadTitleScreen>(true);var front=root.GetComponentInChildren<ObjectHeadFrontEnd>(true);
            var reference=(RectTransform)front.trainingButton.transform;var target=(RectTransform)title.localPlayButton.transform;
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,reference.rect.width);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,reference.rect.height);target.localScale=reference.localScale;
            var from=front.trainingButton.GetComponentInChildren<Text>(true);var to=title.localPlayButton.GetComponentInChildren<Text>(true);
            to.fontSize=from.fontSize;to.fontStyle=from.fontStyle;to.resizeTextForBestFit=from.resizeTextForBestFit;to.resizeTextMinSize=from.resizeTextMinSize;to.resizeTextMaxSize=from.resizeTextMaxSize;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void JoystickAxis()
    {
        var input=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);var axes=input.FindProperty("m_Axes");
        int source=-1,target=-1;
        for(int i=0;i<axes.arraySize;i++)
        {var axis=axes.GetArrayElementAtIndex(i);if(axis.FindPropertyRelative("m_Name").stringValue=="Horizontal" && axis.FindPropertyRelative("type").intValue==2)source=i;if(axis.FindPropertyRelative("m_Name").stringValue=="ObjectHeadLeftStickX")target=i;}
        if(target<0){if(source<0)throw new Exception("Missing joystick source axis");axes.InsertArrayElementAtIndex(source);target=source;}
        var entry=axes.GetArrayElementAtIndex(target);entry.FindPropertyRelative("m_Name").stringValue="ObjectHeadLeftStickX";entry.FindPropertyRelative("type").intValue=2;entry.FindPropertyRelative("axis").intValue=0;
        foreach(string key in new[]{"negativeButton","positiveButton","altNegativeButton","altPositiveButton"})entry.FindPropertyRelative(key).stringValue="";
        input.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SkyDrops()
    {
        var art=ObjectHeadPresentation.Load();art.dropsFromSkyEdge=true;art.skyDropMargin=1.5f;art.airstrikeBombSize=1.5f;art.healingSupplySize=1.6f;art.airstrikeFallSeconds=.8f;EditorUtility.SetDirty(art);
        const string path="Assets/Prefabs/Effects/IronMaiden.prefab";var cage=PrefabUtility.LoadPrefabContents(path);
        try{var visual=cage.GetComponent<ObjectHeadCageVisual>();visual.fallSeconds=.8f;cage.transform.localScale=Vector3.one*(2.6f/visual.closed.bounds.size.y);PrefabUtility.SaveAsPrefabAsset(cage,path);}
        finally{PrefabUtility.UnloadPrefabContents(cage);}
    }
}

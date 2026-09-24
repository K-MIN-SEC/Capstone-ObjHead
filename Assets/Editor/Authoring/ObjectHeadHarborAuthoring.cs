using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit one-map authoring command. Never runs when entering play mode or during normal builds.</summary>
public static class ObjectHeadHarborAuthoring
{
    [MenuItem("Object Head/Maps/Bake Harbor Layered Tunnels")]
    public static void BakeLayeredTunnels()
    {
        var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/twin_citadels.asset");
        var baked=ObjectHeadMapRecipeEditor.Bake(recipe);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TwinCitadels.unity");
        var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();
        var serialized=new SerializedObject(terrain);
        serialized.FindProperty("visualSourceTexture").objectReferenceValue=baked;
        serialized.FindProperty("collisionMaskTexture").objectReferenceValue=baked;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        terrain.GetComponent<SpriteRenderer>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/twin_citadels.png");
        WireTunnelLayer(terrain,recipe);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void WireTunnelLayer(TerrainManager terrain,ObjectHeadMapRecipe recipe)
    {
        var child=terrain.transform.Find("Tunnel Foreground");
        if(child==null)
        {
            var go=new GameObject("Tunnel Foreground",typeof(SpriteRenderer),typeof(ObjectHeadTunnelForeground));
            go.transform.SetParent(terrain.transform,false);
            child=go.transform;
        }
        child.localPosition=Vector3.zero;
        child.GetComponent<ObjectHeadTunnelForeground>().Configure(terrain,recipe);
        EditorUtility.SetDirty(child.gameObject);
    }

    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Maps/Install Hand-drawn Harbor Revision")]
    public static void Apply()
    {
        const string source="Assets/Art/Maps/HarborTerrain0917.png";
        AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(source);
        importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/twin_citadels.asset");
        recipe.width=1408;recipe.height=768;recipe.pixelsPerUnit=32;recipe.origin=new Vector2(-22,-6);
        recipe.polygons=Array.Empty<ObjectHeadTerrainPolygon>();
        recipe.structures=new[]{new ObjectHeadTerrainStamp{artwork=AssetDatabase.LoadAssetAtPath<Texture2D>(source),bottomCenter=new Vector2(704,0),heightPixels=768,maximumWidthPixels=1344}};
        // Open the short lower cave mouth, preserving its floor and thick roof. Editable in the recipe.
        recipe.artworkCutouts=new[]{new ObjectHeadTerrainPolygon{subtract=true,points=new[]{
            new Vector2(5,-.2f),new Vector2(7,.2f),new Vector2(8.5f,.45f),new Vector2(10,.6f),new Vector2(11.5f,.65f),new Vector2(13,.7f),
            new Vector2(13,3.2f),new Vector2(11.5f,3.1f),new Vector2(10,2.95f),new Vector2(8.5f,2.85f),new Vector2(7,3.0f),new Vector2(5,3.4f)
        }.Select(p=>(p+new Vector2(22,6))*32).ToArray()}};
        var baked=ObjectHeadMapRecipeEditor.Bake(recipe);
        var catalog=ObjectHeadContent.Load();catalog.maps.First(m=>m.id=="twin_citadels").preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/twin_citadels.png");
        EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(recipe);AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TwinCitadels.unity");
        var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();var settings=new SerializedObject(terrain);
        settings.FindProperty("visualSourceTexture").objectReferenceValue=baked;settings.FindProperty("collisionMaskTexture").objectReferenceValue=baked;
        settings.FindProperty("terrainOriginWorld").vector2Value=new Vector2(-22,-6);settings.ApplyModifiedPropertiesWithoutUndo();
        WireTunnelLayer(terrain,recipe);
        var layout=UnityEngine.Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
        layout.useMarkerLocalSurface=true;layout.heightTolerance=.2f;layout.maximumSpawnHeightSpread=7;layout.maximumPlayerMeanHeightDifference=1.3f;
        var four=new[]{
            new[]{new Vector2(-18,7.8f),new Vector2(-7.5f,6.87f)},
            new[]{new Vector2(-13,10.1f),new Vector2(-4.5f,4.7f)},
            new[]{new Vector2(6,5.77f),new Vector2(17,10.92f)},
            new[]{new Vector2(10,7.07f),new Vector2(13.25f,9.17f)}
        };
        var duel=new[]{new[]{four[0][0],four[1][0],four[1][1]},new[]{four[2][0],four[3][1],four[2][1]}};
        foreach(var mode in layout.layouts)
        {
            var stations=mode.seats.Length==2?duel:four;
            for(int seat=0;seat<mode.seats.Length;seat++)for(int slot=0;slot<mode.seats[seat].characterSlots.Length;slot++)
            {
                Vector2 position=FindSafeMarker(baked,stations[seat][slot]);
                mode.seats[seat].characterSlots[slot].position=position;
                Debug.Log($"[HARBOR_MARKER] {mode.mode} seat {seat+1} slot {slot+1}: {position}");
            }
        }
        EditorUtility.SetDirty(layout);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[HARBOR_AUTHORING] Only TwinCitadels revised. Artwork, openings and spawn markers are serialized editable assets.");
    }
    private static Vector2 FindSafeMarker(Texture2D texture,Vector2 guess)
    {
        bool Solid(float x,float y)
        {int px=Mathf.RoundToInt((x+22)*32),py=Mathf.RoundToInt((y+6)*32);return px>=0&&py>=0&&px<texture.width&&py<texture.height&&texture.GetPixel(px,py).a>.5f;}
        Vector2 best=default;float bestCost=float.PositiveInfinity;
        for(float x=guess.x-.4f;x<=guess.x+.4f;x+=.03125f)
        for(float y=guess.y-.55f;y<=guess.y+.55f;y+=.03125f)
        {
            if(!Solid(x,y)||Solid(x,y+.04f))continue;
            bool safe=true;
            for(float dx=-.3f;dx<=.3f;dx+=.1f)for(float dy=.12f;dy<=1.3f;dy+=.1f)if(Solid(x+dx,y+dy))safe=false;
            for(float dx=-.42f;dx<=.42f;dx+=.14f)
            {
                bool support=false;for(float dy=-.14f;dy<=.14f;dy+=.03125f)if(Solid(x+dx,y+dy)&&!Solid(x+dx,y+dy+.04f))support=true;
                if(!support)safe=false;
            }
            if(!safe)continue;float cost=Mathf.Abs(x-guess.x)+Mathf.Abs(y-guess.y);
            if(cost<bestCost){bestCost=cost;best=new Vector2(x,y+.65f);}
        }
        if(float.IsInfinity(bestCost))throw new InvalidOperationException("No safe authored marker near "+guess);
        return best;
    }
}

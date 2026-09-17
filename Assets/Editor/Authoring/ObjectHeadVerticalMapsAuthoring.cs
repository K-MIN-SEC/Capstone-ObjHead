using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit authoring migration. Normal builds never reposition designer objects.
public static class ObjectHeadVerticalMapsAuthoring
{
    public static void ApplyAndBuild(){ObjectHeadEscapeAuthoring.Apply();Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Maps/Install Vertical Island Collection")]
    public static void Apply()
    {
        var catalog=ObjectHeadContent.Load();var maps=catalog.maps.ToList();
        AddMap(maps,"tidal_caverns","TidalCaverns");AddMap(maps,"sky_terraces","SkyTerraces");catalog.maps=maps.ToArray();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Texture2D tower=Import("IslandLighthouse"),house=Import("IslandBoathouse"),wreck=Import("IslandShipwreck");
        for(int variant=0;variant<maps.Count;variant++)
        {
            var map=maps[variant];string path="Assets/GameData/Maps/"+map.id+".asset";
            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>(path);
            if(recipe==null){recipe=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/wind_meadow.asset"));AssetDatabase.CreateAsset(recipe,path);}
            recipe.mapId=map.id;recipe.sceneName=map.sceneName;recipe.nameKey=map.nameKey;recipe.descriptionKey=map.descriptionKey;
            recipe.width=1408;recipe.height=1024;recipe.pixelsPerUnit=32;recipe.origin=new Vector2(-22,-6);
            recipe.textureTilePixels=320;recipe.grassDepth=11;recipe.minimumIslandPixels=512;recipe.polygons=Geometry(variant);
            var stamps=new List<ObjectHeadTerrainStamp>();
            if(variant==0)stamps.Add(Stamp(tower,22,14.8f,13));
            if(variant==1){stamps.Add(Stamp(house,12,13.8f,4.5f));stamps.Add(Stamp(house,32,13.8f,4.5f,true));}
            if(variant==2){stamps.Add(Stamp(wreck,11.8f,14.5f,4.3f));stamps.Add(Stamp(wreck,32.2f,14.5f,4.3f,true));}
            if(variant==3)stamps.Add(Stamp(house,22,21.8f,4.5f));
            if(variant==4){stamps.Add(Stamp(tower,12,18.8f,8));stamps.Add(Stamp(tower,32,18.8f,8,true));}
            recipe.structures=stamps.ToArray();ObjectHeadMapRecipeEditor.Bake(recipe);
            map.preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/"+map.id+".png");
            EditorUtility.SetDirty(recipe);EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Texture2D baked=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Maps/"+map.id+".png");
            Vector2 origin=recipe.origin;
            string scenePath="Assets/Scenes/"+map.sceneName+".unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null)AssetDatabase.CopyAsset("Assets/Scenes/WindMeadow.unity",scenePath);
            var scene=EditorSceneManager.OpenScene(scenePath);
            var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();var so=new SerializedObject(terrain);
            so.FindProperty("visualSourceTexture").objectReferenceValue=baked;so.FindProperty("collisionMaskTexture").objectReferenceValue=baked;
            so.FindProperty("terrainOriginWorld").vector2Value=origin;so.ApplyModifiedPropertiesWithoutUndo();terrain.transform.position=origin;
            var layout=UnityEngine.Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
            foreach(var mode in layout.layouts)for(int seat=0;seat<mode.seats.Length;seat++)
            {
                var slots=mode.seats[seat].characterSlots;
                float center=mode.seats.Length==2?(seat==0?-15:15):-15+seat*10;
                for(int slot=0;slot<slots.Length;slot++)slots[slot].position=new Vector3(center+(slot-(slots.Length-1)*.5f)*1.15f,4.62f,0);
            }
            // Camera limits come from terrain dimensions, including the existing sky reserve.
            ObjectHeadSceneryAuthoring.AddToScene(variant);
            EditorSceneManager.SaveScene(scene);
        }
        var config=AssetDatabase.LoadAssetAtPath<ObjectHeadNetworkConfig>("Assets/Resources/ObjectHeadNetworkConfig.asset");var settings=new SerializedObject(config);
        var pool=settings.FindProperty("defaultRoomSettings").FindPropertyRelative("randomMapPool");pool.arraySize=maps.Count;
        var bindings=settings.FindProperty("mapScenes");bindings.arraySize=maps.Count;
        for(int i=0;i<maps.Count;i++){pool.GetArrayElementAtIndex(i).stringValue=maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("mapId").stringValue=maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName").stringValue=maps[i].sceneName;}
        settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/ObjectHeadTitle.unity",true)}.Concat(maps.Select(m=>new EditorBuildSettingsScene("Assets/Scenes/"+m.sceneName+".unity",true))).ToArray();
        AssetDatabase.SaveAssets();Debug.Log("[VERTICAL_MAPS] Authored "+maps.Count+" maps, 44x32 terrain, destructible structures and lower routes.");
    }
    private static void AddMap(List<ObjectHeadMapDefinition> maps,string id,string scene)
    {if(!maps.Any(m=>m.id==id))maps.Add(new ObjectHeadMapDefinition{id=id,sceneName=scene,nameKey="map_"+id,descriptionKey="map_"+id+"_description"});}
    private static Texture2D Import(string name)
    {
        string path="Assets/Art/Presentation/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static ObjectHeadTerrainStamp Stamp(Texture2D image,float x,float y,float h,bool flip=false)=>new ObjectHeadTerrainStamp{artwork=image,bottomCenter=new Vector2(x,y-.15f)*32,heightPixels=h*32,maximumWidthPixels=5.8f*32,flipX=flip};
    private static ObjectHeadTerrainPolygon[] Geometry(int variant)
    {
        // Units relative to terrain bottom-left. Spawn shelves at x=7/17/27/37, y=10.
        Vector2[] half;
        switch(variant)
        {
            case 0:half=Points(0,2,2,4,3.5f,8,4.5f,10,9,10,9.5f,11.4f,10.5f,11.4f,11,12.8f,12.2f,12.8f,12.8f,14.2f,13.5f,14.2f,14.5f,10,19,10,19.3f,11.5f,20,11.5f,20.3f,13,21,13,21.3f,15,22,15);break;
            case 1:half=Points(0,4,1,7,3,7,4,10,9,10,9.3f,11.4f,10,11.4f,10.4f,14,13.4f,14,14,12,14.8f,10,19,10,19.3f,12,20,12,20.3f,15,20.8f,15,21.2f,20,22,20);break;
            case 2:half=Points(0,3,1,5,2.8f,5,4.4f,10,9,10,9.4f,11.6f,10.2f,11.6f,10.5f,14.8f,13.4f,14.8f,14.3f,12,14.8f,10,19,10,20,7,21,7,21.5f,3,22,3);break;
            case 3:half=Points(0,6,2,6,3.8f,10,9,10,9.5f,12,10.4f,12,10.8f,15,11.7f,15,12.1f,18,12.8f,18,13.5f,15,14.8f,10,19,10,19.2f,13,20,13,20.2f,17,21,17,21.3f,22,22,22);break;
            default:half=Points(0,2,2,6,3.5f,6,4,10,9,10,9.2f,12,10,12,10.2f,15,11,15,11.2f,19,12.8f,19,13.2f,15,14,15,14.8f,10,19,10,19.6f,13,20.4f,13,20.8f,16,22,16);break;
        }
        var outline=new List<Vector2>{Vector2.zero};outline.AddRange(half);outline.AddRange(half.Take(half.Length-1).Reverse().Select(p=>new Vector2(44-p.x,p.y)));outline.Add(new Vector2(44,0));
        var shapes=new List<ObjectHeadTerrainPolygon>{Poly(false,outline.ToArray())};
        // Walkable sheltered detours: passages open through the exterior, not sealed decorative holes.
        if(variant==0){Tunnel(shapes,Points(0,4,4,5.5f,9,5.5f,12,7,15,6,19,6,22,8),1.05f,true);}
        if(variant==1){Tunnel(shapes,Points(0,3.2f,5,5,10,6,14,6,17,6,22,8),1.1f,true);Hole(shapes,22,14,.7f,1.5f);}
        if(variant==2){Tunnel(shapes,Points(0,3,6,6,11,8,16,5,20,5),1.15f,true);shapes.Add(Poly(true,Points(21,0,21.2f,3.5f,22.8f,3.5f,23,0)));}
        if(variant==3){Tunnel(shapes,Points(0,5.5f,5,6,10,6,14,6.5f,18,6,22,7),1.2f,true);Tunnel(shapes,Points(10,10.2f,11,11.5f,13,11.5f,14,10.2f),1.05f,true);Hole(shapes,22,13,1.5f,2.5f);}
        if(variant==4){Tunnel(shapes,Points(0,4,5,6,10,6,14,7,18,6,22,8),1.1f,true);Tunnel(shapes,Points(10,11,11,12,13,12,14,11),1.05f,true);Hole(shapes,22,12,1.4f,1.5f);}
        return shapes.ToArray();
    }
    private static Vector2[] Points(params float[] xy)=>Enumerable.Range(0,xy.Length/2).Select(i=>new Vector2(xy[i*2],xy[i*2+1])).ToArray();
    private static ObjectHeadTerrainPolygon Poly(bool subtract,Vector2[] points)=>new ObjectHeadTerrainPolygon{subtract=subtract,points=points.Select(p=>p*32).ToArray()};
    private static void Tunnel(List<ObjectHeadTerrainPolygon> shapes,Vector2[] path,float radius,bool mirror)
    {
        for(int i=0;i<path.Length-1;i++)
        {
            var a=path[i];var b=path[i+1];var n=new Vector2(-(b-a).y,(b-a).x).normalized*radius;
            shapes.Add(Poly(true,new[]{a+n,b+n,b-n,a-n}));
        }
        foreach(var p in path)Hole(shapes,p.x,p.y,radius,radius);
        if(mirror)Tunnel(shapes,path.Select(p=>new Vector2(44-p.x,p.y)).ToArray(),radius,false);
    }
    private static void Hole(List<ObjectHeadTerrainPolygon> shapes,float x,float y,float rx,float ry)
    {shapes.Add(Poly(true,Enumerable.Range(0,20).Select(i=>new Vector2(x+Mathf.Cos(i*Mathf.PI/10)*rx,y+Mathf.Sin(i*Mathf.PI/10)*ry)).ToArray()));}
}

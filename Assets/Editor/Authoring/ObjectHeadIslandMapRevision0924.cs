using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit bake of the three island maps whose 2P and 4P spawns pass validation.</summary>
public static class ObjectHeadIslandMapRevision0924
{
    private struct Source
    {
        public string id, art;
        public Source(string mapId,string artName){id=mapId;art=artName;}
    }
    private struct Surface
    {
        public Vector2 position;
        public float Cost(Vector2 desired)=>Mathf.Abs(position.x-desired.x)*.23f+Mathf.Abs(position.y-desired.y)*2f;
    }
    private static readonly Source[] Sources =
    {
        new Source("low_cavern","RoofedIsland0924"),
        new Source("tidal_caverns","TidalLagoon0924"),
        new Source("sky_terraces","VolcanicScar0924"),
        // LighthouseCliffs and ShipGraveyard remain draft artwork until their
        // safe horizontal spawn terraces are redesigned for eight characters.
    };

    [MenuItem("Object Head/Maps/Install Revised Island Collection 0924")]
    public static void Apply()
    {
        var catalog=ObjectHeadContent.Load();
        foreach(var source in Sources)
        {
            string imagePath="Assets/Art/Maps/"+source.art+".png";
            AssetDatabase.ImportAsset(imagePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
            if(importer==null)throw new InvalidOperationException("Missing map art: "+imagePath);
            importer.isReadable=true;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/"+source.id+".asset");
            if(recipe==null)throw new InvalidOperationException("Missing map recipe: "+source.id);
            recipe.width=1408;recipe.height=768;recipe.pixelsPerUnit=32;recipe.origin=new Vector2(-22,-6);
            recipe.polygons=Array.Empty<ObjectHeadTerrainPolygon>();
            recipe.artworkCutouts=Array.Empty<ObjectHeadTerrainPolygon>();
            recipe.structures=new[]{new ObjectHeadTerrainStamp{
                artwork=AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath),
                bottomCenter=new Vector2(704,0),heightPixels=728,maximumWidthPixels=1360}};
            Texture2D baked=ObjectHeadMapRecipeEditor.Bake(recipe);
            var map=catalog.maps.First(m=>m.id==source.id);
            map.preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/"+source.id+".png");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var terrain=UnityEngine.Object.FindAnyObjectByType<TerrainManager>();
            var serialized=new SerializedObject(terrain);
            serialized.FindProperty("visualSourceTexture").objectReferenceValue=baked;
            serialized.FindProperty("collisionMaskTexture").objectReferenceValue=baked;
            serialized.FindProperty("terrainOriginWorld").vector2Value=recipe.origin;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            terrain.transform.position=recipe.origin;
            terrain.GetComponent<SpriteRenderer>().sprite=map.preview;
            var layout=UnityEngine.Object.FindAnyObjectByType<ObjectHeadSpawnLayout>();
            AssignBalancedSpawns(layout,baked,recipe.origin);
            EditorUtility.SetDirty(recipe);EditorUtility.SetDirty(layout);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ISLAND_0924] "+source.id+" baked, fair spawn groups assigned.");
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static void AssignBalancedSpawns(ObjectHeadSpawnLayout layout,Texture2D baked,Vector2 origin)
    {
        var surfaces=FindSurfaces(baked,origin);
        if(surfaces.Count<8)throw new InvalidOperationException("Map has too few safe spawn surfaces: "+surfaces.Count);
        layout.useMarkerLocalSurface=true;
        layout.heightTolerance=.28f;
        foreach(var mode in layout.layouts)
        {
            Vector2[][] groups=ChooseSeatGroups(surfaces,mode.seats.Length,mode.seats[0].characterSlots.Length);
            var modePoints=groups.SelectMany(group=>group).ToArray();
            for(int seat=0;seat<mode.seats.Length;seat++)
            {
                var slots=mode.seats[seat].characterSlots;
                for(int slot=0;slot<slots.Length;slot++)
                {
                    slots[slot].position=groups[seat][slot];
                }
            }
            float spread=modePoints.Max(p=>p.y)-modePoints.Min(p=>p.y);
            Debug.Log($"[ISLAND_0924_SPAWNS] {mode.mode}: {string.Join(", ",modePoints.Select(p=>p.ToString("F2")))} spread={spread:F2}");
        }
        layout.maximumSpawnHeightSpread=4.5f;
        layout.maximumPlayerMeanHeightDifference=4f;
    }

    private static Vector2[][] ChooseSeatGroups(List<Surface> surfaces,int seatCount,int slotsPerSeat)
    {
        Vector2[][] best=null;float bestCost=float.PositiveInfinity;
        for(float band=.55f;band<=4.5f;band+=.35f)
        for(float targetY=-1;targetY<19;targetY+=.4f)
        {
            var level=surfaces.Where(s=>Mathf.Abs(s.position.y-targetY)<band).ToArray();
            if(level.Length<seatCount*slotsPerSeat)continue;
            float minX=level.Min(s=>s.position.x),maxX=level.Max(s=>s.position.x);
            float minSpan=seatCount==2?8f:7f;
            for(float span=minSpan;span<=Mathf.Min(maxX-minX,32f);span+=2f)
            for(float left=minX;left+span<=maxX+.01f;left+=1.5f)
            {
            float right=left+span;
            float[] centers=Enumerable.Range(0,seatCount)
                .Select(i=>Mathf.Lerp(left,right,i/(float)(seatCount-1))).ToArray();
            var choice=new Vector2[centers.Length][];float cost=0;bool valid=true;
            for(int i=0;i<centers.Length;i++)
            {
                float center=centers[i];
                var candidates=level.Where(s=>Mathf.Abs(s.position.x-center)<4.8f).ToArray();
                if(candidates.Length<slotsPerSeat){valid=false;break;}
                choice[i]=new Vector2[slotsPerSeat];
                for(int slot=0;slot<slotsPerSeat;slot++)
                {
                    float idealX=center+(slot-(slotsPerSeat-1)*.5f)*1.05f;
                    var match=candidates.Where(s=>choice[i].Take(slot).All(p=>
                        Vector2.Distance(p,s.position)>.72f))
                        .OrderBy(s=>Mathf.Abs(s.position.x-idealX)*.5f+
                            Mathf.Abs(s.position.y-targetY)*2.5f).FirstOrDefault();
                    if(match.position==Vector2.zero){valid=false;break;}
                    choice[i][slot]=match.position;
                    cost+=Mathf.Abs(match.position.x-idealX)*.5f+
                        Mathf.Abs(match.position.y-targetY)*2.5f;
                }
                if(!valid)break;
            }
            if(!valid)continue;
            var all=choice.SelectMany(group=>group).ToArray();
            if(all.Where((p,i)=>all.Skip(i+1).Any(other=>Vector2.Distance(p,other)<.8f)).Any())continue;
            float spread=all.Max(p=>p.y)-all.Min(p=>p.y);
            if(spread>4.5f)continue;
            cost+=spread*8f+band*3f-(right-left)*.1f;
            if(cost<bestCost){bestCost=cost;best=choice;}
            }
        }
        if(best==null)best=ChooseLinearClusters(surfaces,seatCount,slotsPerSeat);
        if(best==null)throw new InvalidOperationException("No balanced spawn groups for "+seatCount+
            " seats with "+slotsPerSeat+" characters; safe="+surfaces.Count+
            ", elevations="+string.Join(",",surfaces.GroupBy(s=>Mathf.RoundToInt(s.position.y))
                .OrderByDescending(g=>g.Count()).Take(8).Select(g=>g.Key+":"+g.Count())));
        return best;
    }

    private static Vector2[][] ChooseLinearClusters(List<Surface> surfaces,int seats,int slots)
    {
        Vector2[][] best=null;float bestCost=float.PositiveInfinity;
        int count=seats*slots;
        for(float band=.75f;band<=4.5f;band+=.5f)
        for(float targetY=-1f;targetY<19f;targetY+=.5f)
        {
            var level=surfaces.Where(s=>Mathf.Abs(s.position.y-targetY)<band)
                .Select(s=>s.position).OrderBy(p=>p.x).ToArray();
            if(level.Length<count)continue;
            for(int start=0;start<=level.Length-count;start++)
            {
                var points=new List<Vector2>{level[start]};
                for(int i=start+1;i<level.Length&&points.Count<count;i++)
                {
                    Vector2 previous=points[points.Count-1],next=level[i];
                    if(next.x-previous.x<.82f)continue;
                    if(points.Count%slots!=0&&next.x-previous.x>4f)break;
                    points.Add(next);
                }
                if(points.Count!=count)continue;
                float spread=points.Max(p=>p.y)-points.Min(p=>p.y);
                float span=points[count-1].x-points[0].x;
                if(spread>4.5f||span<(seats==2?8f:7f))continue;
                float cost=spread*8f-span*.15f+band*3f;
                if(cost>=bestCost)continue;
                bestCost=cost;
                best=Enumerable.Range(0,seats).Select(seat=>points.Skip(seat*slots).Take(slots).ToArray()).ToArray();
            }
        }
        return best;
    }

    private static List<Surface> FindSurfaces(Texture2D art,Vector2 origin)
    {
        var pixels=art.GetPixels32();int width=art.width,height=art.height;
        bool Solid(int x,int y)=>x>=0&&y>=0&&x<width&&y<height&&pixels[y*width+x].a>127;
        var found=new List<Surface>();
        for(int x=60;x<width-60;x+=4)
        for(int y=55;y<height-55;y+=2)
        {
            if(!Solid(x,y)||Solid(x,y+2))continue;
            Vector2 marker=origin+new Vector2(x/32f,(y+22)/32f);
            if(!TryExactLocalSpawn(Solid,origin,marker,out Vector2 legal))continue;
            found.Add(new Surface{position=legal});
        }
        return found;
    }

    private static bool TryExactLocalSpawn(Func<int,int,bool> solid,Vector2 origin,Vector2 marker,out Vector2 point)
    {
        point=default;
        const float tolerance=.28f,halfWidth=.36f,halfHeight=.58f;
        bool SolidWorld(Vector2 world)
        {
            int x=Mathf.FloorToInt((world.x-origin.x)*32);
            int y=Mathf.FloorToInt((world.y-origin.y)*32);
            return solid(x,y);
        }
        bool Hit(Vector2 start,Vector2 end,out float hitY)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,end)*64));
            for(int i=0;i<=steps;i++)
            {
                Vector2 sample=Vector2.Lerp(start,end,i/(float)steps);
                if(!SolidWorld(sample))continue;
                hitY=sample.y;return true;
            }
            hitY=0;return false;
        }
        float foot=marker.y-halfHeight-.06f;
        if(!Hit(new Vector2(marker.x,foot+tolerance),new Vector2(marker.x,foot-tolerance),out float centerY) ||
            centerY<=origin.y+1.5f)return false;
        float highest=centerY,lowest=centerY;
        for(int i=0;i<5;i++)
        {
            float x=marker.x+Mathf.Lerp(-halfWidth-.12f,halfWidth+.12f,i/4f);
            if(!Hit(new Vector2(x,centerY+tolerance),new Vector2(x,centerY-tolerance),out float supportY) ||
                Mathf.Abs(supportY-centerY)>tolerance)return false;
            highest=Mathf.Max(highest,supportY);lowest=Mathf.Min(lowest,supportY);
        }
        if(highest-lowest>tolerance)return false;
        point=new Vector2(marker.x,highest+halfHeight+.06f);
        if(Mathf.Abs(point.y-marker.y)>tolerance)return false;
        for(float x=-halfWidth;x<=halfWidth+.001f;x+=.08f)
        for(float y=-halfHeight+.02f;y<=halfHeight+.001f;y+=.08f)
            if(SolidWorld(point+new Vector2(x,y)))return false;
        return true;
    }
}

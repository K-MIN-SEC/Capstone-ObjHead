using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ObjectHeadMapRecipe))]
public sealed class ObjectHeadMapRecipeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Polygon points are in pixels. Bake after changing the shape or palette. Existing scene object positions are preserved.", MessageType.Info);
        if (GUILayout.Button("Bake Terrain Texture")) Bake((ObjectHeadMapRecipe)target);
    }

    public static Texture2D Bake(ObjectHeadMapRecipe recipe)
    {
        bool[] solid = new bool[recipe.width * recipe.height];
        foreach (var polygon in recipe.polygons)
        {
            int minX=recipe.width,minY=recipe.height,maxX=0,maxY=0;
            foreach(var p in polygon.points){minX=Mathf.Min(minX,Mathf.FloorToInt(p.x));minY=Mathf.Min(minY,Mathf.FloorToInt(p.y));maxX=Mathf.Max(maxX,Mathf.CeilToInt(p.x));maxY=Mathf.Max(maxY,Mathf.CeilToInt(p.y));}
            for (int y = Mathf.Max(0,minY); y < Mathf.Min(recipe.height,maxY); y++)
            for (int x = Mathf.Max(0,minX); x < Mathf.Min(recipe.width,maxX); x++)
                if (Inside(polygon.points, x + .5f, y + .5f)) solid[y * recipe.width + x] = !polygon.subtract;
        }
        RemoveBakeScraps(solid,recipe.width,recipe.minimumIslandPixels);
        Color32[] colors = new Color32[solid.Length];
        for (int y = 0; y < recipe.height; y++)
        for (int x = 0; x < recipe.width; x++)
        {
            int at = y * recipe.width + x;
            if (!solid[at]) continue;
            int depth = 0;
            while (depth < 32 && y + depth < recipe.height && solid[(y + depth) * recipe.width + x]) depth++;
            float noise = Mathf.PerlinNoise(x * .055f, y * .055f);
            Color c = Color.Lerp(recipe.rock, recipe.soil, noise);
            if (recipe.soilTexture != null)
                c = SampleTile(recipe.soilTexture, x, y, recipe.textureTilePixels) * recipe.textureTint;
            int grassDepth = recipe.grassDepth + Mathf.RoundToInt(3 * Mathf.PerlinNoise(x * .08f, 0));
            if (depth <= 2) c = recipe.grass * 1.18f;
            else if (depth < grassDepth) c = recipe.grass * (.88f + .12f * noise);
            else if (depth < grassDepth + 3) c = recipe.edgeColor;
            if(depth>2 && depth<grassDepth && recipe.grassTexture!=null)
                c=Color.Lerp(c,SampleTile(recipe.grassTexture,x,y,recipe.grassTilePixels),recipe.grassTextureStrength);
            bool edge = x < 2 || x >= recipe.width-2 || y < 2 ||
                !solid[y*recipe.width+Mathf.Max(0,x-2)] || !solid[y*recipe.width+Mathf.Min(recipe.width-1,x+2)] ||
                !solid[Mathf.Max(0,y-2)*recipe.width+x];
            if (edge && depth > grassDepth) c = recipe.edgeColor;
            c.a = 1;
            colors[at] = c;
        }
        if(recipe.structures!=null)foreach(var stamp in recipe.structures)Stamp(colors,recipe.width,recipe.height,stamp);
        if(recipe.artworkCutouts!=null && recipe.artworkCutouts.Length>0)
        {
            var removed=new bool[colors.Length];
            foreach(var cut in recipe.artworkCutouts)
            {
                if(cut.points==null||cut.points.Length<3)continue;
                for(int y=0;y<recipe.height;y++)for(int x=0;x<recipe.width;x++)
                {
                    int at=y*recipe.width+x;
                    if(colors[at].a>0 && Inside(cut.points,x+.5f,y+.5f)){colors[at]=default;removed[at]=true;}
                }
            }
            for(int y=1;y<recipe.height-1;y++)for(int x=1;x<recipe.width-1;x++)
            {
                int at=y*recipe.width+x;if(colors[at].a==0)continue;
                if(removed[at-1]||removed[at+1]||removed[at-recipe.width]||removed[at+recipe.width])colors[at]=recipe.edgeColor;
            }
        }
        var texture = new Texture2D(recipe.width, recipe.height, TextureFormat.RGBA32, false);
        texture.SetPixels32(colors); texture.Apply();
        string path = "Assets/Art/Maps/" + recipe.mapId + ".png";
        Directory.CreateDirectory("Assets/Art/Maps");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = recipe.pixelsPerUnit;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        importer.SetTextureSettings(settings);
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        recipe.bakedTerrain = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        EditorUtility.SetDirty(recipe);
        return recipe.bakedTerrain;
    }

    private static void RemoveBakeScraps(bool[] solid,int width,int minimum)
    {
        if(minimum<=1)return;
        var seen=new bool[solid.Length];var queue=new int[solid.Length];
        for(int start=0;start<solid.Length;start++)
        {
            if(!solid[start]||seen[start])continue;
            int count=1,read=0;queue[0]=start;seen[start]=true;
            while(read<count)
            {
                int at=queue[read++];int x=at%width;
                Visit(at-width);Visit(at+width);if(x>0)Visit(at-1);if(x<width-1)Visit(at+1);
                void Visit(int next){if(next<0||next>=solid.Length||seen[next]||!solid[next])return;seen[next]=true;queue[count++]=next;}
            }
            if(count<minimum)for(int i=0;i<count;i++)solid[queue[i]]=false;
        }
    }

    private static Color SampleTile(Texture2D texture,float x,float y,float tilePixels)
    {
        // CPU sampling must receive normalized UVs: unsupported wrap modes can clamp
        // outside 0..1 and stretch the last column across the rest of the map.
        float u=x/Mathf.Max(1,tilePixels),v=y/Mathf.Max(1,tilePixels);
        u=texture.wrapModeU==TextureWrapMode.Mirror?Mathf.PingPong(u,1):Mathf.Repeat(u,1);
        v=texture.wrapModeV==TextureWrapMode.Mirror?Mathf.PingPong(v,1):Mathf.Repeat(v,1);
        return texture.GetPixelBilinear(u,v);
    }

    private static void Stamp(Color32[] colors,int width,int height,ObjectHeadTerrainStamp stamp)
    {
        if(stamp.artwork==null)return;
        var source=stamp.artwork;var pixels=source.GetPixels32();
        int minX=source.width,minY=source.height,maxX=0,maxY=0;
        for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)if(pixels[y*source.width+x].a>127)
        {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
        if(minX>maxX)return;
        float scale=stamp.heightPixels/(maxY-minY+1);
        if(stamp.maximumWidthPixels>0)scale=Mathf.Min(scale,stamp.maximumWidthPixels/(maxX-minX+1));
        int w=Mathf.CeilToInt((maxX-minX+1)*scale),h=Mathf.CeilToInt((maxY-minY+1)*scale);
        int left=Mathf.RoundToInt(stamp.bottomCenter.x-w*.5f),bottom=Mathf.RoundToInt(stamp.bottomCenter.y);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int dx=left+x,dy=bottom+y;if(dx<0||dx>=width||dy<0||dy>=height)continue;
            float u=(x+.5f)/w;if(stamp.flipX)u=1-u;
            var c=source.GetPixelBilinear((minX+u*(maxX-minX))/source.width,(minY+(y+.5f)/h*(maxY-minY))/source.height);
            // The same alpha mask feeds rendering and destruction/collision; no invisible box colliders.
            if(c.a<.5f)continue;c.a=1;colors[dy*width+dx]=c;
        }
    }

    private static bool Inside(Vector2[] p, float x, float y)
    {
        bool result = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x-p[i].x) * (y-p[i].y) / (p[j].y-p[i].y) + p[i].x) result = !result;
        return result;
    }
}

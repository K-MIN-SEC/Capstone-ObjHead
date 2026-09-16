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
            for (int y = 0; y < recipe.height; y++)
            for (int x = 0; x < recipe.width; x++)
                if (Inside(polygon.points, x + .5f, y + .5f)) solid[y * recipe.width + x] = !polygon.subtract;
        }
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
                c = recipe.soilTexture.GetPixelBilinear(x / recipe.textureTilePixels, y / recipe.textureTilePixels) * recipe.textureTint;
            int grassDepth = recipe.grassDepth + Mathf.RoundToInt(3 * Mathf.PerlinNoise(x * .08f, 0));
            if (depth <= 2) c = recipe.grass * 1.18f;
            else if (depth < grassDepth) c = recipe.grass * (.88f + .12f * noise);
            else if (depth < grassDepth + 3) c = recipe.edgeColor;
            bool edge = x < 2 || x >= recipe.width-2 || y < 2 ||
                !solid[y*recipe.width+Mathf.Max(0,x-2)] || !solid[y*recipe.width+Mathf.Min(recipe.width-1,x+2)] ||
                !solid[Mathf.Max(0,y-2)*recipe.width+x];
            if (edge && depth > grassDepth) c = recipe.edgeColor;
            c.a = 1;
            colors[at] = c;
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

    private static bool Inside(Vector2[] p, float x, float y)
    {
        bool result = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x-p[i].x) * (y-p[i].y) / (p[j].y-p[i].y) + p[i].x) result = !result;
        return result;
    }
}

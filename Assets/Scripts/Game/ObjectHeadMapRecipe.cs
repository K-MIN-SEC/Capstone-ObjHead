using System;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadTerrainPolygon
{
    public bool subtract;
    public Vector2[] points;
}

/// <summary>Editable source geometry for baked terrain. Coordinates are pixels, bottom-left origin.</summary>
[CreateAssetMenu(menuName = "Object Head/Map Recipe")]
public sealed class ObjectHeadMapRecipe : ScriptableObject
{
    public string mapId;
    public string nameKey;
    public string descriptionKey;
    public string sceneName;
    public int width;
    public int height;
    public int pixelsPerUnit;
    public Vector2 origin;
    public float waterY;
    public float skyPadding;
    public Color soil;
    public Color rock;
    public Color grass;
    [Header("Surface artwork (baked only when requested in the editor)")]
    public Texture2D soilTexture;
    [Min(32)] public float textureTilePixels = 600;
    public Color textureTint = Color.white;
    [Range(2, 24)] public int grassDepth = 10;
    public Color edgeColor = new Color(.18f, .22f, .18f, 1);
    public ObjectHeadTerrainPolygon[] polygons;
    public Texture2D bakedTerrain;
}

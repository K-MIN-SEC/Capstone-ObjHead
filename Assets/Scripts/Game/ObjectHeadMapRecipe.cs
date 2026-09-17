using System;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadTerrainPolygon
{
    public bool subtract;
    public Vector2[] points;
}

[Serializable]
public sealed class ObjectHeadTerrainStamp
{
    public Texture2D artwork;
    public Vector2 bottomCenter;
    [Min(1)] public float heightPixels = 256;
    [Tooltip("Optional width limit, preserving the artwork aspect ratio. Zero means unrestricted.")]
    [Min(0)] public float maximumWidthPixels;
    public bool flipX;
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
    public Texture2D grassTexture;
    [Min(16)] public float grassTilePixels=100;
    [Range(0,1)] public float grassTextureStrength=.7f;
    [Min(32)] public float textureTilePixels = 600;
    public Color textureTint = Color.white;
    [Range(2, 24)] public int grassDepth = 10;
    public Color edgeColor = new Color(.18f, .22f, .18f, 1);
    public ObjectHeadTerrainPolygon[] polygons;
    [Tooltip("Editor bake only: remove tiny disconnected scraps left by polygon subtraction. Zero disables cleanup.")]
    [Min(0)] public int minimumIslandPixels = 512;
    [Header("Destructible buildings (pixel coordinates, bottom centre)")]
    public ObjectHeadTerrainStamp[] structures = Array.Empty<ObjectHeadTerrainStamp>();
    [Header("Editable openings cut through artwork after stamping (pixel coordinates)")]
    public ObjectHeadTerrainPolygon[] artworkCutouts = Array.Empty<ObjectHeadTerrainPolygon>();
    public Texture2D bakedTerrain;
}

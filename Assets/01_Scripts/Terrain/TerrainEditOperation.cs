using System;
using UnityEngine;

public enum TerrainEditOperationKind
{
    DestroyCircle = 1,
    CreateCircle = 2,
    CreateEllipse = 3,
    CreateBridge = 4
}

[Serializable]
public struct TerrainEditOperation
{
    public TerrainEditOperationKind kind;
    public int centerPixelX;
    public int centerPixelY;
    public int radiusXPx;
    public int radiusYPx;
    public TerrainType terrainType;
    public float directionX;
    public float directionY;
    public int lengthPx;
    public int thicknessPx;

    public static TerrainEditOperation Circle(
        TerrainEditOperationKind operationKind,
        Vector2Int centerPixel,
        int radiusPx,
        TerrainType type)
    {
        return Ellipse(operationKind, centerPixel, radiusPx, radiusPx, type);
    }

    public static TerrainEditOperation Ellipse(
        TerrainEditOperationKind operationKind,
        Vector2Int centerPixel,
        int radiusXPixels,
        int radiusYPixels,
        TerrainType type)
    {
        return new TerrainEditOperation
        {
            kind = operationKind,
            centerPixelX = centerPixel.x,
            centerPixelY = centerPixel.y,
            radiusXPx = Mathf.Max(1, radiusXPixels),
            radiusYPx = Mathf.Max(1, radiusYPixels),
            terrainType = type
        };
    }

    public static TerrainEditOperation Bridge(
        Vector2Int startPixel,
        Vector2 direction,
        int lengthPixels,
        int thicknessPixels)
    {
        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;
        return new TerrainEditOperation
        {
            kind = TerrainEditOperationKind.CreateBridge,
            centerPixelX = startPixel.x,
            centerPixelY = startPixel.y,
            terrainType = TerrainType.Created,
            directionX = normalizedDirection.x,
            directionY = normalizedDirection.y,
            lengthPx = Mathf.Max(0, lengthPixels),
            thicknessPx = Mathf.Max(1, thicknessPixels)
        };
    }
}

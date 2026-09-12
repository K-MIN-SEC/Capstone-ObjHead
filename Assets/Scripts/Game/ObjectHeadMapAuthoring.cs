using System;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadPlayerSpawnRegion
{
    [Range(1, 4)] public int playerIndex = 1;
    public Transform minimum;
    public Transform maximum;

    public bool TryGetXRange(out float minimumX, out float maximumX)
    {
        minimumX = 0f;
        maximumX = 0f;
        if (minimum == null || maximum == null)
        {
            return false;
        }

        minimumX = Mathf.Min(minimum.position.x, maximum.position.x);
        maximumX = Mathf.Max(minimum.position.x, maximum.position.x);
        return maximumX > minimumX;
    }
}

[DisallowMultipleComponent]
public sealed class ObjectHeadMapAuthoring : MonoBehaviour
{
    [Header("Move these markers in the Scene view")]
    [SerializeField] private Transform terrainOriginMarker;
    [SerializeField] private Transform waterSurfaceMarker;
    [SerializeField] private ObjectHeadPlayerSpawnRegion[] playerSpawnRegions = Array.Empty<ObjectHeadPlayerSpawnRegion>();

    [Header("Scene gizmos")]
    [SerializeField] private float spawnRegionHeight = 8f;
    [SerializeField] private Color playerOneColor = new Color(0.25f, 0.65f, 1f, 0.28f);
    [SerializeField] private Color playerTwoColor = new Color(1f, 0.35f, 0.3f, 0.28f);
    [SerializeField] private Color otherPlayerColor = new Color(0.75f, 0.45f, 1f, 0.28f);

    public bool TryGetTerrainOrigin(out Vector2 position)
    {
        position = terrainOriginMarker != null ? terrainOriginMarker.position : Vector3.zero;
        return terrainOriginMarker != null;
    }

    public bool TryGetWaterSurfaceY(out float worldY)
    {
        worldY = waterSurfaceMarker != null ? waterSurfaceMarker.position.y : 0f;
        return waterSurfaceMarker != null;
    }

    public bool TryGetPlayerSpawnXRange(int playerIndex, out float minimumX, out float maximumX)
    {
        ObjectHeadPlayerSpawnRegion[] regions = playerSpawnRegions ?? Array.Empty<ObjectHeadPlayerSpawnRegion>();
        for (int index = 0; index < regions.Length; index++)
        {
            ObjectHeadPlayerSpawnRegion region = regions[index];
            if (region != null && region.playerIndex == playerIndex)
            {
                return region.TryGetXRange(out minimumX, out maximumX);
            }
        }

        minimumX = 0f;
        maximumX = 0f;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (terrainOriginMarker != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(terrainOriginMarker.position, 0.35f);
        }

        if (waterSurfaceMarker != null)
        {
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.85f);
            Vector3 center = waterSurfaceMarker.position;
            Gizmos.DrawLine(center + Vector3.left * 30f, center + Vector3.right * 30f);
        }

        ObjectHeadPlayerSpawnRegion[] regions = playerSpawnRegions ?? Array.Empty<ObjectHeadPlayerSpawnRegion>();
        foreach (ObjectHeadPlayerSpawnRegion region in regions)
        {
            if (region == null || !region.TryGetXRange(out float minimumX, out float maximumX))
            {
                continue;
            }

            Gizmos.color = GetRegionColor(region.playerIndex);
            float centerX = (minimumX + maximumX) * 0.5f;
            float centerY = transform.position.y + spawnRegionHeight * 0.5f;
            Gizmos.DrawCube(
                new Vector3(centerX, centerY, 0f),
                new Vector3(maximumX - minimumX, spawnRegionHeight, 0.1f));
        }
    }

    private Color GetRegionColor(int playerIndex)
    {
        if (playerIndex == 1)
        {
            return playerOneColor;
        }

        if (playerIndex == 2)
        {
            return playerTwoColor;
        }

        return otherPlayerColor;
    }
}

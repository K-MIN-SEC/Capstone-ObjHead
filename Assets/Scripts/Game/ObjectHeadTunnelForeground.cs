using UnityEngine;

/// <summary>Visual cover over a collision-free authored tunnel. The active character reveals its interior.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ObjectHeadTunnelForeground : MonoBehaviour
{
    [SerializeField] private TerrainManager terrain;
    [SerializeField] private ObjectHeadMapRecipe recipe;
    [SerializeField, Range(0f, 1f)] private float occupiedAlpha = .24f;
    [SerializeField, Min(0f)] private float fadeSpeed = 8f;

    private SpriteRenderer cover;
    private TurnManager turns;
    private Texture2D runtimeTexture;
    private Sprite runtimeSprite;
    private Color32[] networkPixels;

    public void Configure(TerrainManager terrainManager, ObjectHeadMapRecipe mapRecipe)
    {
        terrain = terrainManager;
        recipe = mapRecipe;
        if (Application.isPlaying) Initialize();
        else
        {
            cover = GetComponent<SpriteRenderer>();
            cover.sprite = recipe != null ? recipe.bakedTunnelForeground : null;
            cover.sortingOrder = 20;
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying) Initialize();
    }

    private void Start()
    {
        if (Application.isPlaying) Initialize();
    }

    private void Initialize()
    {
        if (runtimeTexture != null || recipe == null || recipe.bakedTunnelForeground == null) return;
        cover = GetComponent<SpriteRenderer>();
        if (terrain == null) terrain = GetComponentInParent<TerrainManager>();
        Texture2D source = recipe.bakedTunnelForeground.texture;
        runtimeTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        runtimeTexture.filterMode = FilterMode.Bilinear;
        networkPixels = source.GetPixels32();
        runtimeTexture.SetPixels32(networkPixels);
        runtimeTexture.Apply();
        runtimeSprite = Sprite.Create(runtimeTexture, new Rect(0, 0, source.width, source.height),
            Vector2.zero, recipe.pixelsPerUnit);
        cover.sprite = runtimeSprite;
        cover.sortingOrder = 20;
        if (terrain != null) terrain.RegisterNetworkForeground(this);
    }

    private void Update()
    {
        if (cover == null || terrain == null || recipe == null) return;
        if (turns == null) turns = FindAnyObjectByType<TurnManager>();
        bool occupied = false;
        if (turns != null && turns.Characters != null)
            foreach (TurnCharacterController character in turns.Characters)
                if (character != null && InTunnel(terrain.WorldToPixel(character.transform.position)))
                {
                    occupied = true;
                    break;
                }
        Color tint = cover.color;
        tint.a = Mathf.MoveTowards(tint.a, occupied ? occupiedAlpha : 1f, fadeSpeed * Time.deltaTime);
        cover.color = tint;
    }

    private bool InTunnel(Vector2Int pixel)
    {
        if (recipe.artworkCutouts == null) return false;
        foreach (ObjectHeadTerrainPolygon cutout in recipe.artworkCutouts)
        {
            Vector2[] points = cutout?.points;
            if (points == null || points.Length < 3) continue;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if ((points[i].y > pixel.y) != (points[j].y > pixel.y) &&
                    pixel.x < (points[j].x - points[i].x) * (pixel.y - points[i].y) /
                    (points[j].y - points[i].y) + points[i].x) inside = !inside;
            }
            if (inside) return true;
        }
        return false;
    }

    public Color32 ReadNetworkPixel(int x, int y) => runtimeTexture != null && x < runtimeTexture.width && y < runtimeTexture.height
        ? networkPixels[y * runtimeTexture.width + x] : Color.clear;

    public void WriteNetworkPixel(int x, int y, Color32 color)
    {
        if (runtimeTexture != null && x < runtimeTexture.width && y < runtimeTexture.height)
            networkPixels[y * runtimeTexture.width + x] = color;
    }

    public void FlushNetworkPixels()
    {
        if (runtimeTexture == null) return;
        runtimeTexture.SetPixels32(networkPixels);
        runtimeTexture.Apply(false);
    }

    public bool ApplyTerrainOperation(TerrainEditOperation operation)
    {
        if (runtimeTexture == null || operation.kind != TerrainEditOperationKind.DestroyCircle) return false;
        int radius = operation.radiusXPx;
        int minX = Mathf.Max(0, operation.centerPixelX - radius);
        int maxX = Mathf.Min(runtimeTexture.width - 1, operation.centerPixelX + radius);
        int minY = Mathf.Max(0, operation.centerPixelY - radius);
        int maxY = Mathf.Min(runtimeTexture.height - 1, operation.centerPixelY + radius);
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            int dx = x - operation.centerPixelX, dy = y - operation.centerPixelY;
            if (dx * dx + dy * dy > radius * radius) continue;
            int pixel = y * runtimeTexture.width + x;
            if (networkPixels[pixel].a == 0) continue;
            networkPixels[pixel] = Color.clear;
            terrain.InvalidateNetworkPixel(x, y);
            changed = true;
        }
        if (changed) FlushNetworkPixels();
        return changed;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }
        else
        {
            if (runtimeSprite != null) DestroyImmediate(runtimeSprite);
            if (runtimeTexture != null) DestroyImmediate(runtimeTexture);
        }
    }
}

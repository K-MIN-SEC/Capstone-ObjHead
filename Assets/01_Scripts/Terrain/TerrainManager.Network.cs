using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public partial class TerrainManager
{
    // Fixed network tiles are independent of the collider chunk size.
    public const int NetworkTileSize = 128;
    private uint[] networkTileHashes;
    private bool[] networkTileDirty;
    private ObjectHeadTunnelForeground networkForeground;
    public void RegisterNetworkForeground(ObjectHeadTunnelForeground foreground)
    {
        networkForeground = foreground;
        ResetNetworkTileCache();
    }
    public void InvalidateNetworkPixel(int x, int y) => MarkNetworkTileDirty(x, y);
    public int NetworkTileColumns => (WidthPx + NetworkTileSize - 1) / NetworkTileSize;
    public int NetworkTileCount => NetworkTileColumns * ((HeightPx + NetworkTileSize - 1) / NetworkTileSize);

    private void ResetNetworkTileCache()
    {
        networkTileHashes = new uint[NetworkTileCount];
        networkTileDirty = new bool[NetworkTileCount];
        Array.Fill(networkTileDirty, true);
    }

    private void MarkNetworkTileDirty(int x, int y)
    {
        if (networkTileDirty != null && x >= 0 && y >= 0 && x < WidthPx && y < HeightPx)
            networkTileDirty[(y / NetworkTileSize) * NetworkTileColumns + x / NetworkTileSize] = true;
    }

    private RectInt NetworkTileRect(int index)
    {
        int x = (index % NetworkTileColumns) * NetworkTileSize;
        int y = (index / NetworkTileColumns) * NetworkTileSize;
        return new RectInt(x, y, Mathf.Min(NetworkTileSize, WidthPx - x), Mathf.Min(NetworkTileSize, HeightPx - y));
    }

    public uint[] CaptureNetworkTileHashes()
    {
        if (!EnsureInitialized()) return Array.Empty<uint>();
        if (networkTileDirty == null) ResetNetworkTileCache();
        var visual = runtimeVisualTexture.GetRawTextureData<Color32>();
        var collision = runtimeCollisionTexture.GetRawTextureData<Color32>();
        for (int tile = 0; tile < networkTileHashes.Length; tile++)
        {
            if (!networkTileDirty[tile]) continue;
            uint hash = 2166136261;
            var rect = NetworkTileRect(tile);
            unchecked
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    int i = y * WidthPx + x;
                    var a = visual[i]; var b = collision[i];
                    hash = (hash ^ a.r) * 16777619; hash = (hash ^ a.g) * 16777619;
                    hash = (hash ^ a.b) * 16777619; hash = (hash ^ a.a) * 16777619;
                    hash = (hash ^ b.r) * 16777619; hash = (hash ^ b.g) * 16777619;
                    hash = (hash ^ b.b) * 16777619; hash = (hash ^ b.a) * 16777619;
                    hash = (hash ^ (uint)terrainTypeMask[x,y]) * 16777619;
                    hash = (hash ^ (solidMask[x,y] ? 1u : 0u)) * 16777619;
                    Color32 cover = networkForeground != null ? networkForeground.ReadNetworkPixel(x,y) : Color.clear;
                    hash = (hash ^ cover.r) * 16777619; hash = (hash ^ cover.g) * 16777619;
                    hash = (hash ^ cover.b) * 16777619; hash = (hash ^ cover.a) * 16777619;
                }
            }
            networkTileHashes[tile] = hash;
            networkTileDirty[tile] = false;
        }
        return (uint[])networkTileHashes.Clone();
    }

    public string CaptureNetworkTile(int index)
    {
        if (!EnsureInitialized() || index < 0 || index >= NetworkTileCount) return null;
        var rect = NetworkTileRect(index);
        var visual = runtimeVisualTexture.GetRawTextureData<Color32>();
        var collision = runtimeCollisionTexture.GetRawTextureData<Color32>();
        using var output = new MemoryStream();
        using (var compressed = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
        using (var writer = new BinaryWriter(compressed))
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = y * WidthPx + x;
                var a = visual[i]; var b = collision[i];
                writer.Write(a.r); writer.Write(a.g); writer.Write(a.b); writer.Write(a.a);
                writer.Write(b.r); writer.Write(b.g); writer.Write(b.b); writer.Write(b.a);
                writer.Write((byte)terrainTypeMask[x,y]); writer.Write(solidMask[x,y]);
                Color32 cover = networkForeground != null ? networkForeground.ReadNetworkPixel(x,y) : Color.clear;
                writer.Write(cover.r); writer.Write(cover.g); writer.Write(cover.b); writer.Write(cover.a);
            }
        }
        return Convert.ToBase64String(output.ToArray());
    }

    public bool ApplyNetworkTile(int index, string data)
    {
        if (!EnsureInitialized() || index < 0 || index >= NetworkTileCount ||
            string.IsNullOrEmpty(data) || data.Length > NetworkTileSize * NetworkTileSize * 24) return false;
        var rect = NetworkTileRect(index);
        // Bound decompression before touching terrain. Reject malformed/truncated payloads atomically.
        var bytes = new byte[rect.width * rect.height * 14];
        try
        {
            using var input = new MemoryStream(Convert.FromBase64String(data));
            using var compressed = new DeflateStream(input, CompressionMode.Decompress);
            int read = 0;
            while (read < bytes.Length)
            {
                int count = compressed.Read(bytes, read, bytes.Length - read);
                if (count == 0) return false;
                read += count;
            }
            if (compressed.ReadByte() != -1) return false;
        }
        catch (Exception ex) when (ex is FormatException || ex is InvalidDataException || ex is IOException)
        { return false; }
        for (int i = 0; i < bytes.Length; i += 14)
            if (bytes[i+8] > (byte)TerrainType.Cloud || bytes[i+9] > 1) return false;

        var visual = runtimeVisualTexture.GetRawTextureData<Color32>();
        var collision = runtimeCollisionTexture.GetRawTextureData<Color32>();
        int offset = 0;
        for (int y = rect.yMin; y < rect.yMax; y++)
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            int i = y * WidthPx + x;
            visual[i] = new Color32(bytes[offset],bytes[offset+1],bytes[offset+2],bytes[offset+3]);
            collision[i] = new Color32(bytes[offset+4],bytes[offset+5],bytes[offset+6],bytes[offset+7]);
            terrainTypeMask[x,y] = (TerrainType)bytes[offset+8];
            solidMask[x,y] = bytes[offset+9] != 0;
            networkForeground?.WriteNetworkPixel(x,y,new Color32(bytes[offset+10],bytes[offset+11],bytes[offset+12],bytes[offset+13]));
            offset += 14;
            MarkDirtyPixelAndNeighbors(x,y);
        }
        networkForeground?.FlushNetworkPixels();
        ApplyTextureAndDirtyChunks(true);
        return true;
    }
}

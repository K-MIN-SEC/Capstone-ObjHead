using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadTerrainSyncMessage
{
    public int protocolVersion = ObjectHeadNetworkProtocol.ProtocolVersion;
    public long matchEpoch;
    public string kind, recipient;
    public long revision;
    public int width, height, ppu;
    public float originX, originY;
    public bool resetRevision;
    public uint[] hashes;
    public int[] tiles;
    public int tile;
    public string data;
    public TerrainEditOperation operation;
}

/// <summary>Ordered edit log plus exact, bounded tile repair. Only the host writes terrain.</summary>
public sealed class ObjectHeadTerrainSync : MonoBehaviour
{
    private const int HistoryLimit = 4096;
    private ObjectHeadNetworkManager network;
    private TerrainManager terrain;
    private GameStartData match;
    private long revision;
    private float nextManifest, nextRequest, nextTile;
    private bool started, initialBarrierPassed;
    private readonly SortedDictionary<long,TerrainEditOperation> history = new();
    private readonly SortedDictionary<long,TerrainEditOperation> pending = new();
    private readonly HashSet<string> readyPeers = new();
    private readonly Queue<(string user, int tile)> repairQueue = new();
    private readonly HashSet<(string user, int tile)> queuedRepairs = new();
    private readonly Dictionary<string,float> requestTimes = new();
    private ObjectHeadTerrainSyncMessage manifest;
    public bool IsSynchronized { get; private set; }
    public bool InitialBarrierPassed => network != null && network.IsHost ? initialBarrierPassed : IsSynchronized;
    public int AppliedOperations { get; private set; }
    public int SentOperations { get; private set; }
    public int RepairedTiles { get; private set; }

    public void Initialize(ObjectHeadNetworkManager manager, TerrainManager map, GameStartData data)
    {
        network = manager; terrain = map; match = data;
        network.GameplayMessageReceived += Receive;
        if (network.IsHost) terrain.OperationApplied += OnOperation;
        started = true;
        IsSynchronized = network.IsHost;
    }

    private void OnDestroy()
    {
        if (network != null) network.GameplayMessageReceived -= Receive;
        if (terrain != null) terrain.OperationApplied -= OnOperation;
    }

    private void Update()
    {
        if (!started || !network.IsInMatch || !terrain.IsInitialized) return;
        if (network.IsHost)
        {
            if (!initialBarrierPassed)
                initialBarrierPassed = (match.players ?? Array.Empty<ObjectHeadPlayerAssignment>())
                    .All(p => p.isAi || p.userId == network.LocalUserId || readyPeers.Contains(p.userId));
            if (Time.unscaledTime >= nextManifest)
            {
                nextManifest = Time.unscaledTime + 1;
                SendManifest();
            }
            // Pace large repairs so they do not swamp input/state traffic.
            if (repairQueue.Count > 0 && Time.unscaledTime >= nextTile)
            {
                nextTile = Time.unscaledTime + .1f;
                var request = repairQueue.Dequeue(); queuedRepairs.Remove(request);
                Send(new ObjectHeadTerrainSyncMessage { kind="tile", recipient=request.user,
                    revision=revision, tile=request.tile, data=terrain.CaptureNetworkTile(request.tile) });
            }
        }
        else if (Time.unscaledTime >= nextRequest)
        {
            nextRequest = Time.unscaledTime + .75f;
            CheckManifest();
        }
    }

    private void OnOperation(TerrainEditOperation operation)
    {
        history[++revision] = operation;
        while (history.Count > HistoryLimit) history.Remove(history.Keys.First());
        Send(new ObjectHeadTerrainSyncMessage { kind="edit", revision=revision, operation=operation });
        SentOperations++;
    }

    private void SendManifest(string recipient=null, bool reset=false)
    {
        Send(new ObjectHeadTerrainSyncMessage { kind="manifest", recipient=recipient, revision=revision,
            width=terrain.WidthPx, height=terrain.HeightPx, ppu=terrain.PixelsPerUnit,
            originX=terrain.TerrainOriginWorld.x, originY=terrain.TerrainOriginWorld.y,
            hashes=terrain.CaptureNetworkTileHashes(), resetRevision=reset });
    }

    private void CheckManifest()
    {
        if (manifest == null || manifest.revision > revision)
        {
            IsSynchronized = false;
            Send(new ObjectHeadTerrainSyncMessage { kind="request", revision=revision });
            return;
        }
        if (manifest.revision < revision) return; // Wait for a checksum of the current revision.
        if (manifest.width != terrain.WidthPx || manifest.height != terrain.HeightPx ||
            manifest.ppu != terrain.PixelsPerUnit || manifest.hashes == null ||
            manifest.hashes.Length != terrain.NetworkTileCount ||
            Vector2.Distance(new Vector2(manifest.originX,manifest.originY),terrain.TerrainOriginWorld)>.001f)
        {
            IsSynchronized = false;
            Debug.LogError("[TerrainSync] Map layout/build mismatch. Both players must use the same build.");
            return;
        }
        uint[] local = terrain.CaptureNetworkTileHashes();
        var mismatches = new List<int>();
        for (int i=0; i<local.Length && mismatches.Count<4; i++)
            if (local[i] != manifest.hashes[i]) mismatches.Add(i);
        IsSynchronized = mismatches.Count == 0;
        Send(IsSynchronized
            ? new ObjectHeadTerrainSyncMessage { kind="ready", revision=revision }
            : new ObjectHeadTerrainSyncMessage { kind="repair", revision=revision, tiles=mismatches.ToArray() });
    }

    private void Receive(long opCode, string sender, string json)
    {
        if (opCode != ObjectHeadNetworkProtocol.TerrainSync || !started || !terrain.IsInitialized) return;
        ObjectHeadTerrainSyncMessage message;
        try { message = JsonUtility.FromJson<ObjectHeadTerrainSyncMessage>(json); }
        catch (ArgumentException) { return; }
        if (message == null || message.protocolVersion != ObjectHeadNetworkProtocol.ProtocolVersion ||
            message.matchEpoch != match.startedAtUnixMilliseconds ||
            (!string.IsNullOrEmpty(message.recipient) && message.recipient != network.LocalUserId)) return;
        if (network.IsHost)
        {
            if (!(match.players ?? Array.Empty<ObjectHeadPlayerAssignment>()).Any(p=>p.userId==sender && !p.isAi)) return;
            if (message.kind=="ready")
            {
                if (message.revision==revision) readyPeers.Add(sender);
                return;
            }
            // Limit retransmission work from any individual peer.
            if (requestTimes.TryGetValue(sender,out float previous) && Time.unscaledTime-previous<.3f) return;
            requestTimes[sender]=Time.unscaledTime;
            if (message.kind=="request")
            {
                bool unavailable = message.revision<0 || message.revision>revision ||
                    (message.revision<revision && !history.ContainsKey(message.revision+1));
                if (!unavailable)
                    foreach (var edit in history.Where(e=>e.Key>message.revision).Take(32))
                        Send(new ObjectHeadTerrainSyncMessage { kind="edit", recipient=sender, revision=edit.Key, operation=edit.Value });
                // A very old peer recovers directly from exact tiles instead of an unbounded history.
                SendManifest(sender,unavailable);
            }
            else if (message.kind=="repair" && message.tiles!=null)
                foreach (int tile in message.tiles.Take(4))
                {
                    var key=(sender,tile);
                    if (tile>=0 && tile<terrain.NetworkTileCount && repairQueue.Count<32 && queuedRepairs.Add(key)) repairQueue.Enqueue(key);
                }
            return;
        }
        if (sender!=network.HostUserId) return;
        switch (message.kind)
        {
            case "edit":
                if (message.revision<=revision) return;
                if (pending.Count>=HistoryLimit) { pending.Clear(); manifest=null; return; }
                pending[message.revision]=message.operation;
                revision=ApplyContiguous(revision,pending,edit=>{terrain.ApplyOperation(edit); AppliedOperations++;});
                break;
            case "manifest":
                if (message.revision<revision) return;
                if (message.resetRevision)
                {
                    revision=message.revision; pending.Clear(); IsSynchronized=false;
                }
                manifest=message;
                break;
            case "tile":
                // Never roll a tile back to an older version or apply it ahead of queued edits.
                if (message.revision==revision && terrain.ApplyNetworkTile(message.tile,message.data)) RepairedTiles++;
                break;
        }
    }

    private async void Send(ObjectHeadTerrainSyncMessage message)
    {
        message.matchEpoch=match.startedAtUnixMilliseconds;
        try { await network.SendGameplayMessageAsync(ObjectHeadNetworkProtocol.TerrainSync,message); }
        catch (Exception ex) { Debug.LogWarning("[TerrainSync] Send failed; retrying via revision check: "+ex.Message); }
    }

    public static long ApplyContiguous(long current, SortedDictionary<long,TerrainEditOperation> edits, Action<TerrainEditOperation> apply)
    {
        while (edits.TryGetValue(current+1,out var edit))
        {
            apply(edit);
            edits.Remove(++current);
        }
        return current;
    }
}

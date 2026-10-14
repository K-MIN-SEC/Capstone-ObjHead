using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Native-free protocol regressions. These exercise the real transport helpers without
// connecting an EOS account; a separate multi-PC EOS session is still required.
public static class ObjectHeadLobbyTransportAudit
{
    private static int checks;
    private const BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

    public static void Run()
    {
        checks = 0;
        var root = new GameObject("LobbyTransportAudit");
        try
        {
            var transport = root.AddComponent<ObjectHeadEosTransport>();
            Set(transport, "lobbyId", "audit-room");
            // A null native identity intentionally avoids SDK calls. Equal empty IDs
            // make this component act as host for the inbound packet parser alone.
            Set(transport, "hostId", string.Empty);
            var admitted = (HashSet<string>)Get(transport, "admitted");
            foreach (string peer in new[] { "second", "third", "fourth" }) admitted.Add(peer);
            var received = new Dictionary<string, string>();
            transport.MessageReceived += (op, sender, json) => received[sender] = json;

            var payloads = new Dictionary<string, string>();
            var packets = new Dictionary<string, List<byte[]>>();
            foreach (string peer in admitted)
            {
                string payload = "{\"player\":\"" + peer + "\",\"roster\":\"" + new string('방', 800) + "\"}";
                payloads[peer] = payload;
                packets[peer] = Pack("audit-room", 41, ObjectHeadNetworkProtocol.LobbyState, payload);
                Check(packets[peer].Count > 2, peer + " uses a fragmented Unicode lobby payload");
            }
            // Every peer uses the same message ID, as independently started clients do.
            // Interleave them and repeat fragments; the sender is part of reassembly identity.
            for (int index = packets.Values.Max(value => value.Count) - 1; index >= 0; index--)
                foreach (string peer in admitted)
                    if (index < packets[peer].Count)
                    {
                        Receive(transport, peer, packets[peer][index]);
                        if (index > 0) Receive(transport, peer, packets[peer][index]);
                    }
            Check(received.Count == 3, "second, third and fourth peers all complete independently");
            foreach (var expected in payloads)
                Check(received.TryGetValue(expected.Key, out string actual) && actual == expected.Value,
                    expected.Key + " fragmented message is exact");

            int before = received.Count;
            foreach (byte[] packet in Pack("stale-room", 42, ObjectHeadNetworkProtocol.LobbyState, "wrong room"))
                Receive(transport, "second", packet);
            Check(received["second"] == payloads["second"], "stale-room packets do not replace current lobby");
            foreach (byte[] packet in Pack("audit-room", 43, ObjectHeadNetworkProtocol.LobbyState, "unapproved"))
                Receive(transport, "stranger", packet);
            Check(received.Count == before, "unadmitted peer cannot submit game messages");

            var visited = new List<string>();
            Func<string, Task> failingSecond = peer =>
            {
                visited.Add(peer);
                return peer == "second" ? Task.FromException(new InvalidOperationException("simulated queue failure")) : Task.CompletedTask;
            };
            Task broadcast = (Task)typeof(ObjectHeadEosTransport)
                .GetMethod("BroadcastToPeersAsync", StaticPrivate)
                .Invoke(null, new object[] { new[] { "second", "third", "fourth" }, failingSecond });
            bool failureReported = false;
            try { broadcast.GetAwaiter().GetResult(); }
            catch (AggregateException exception) { failureReported = exception.InnerExceptions.Count == 1; }
            Check(visited.SequenceEqual(new[] { "second", "third", "fourth" }), "one failed peer cannot starve later peers");
            Check(failureReported, "partial broadcast failure is reported rather than swallowed");

            Check(Capacity(4, 0, 3) == 4, "three humans retain a four-human room");
            Check(Capacity(4, 1, 3) == 3, "one AI reserves the fourth slot from EOS joins");
            Check(Capacity(4, 3, 1) == 1, "host plus three AI reserves three EOS slots");
            Check(Capacity(2, 0, 2) == 2, "two humans can use duel capacity");
            Check(RejectsCapacity(2, 0, 3), "third joining EOS member blocks a shrink even before hello");
            Check(RejectsCapacity(4, 2, 3), "AI cannot take an occupied human slot");
            Check(RejectsCapacity(4, 4, 0), "an AI reservation cannot remove the host slot");
            Debug.Log("[ObjectHeadLobbyTransportAudit] PASS " + checks + " checks");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static List<byte[]> Pack(string room, int messageId, long opCode, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(room + "\n" + opCode + "|" + json);
        int count = (bytes.Length + 959) / 960;
        var result = new List<byte[]>();
        for (int index = 0; index < count; index++)
        {
            int length = Math.Min(960, bytes.Length - index * 960);
            byte[] packet = new byte[16 + length];
            packet[0] = (byte)'O'; packet[1] = (byte)'H'; packet[2] = (byte)'B'; packet[3] = (byte)'1';
            Buffer.BlockCopy(BitConverter.GetBytes(messageId), 0, packet, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)index), 0, packet, 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)count), 0, packet, 10, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, packet, 12, 4);
            Buffer.BlockCopy(bytes, index * 960, packet, 16, length);
            result.Add(packet);
        }
        return result;
    }

    private static int Capacity(int capacity, int ai, int humans) => (int)typeof(ObjectHeadEosTransport)
        .GetMethod("HumanCapacityFor", StaticPrivate).Invoke(null, new object[] { capacity, ai, humans });
    private static bool RejectsCapacity(int capacity, int ai, int humans)
    {
        try { Capacity(capacity, ai, humans); return false; }
        catch (TargetInvocationException exception) { return exception.InnerException is InvalidOperationException; }
    }
    private static void Receive(ObjectHeadEosTransport transport, string sender, byte[] packet) =>
        typeof(ObjectHeadEosTransport).GetMethod("ReceiveFragment", InstancePrivate)
            .Invoke(transport, new object[] { sender, packet, packet.Length });
    private static object Get(ObjectHeadEosTransport transport, string name) =>
        typeof(ObjectHeadEosTransport).GetField(name, InstancePrivate).GetValue(transport);
    private static void Set(ObjectHeadEosTransport transport, string name, object value) =>
        typeof(ObjectHeadEosTransport).GetField(name, InstancePrivate).SetValue(transport, value);
    private static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException("Lobby transport audit failed: " + label);
        checks++;
    }
}

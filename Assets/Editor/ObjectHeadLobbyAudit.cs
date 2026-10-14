using System;
using System.Linq;
using UnityEngine;

public static class ObjectHeadLobbyAudit
{
    public static void BuildPlaytest()
    {
        ObjectHeadTitlePolishAuthoring.ApplyOnlineLobbyControls();
        Run();
        ObjectHeadLobbyRuntimeAudit.Run();
        ObjectHeadLobbyTransportAudit.Run();
        ObjectHeadFeelAudit.BuildPlaytest();
    }

    public static void Run()
    {
        int checks = 0;
        void Check(bool pass, string text)
        {
            if (!pass) throw new InvalidOperationException("[LOBBY_AUDIT_FAIL] " + text);
            checks++;
        }
        var catalog = ObjectHeadContent.Load();
        ObjectHeadLobbyState Room(ObjectHeadMatchMode mode, int count, int humans)
        {
            int capacity = catalog.Mode(mode).players;
            return new ObjectHeadLobbyState
            {
                hostUserId = "human-1", settings = new ObjectHeadRoomSettings { mode = mode, minPlayers = 2, maxPlayers = capacity },
                players = Enumerable.Range(1, count).Select(index => new ObjectHeadLobbyPlayer
                {
                    userId = (index <= humans ? "human-" : "ai-") + index,
                    playerIndex = index, isAi = index > humans, ready = true,
                    characters = catalog.DefaultSelection(capacity)
                }).ToArray()
            };
        }
        foreach (var mode in new[] { ObjectHeadMatchMode.FreeForAll, ObjectHeadMatchMode.Teams })
        foreach (int count in new[] { 2, 3, 4 })
        foreach (int humans in Enumerable.Range(1, count))
        {
            var lobby = Room(mode, count, humans);
            Check(ObjectHeadLobbyRules.CanStart(lobby, out _), $"{mode} {humans} human + {count-humans} AI starts");
            Check(ObjectHeadLobbyRules.CanChangeMode(lobby, ObjectHeadMatchMode.Duel, out _) == (count <= 2), "duel capacity prevents silent removal");
            var replica = JsonUtility.FromJson<ObjectHeadLobbyState>(JsonUtility.ToJson(lobby));
            Check(ObjectHeadLobbyRules.CanStart(replica, out _), "lobby serialization preserves readiness and two-head selection");
            ObjectHeadLobbyRules.ResetReady(lobby);
            Check(lobby.players.All(p => p.ready == p.isAi), "roster changes reset humans but keep AI ready");
            Check(!ObjectHeadLobbyRules.CanStart(lobby, out _), "human must reconfirm after roster change");
        }
        Check(!ObjectHeadLobbyRules.CanStart(Room(ObjectHeadMatchMode.FreeForAll, 1, 1), out _), "cannot start without opponent");
        Check(ObjectHeadLobbyRules.CanStart(Room(ObjectHeadMatchMode.Duel, 2, 2), out _), "duel still accepts its three-head roster");
        var three = Room(ObjectHeadMatchMode.FreeForAll, 3, 3);
        three.players[1].characters = catalog.DefaultSelection(2);
        Check(!ObjectHeadLobbyRules.CanStart(three, out _), "actual three occupants do not change four-seat roster rule");
        three.players[1].characters = catalog.DefaultSelection(4);
        three.players[1].playerIndex = 1;
        Check(!ObjectHeadLobbyRules.CanStart(three, out _), "duplicate seat rejected");
        var mixed = Room(ObjectHeadMatchMode.FreeForAll, 4, 2);
        var lateHuman = new ObjectHeadLobbyPlayer { userId = "late-human", playerIndex = 0 };
        var seats = ObjectHeadLobbyRules.AssignSeats(new[] { mixed.players[0], mixed.players[1], mixed.players[2], lateHuman });
        Check(seats[0].userId == "human-1" && seats[1].userId == "human-2" && seats[2] == lateHuman && seats[3].isAi,
            "third human keeps previous humans stable and moves AI to last seat");
        var afterLeave = ObjectHeadLobbyRules.AssignSeats(seats.Where(p => p.userId != "human-2").ToArray());
        Check(afterLeave.Select(p => p.playerIndex).SequenceEqual(new[] { 1, 2, 3 }), "leaving never leaves an invalid seat gap");
        Check(!ObjectHeadLobbyRules.CanStart(null, out _), "missing lobby rejected");
        Check(ObjectHeadNetworkProtocol.ProtocolVersion == 7, "old playtest clients cannot silently use new lobby protocol");
        Debug.Log("[LOBBY_AUDIT_PASS] " + checks + " capacity, three-player, AI, readiness and serialization checks");
    }
}

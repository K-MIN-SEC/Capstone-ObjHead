using System;
using System.Linq;

// Room capacity belongs to the selected mode, not to today's number of occupants.
// In a four-seat mode, two or three occupants still select two characters each.
public static class ObjectHeadLobbyRules
{
    public static bool CanChangeMode(ObjectHeadLobbyState lobby, ObjectHeadMatchMode mode, out string reason)
    {
        reason = string.Empty;
        var definition = ObjectHeadContent.Load().Mode(mode);
        if (definition == null) { reason = "unsupported_mode"; return false; }
        if ((lobby?.players?.Length ?? 0) > definition.players)
        { reason = "room_capacity_too_small"; return false; }
        return true;
    }

    public static bool CanStart(ObjectHeadLobbyState lobby, out string reason)
    {
        reason = string.Empty;
        if (lobby?.settings == null) { reason = "lobby_not_ready"; return false; }
        var content = ObjectHeadContent.Load();
        var mode = content.Mode(lobby.settings.mode);
        var players = lobby.players ?? Array.Empty<ObjectHeadLobbyPlayer>();
        if (mode == null || players.Any(p => p == null || string.IsNullOrEmpty(p.userId)) ||
            players.Select(p => p.userId).Distinct().Count() != players.Length ||
            !players.Any(p => !p.isAi && p.userId == lobby.hostUserId))
        { reason = "lobby_not_ready"; return false; }
        if (players.Length < 2) { reason = "not_enough_players"; return false; }
        if (players.Length > mode.players) { reason = "room_capacity_too_small"; return false; }
        if (!players.Select(p => p.playerIndex).OrderBy(i => i).SequenceEqual(Enumerable.Range(1, players.Length)))
        { reason = "lobby_not_ready"; return false; }
        if (players.Any(p => !p.ready)) { reason = "all_players_ready_required"; return false; }
        if (players.Any(p => !content.ValidSelection(p.characters, mode.players)))
        { reason = "selection_required"; return false; }
        return true;
    }

    public static void ResetReady(ObjectHeadLobbyState lobby)
    {
        foreach (var player in lobby?.players ?? Array.Empty<ObjectHeadLobbyPlayer>())
            player.ready = player.isAi;
    }

    // Keep human seat order stable; bots occupy the remaining seats, not EOS peer IDs.
    public static ObjectHeadLobbyPlayer[] AssignSeats(ObjectHeadLobbyPlayer[] players)
    {
        return (players ?? Array.Empty<ObjectHeadLobbyPlayer>()).OrderBy(p => p.isAi)
            .ThenBy(p => p.playerIndex > 0 ? p.playerIndex : int.MaxValue)
            .Select((player, index) => { player.playerIndex = index + 1; return player; }).ToArray();
    }
}

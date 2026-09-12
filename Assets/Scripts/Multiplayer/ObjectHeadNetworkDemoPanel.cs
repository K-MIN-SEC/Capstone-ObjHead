using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ObjectHeadNetworkDemoPanel : MonoBehaviour
{
    private ObjectHeadRoomSettings editableSettings;

    private ObjectHeadNetworkManager network;
    private bool expanded;
    private bool busy;
    private string scheme;
    private string host;
    private string port;
    private string serverKey;
    private string displayName = "Player";
    private string profileId = "A";
    private string joinMatchId = string.Empty;
    private string error = string.Empty;
    private Vector2 scroll;

    private void Awake()
    {
        network = GetComponent<ObjectHeadNetworkManager>();
        ObjectHeadNetworkConfig config = network.Config;
        ObjectHeadServerProfile profile = config.GetProfile();
        scheme = profile.scheme;
        host = profile.host;
        port = profile.port.ToString();
        serverKey = profile.serverKey;
        editableSettings = config.DefaultRoomSettings;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            expanded = !expanded;
        }
    }

    private void OnGUI()
    {
        ObjectHeadNetworkConfig config = network.Config;
        float height = expanded
            ? Mathf.Min(Screen.height - config.PanelMargin * 2f, config.PanelMaxHeight)
            : 42f;
        GUILayout.BeginArea(
            new Rect(config.PanelMargin, config.PanelMargin, config.PanelWidth, height),
            GUI.skin.box);
        if (GUILayout.Button(expanded ? "OBJECT HEAD MULTIPLAYER  [F8: CLOSE]" : "MULTIPLAYER  [F8]", GUILayout.Height(30f)))
        {
            expanded = !expanded;
        }

        if (!expanded)
        {
            GUILayout.EndArea();
            return;
        }

        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("Status: " + network.Status);
        if (!string.IsNullOrEmpty(error))
        {
            GUILayout.Label("Error: " + error);
        }

        GUI.enabled = !busy;
        if (!network.IsConnected)
        {
            DrawConnectionForm();
        }
        else
        {
            DrawConnectedControls();
        }
        GUI.enabled = true;

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawConnectionForm()
    {
        GUILayout.Space(6f);
        GUILayout.Label("Server Profile");
        scheme = DrawTextField("Scheme", scheme);
        host = DrawTextField("Host", host);
        port = DrawTextField("Port", port);
        serverKey = DrawTextField("Server Key", serverKey);
        displayName = DrawTextField("Display Name", displayName);
        profileId = DrawTextField("Test Profile", profileId);
        GUILayout.Label("Use profile A and B to run two clients on one PC.");

        if (GUILayout.Button("CONNECT", GUILayout.Height(34f)))
        {
            Run(async () =>
            {
                if (!int.TryParse(port, out int parsedPort))
                {
                    throw new InvalidOperationException("Port must be a number.");
                }
                network.ConfigureServer(scheme, host, parsedPort, serverKey);
                await network.ConnectAsync(displayName, profileId);
            });
        }
    }

    private void DrawConnectedControls()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("DISCONNECT"))
        {
            Run(network.DisconnectAsync);
        }
        if (GUILayout.Button("COPY MATCH ID") && network.IsInMatch)
        {
            GUIUtility.systemCopyBuffer = network.MatchId;
        }
        GUILayout.EndHorizontal();

        if (!network.IsInMatch && !network.IsMatchmaking)
        {
            DrawRoomSettings();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CREATE ROOM", GUILayout.Height(34f)))
            {
                Run(() => network.CreateRoomAsync(editableSettings));
            }
            if (GUILayout.Button("QUICK MATCH 2P", GUILayout.Height(34f)))
            {
                Run(() => network.StartQuickMatchAsync(2));
            }
            GUILayout.EndHorizontal();

            joinMatchId = DrawTextField("Match ID", joinMatchId);
            if (GUILayout.Button("JOIN ROOM", GUILayout.Height(34f)))
            {
                Run(() => network.JoinRoomAsync(joinMatchId));
            }
            return;
        }

        if (network.IsMatchmaking)
        {
            if (GUILayout.Button("CANCEL MATCHMAKING"))
            {
                Run(network.CancelQuickMatchAsync);
            }
            return;
        }

        DrawLobby();
    }

    private void DrawLobby()
    {
        ObjectHeadLobbyState lobby = network.LobbyState;
        GUILayout.Space(8f);
        GUILayout.Label("Match ID: " + network.MatchId);
        GUILayout.Label(network.IsHost ? "Role: HOST" : "Role: CLIENT");

        if (lobby == null)
        {
            GUILayout.Label("Waiting for lobby state...");
            return;
        }

        GUILayout.Label($"Players ({lobby.players.Length}/{lobby.settings.maxPlayers})");
        foreach (ObjectHeadLobbyPlayer player in lobby.players)
        {
            string hostMark = player.userId == lobby.hostUserId ? " HOST" : string.Empty;
            GUILayout.Label($"P{player.playerIndex}  {player.username}  {(player.ready ? "READY" : "NOT READY")}{hostMark}");
        }

        if (network.IsHost)
        {
            editableSettings.mapSelectionMode = lobby.settings.mapSelectionMode;
            editableSettings.fixedMapId = lobby.settings.fixedMapId;
            DrawRoomSettings();
            if (GUILayout.Button("APPLY ROOM SETTINGS"))
            {
                Run(() => network.UpdateRoomSettingsAsync(editableSettings));
            }
        }

        ObjectHeadLobbyPlayer localPlayer = Array.Find(lobby.players, player => player.userId == network.LocalUserId);
        bool localReady = localPlayer != null && localPlayer.ready;
        if (GUILayout.Button(localReady ? "CANCEL READY" : "READY", GUILayout.Height(34f)))
        {
            Run(() => network.SetReadyAsync(!localReady));
        }

        if (network.IsHost)
        {
            bool canStart = network.CanStartMatch(out string reason);
            GUI.enabled = !busy && canStart;
            if (GUILayout.Button("START GAME", GUILayout.Height(40f)))
            {
                Run(network.StartGameAsync);
            }
            GUI.enabled = !busy;
            if (!canStart)
            {
                GUILayout.Label(reason);
            }
        }
    }

    private void DrawRoomSettings()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Map Selection");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(editableSettings.mapSelectionMode == ObjectHeadMapSelectionMode.Fixed ? "[ Fixed ]" : "Fixed"))
        {
            editableSettings.mapSelectionMode = ObjectHeadMapSelectionMode.Fixed;
        }
        if (GUILayout.Button(editableSettings.mapSelectionMode == ObjectHeadMapSelectionMode.Random ? "[ Random ]" : "Random"))
        {
            editableSettings.mapSelectionMode = ObjectHeadMapSelectionMode.Random;
        }
        GUILayout.EndHorizontal();

        editableSettings.fixedMapId = DrawTextField("Map ID", editableSettings.fixedMapId);
    }

    private static string DrawTextField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(105f));
        string result = GUILayout.TextField(value ?? string.Empty);
        GUILayout.EndHorizontal();
        return result;
    }

    private async void Run(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        error = string.Empty;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            busy = false;
        }
    }
}

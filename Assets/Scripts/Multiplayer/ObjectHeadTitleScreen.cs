using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectHeadTitleScreen : MonoBehaviour
{
    private const string LanguagePreferenceKey = "ObjectHead.Language";
    private const string DisplayNamePreferenceKey = "ObjectHead.DisplayName";

    [Header("Data")]
    public ObjectHeadLocalizationTable localization;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;

    [Header("Main menu")]
    public InputField nicknameInput;
    public InputField roomCodeInput;
    public Text roomSizeValueText;
    public Button koreanButton;
    public Button englishButton;
    public Button roomSizeMinusButton;
    public Button roomSizePlusButton;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button quickMatch2Button;
    public Button quickMatch3Button;
    public Button quickMatch4Button;
    public Button cancelMatchmakingButton;

    [Header("Lobby")]
    public Text lobbyTitleText;
    public Text lobbyRoomCodeText;
    public Text lobbyPlayerListText;
    public Text lobbyCapacityText;
    public Text lobbyMapModeText;
    public Button copyRoomCodeButton;
    public Button lobbySizeMinusButton;
    public Button lobbySizePlusButton;
    public Button toggleMapModeButton;
    public Button applySettingsButton;
    public Button readyButton;
    public Text readyButtonText;
    public Button startGameButton;
    public Button leaveRoomButton;

    [Header("Common")]
    public Text statusText;

    private ObjectHeadNetworkManager network;
    private ObjectHeadLanguage language = ObjectHeadLanguage.Korean;
    private Font runtimeFont;
    private bool busy;
    private int roomSize = 2;
    private int requestedMatchSize;
    private ObjectHeadMapSelectionMode selectedMapMode = ObjectHeadMapSelectionMode.Fixed;
    private string statusKey = "status_offline";
    private string rawStatus;

    private void Awake()
    {
        network = ObjectHeadNetworkManager.Instance;
        localization ??= ObjectHeadLocalizationTable.Load();
        language = (ObjectHeadLanguage)Mathf.Clamp(
            PlayerPrefs.GetInt(LanguagePreferenceKey, (int)ObjectHeadLanguage.Korean),
            (int)ObjectHeadLanguage.Korean,
            (int)ObjectHeadLanguage.English);

        if (nicknameInput != null)
        {
            nicknameInput.text = PlayerPrefs.GetString(DisplayNamePreferenceKey, "Player");
        }

        ApplyRuntimeFont();
        BindButtons();
    }

    private void OnEnable()
    {
        if (network == null)
        {
            network = ObjectHeadNetworkManager.Instance;
        }

        if (network != null)
        {
            network.StateChanged += HandleNetworkChanged;
            network.LobbyChanged += HandleLobbyChanged;
            network.MatchStarting += HandleMatchStarting;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (network != null)
        {
            network.StateChanged -= HandleNetworkChanged;
            network.LobbyChanged -= HandleLobbyChanged;
            network.MatchStarting -= HandleMatchStarting;
        }
    }

    private void BindButtons()
    {
        koreanButton?.onClick.AddListener(() => SetLanguage(ObjectHeadLanguage.Korean));
        englishButton?.onClick.AddListener(() => SetLanguage(ObjectHeadLanguage.English));
        roomSizeMinusButton?.onClick.AddListener(() => ChangeRoomSize(-1));
        roomSizePlusButton?.onClick.AddListener(() => ChangeRoomSize(1));
        lobbySizeMinusButton?.onClick.AddListener(() => ChangeRoomSize(-1));
        lobbySizePlusButton?.onClick.AddListener(() => ChangeRoomSize(1));
        createRoomButton?.onClick.AddListener(CreateRoom);
        joinRoomButton?.onClick.AddListener(JoinRoom);
        quickMatch2Button?.onClick.AddListener(() => StartQuickMatch(2));
        quickMatch3Button?.onClick.AddListener(() => StartQuickMatch(3));
        quickMatch4Button?.onClick.AddListener(() => StartQuickMatch(4));
        cancelMatchmakingButton?.onClick.AddListener(CancelMatchmaking);
        copyRoomCodeButton?.onClick.AddListener(CopyRoomCode);
        toggleMapModeButton?.onClick.AddListener(ToggleMapMode);
        applySettingsButton?.onClick.AddListener(ApplyRoomSettings);
        readyButton?.onClick.AddListener(ToggleReady);
        startGameButton?.onClick.AddListener(StartGame);
        leaveRoomButton?.onClick.AddListener(LeaveRoom);
    }

    private void SetLanguage(ObjectHeadLanguage nextLanguage)
    {
        language = nextLanguage;
        PlayerPrefs.SetInt(LanguagePreferenceKey, (int)language);
        PlayerPrefs.Save();
        RefreshAll();
    }

    private void ChangeRoomSize(int delta)
    {
        roomSize = Mathf.Clamp(roomSize + delta, 2, 4);
        RefreshAll();
    }

    private void CreateRoom()
    {
        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.CreateRoomAsync(BuildRoomSettings());
            statusKey = "status_room_created";
        }, "status_connecting");
    }

    private void JoinRoom()
    {
        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("status_room_code_required");
            return;
        }

        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.JoinRoomAsync(code);
            statusKey = "status_joined_room";
        }, "status_connecting");
    }

    private void StartQuickMatch(int playerCount)
    {
        requestedMatchSize = playerCount;
        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.StartQuickMatchAsync(playerCount);
            statusKey = "status_matching";
        }, "status_connecting");
    }

    private void CancelMatchmaking()
    {
        Run(async () =>
        {
            await network.CancelQuickMatchAsync();
            requestedMatchSize = 0;
            statusKey = "status_connected";
        }, "status_cancelling");
    }

    private void CopyRoomCode()
    {
        if (network != null && network.IsInMatch)
        {
            GUIUtility.systemCopyBuffer = network.RoomCode;
            SetStatus("status_room_code_copied");
        }
    }

    private void ToggleMapMode()
    {
        selectedMapMode = selectedMapMode == ObjectHeadMapSelectionMode.Fixed
            ? ObjectHeadMapSelectionMode.Random
            : ObjectHeadMapSelectionMode.Fixed;
        RefreshAll();
    }

    private void ApplyRoomSettings()
    {
        Run(() => network.UpdateRoomSettingsAsync(BuildRoomSettings()), "status_applying_settings");
    }

    private void ToggleReady()
    {
        ObjectHeadLobbyPlayer localPlayer = FindLocalPlayer();
        bool nextReady = localPlayer == null || !localPlayer.ready;
        Run(() => network.SetReadyAsync(nextReady), nextReady ? "status_readying" : "status_unreadying");
    }

    private void StartGame()
    {
        Run(network.StartGameAsync, "status_starting_game");
    }

    private void LeaveRoom()
    {
        Run(async () =>
        {
            await network.DisconnectAsync();
            statusKey = "status_offline";
        }, "status_leaving_room");
    }

    private async Task EnsureConnectedAsync()
    {
        if (network.IsConnected)
        {
            return;
        }

        string displayName = nicknameInput != null ? nicknameInput.text.Trim() : "Player";
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Player";
        }

        PlayerPrefs.SetString(DisplayNamePreferenceKey, displayName);
        PlayerPrefs.Save();
        await network.ConnectAsync(displayName, ResolveLocalProfileId());
    }

    private ObjectHeadRoomSettings BuildRoomSettings()
    {
        ObjectHeadRoomSettings settings = network.Config.DefaultRoomSettings;
        settings.minPlayers = roomSize;
        settings.maxPlayers = roomSize;
        settings.mapSelectionMode = selectedMapMode;
        settings.fixedMapId = network.Config.FallbackMapId;
        return settings;
    }

    private async void Run(Func<Task> operation, string pendingStatusKey)
    {
        if (busy || operation == null)
        {
            return;
        }

        busy = true;
        rawStatus = string.Empty;
        statusKey = pendingStatusKey;
        RefreshAll();
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            statusKey = "status_error";
            rawStatus = exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            busy = false;
            RefreshAll();
        }
    }

    private void HandleNetworkChanged()
    {
        if (!busy && network.IsConnected && !network.IsInMatch && !network.IsMatchmaking)
        {
            statusKey = "status_connected";
        }

        RefreshAll();
    }

    private void HandleLobbyChanged(ObjectHeadLobbyState lobby)
    {
        if (lobby != null)
        {
            roomSize = Mathf.Clamp(lobby.settings.maxPlayers, 2, 4);
            selectedMapMode = lobby.settings.mapSelectionMode;
            statusKey = "status_in_lobby";
        }

        RefreshAll();
    }

    private void HandleMatchStarting(GameStartData unused)
    {
        SetStatus("status_starting_game");
    }

    private void RefreshAll()
    {
        if (localization == null)
        {
            return;
        }

        foreach (ObjectHeadLocalizedLabel label in GetComponentsInChildren<ObjectHeadLocalizedLabel>(true))
        {
            label.Apply(localization, language);
        }

        ObjectHeadLobbyState lobby = network != null ? network.LobbyState : null;
        bool inMatch = network != null && network.IsInMatch;
        mainMenuPanel?.SetActive(!inMatch);
        lobbyPanel?.SetActive(inMatch);
        cancelMatchmakingButton?.gameObject.SetActive(network != null && network.IsMatchmaking);

        if (roomSizeValueText != null)
        {
            roomSizeValueText.text = string.Format(L("room_size_value"), roomSize);
        }

        if (statusText != null)
        {
            string translated = statusKey == "status_matching"
                ? string.Format(L(statusKey), requestedMatchSize)
                : L(statusKey);
            statusText.text = string.IsNullOrWhiteSpace(rawStatus)
                ? translated
                : translated + "\n" + rawStatus;
        }

        if (!inMatch)
        {
            SetMainButtonsInteractable(!busy && (network == null || !network.IsMatchmaking));
            return;
        }

        string roomCode = network.RoomCode;
        if (lobbyTitleText != null)
        {
            lobbyTitleText.text = network.IsHost ? L("lobby_host_title") : L("lobby_guest_title");
        }
        if (lobbyRoomCodeText != null)
        {
            lobbyRoomCodeText.text = string.Format(L("room_code_value"), roomCode);
        }
        if (lobbyCapacityText != null)
        {
            int playerCount = lobby != null && lobby.players != null ? lobby.players.Length : 0;
            lobbyCapacityText.text = string.Format(L("player_count_value"), playerCount, roomSize);
        }
        if (lobbyMapModeText != null)
        {
            lobbyMapModeText.text = L(selectedMapMode == ObjectHeadMapSelectionMode.Fixed ? "map_fixed" : "map_random");
        }
        if (lobbyPlayerListText != null)
        {
            lobbyPlayerListText.text = BuildPlayerList(lobby);
        }

        ObjectHeadLobbyPlayer localPlayer = FindLocalPlayer();
        bool localReady = localPlayer != null && localPlayer.ready;
        if (readyButtonText != null)
        {
            readyButtonText.text = L(localReady ? "cancel_ready" : "ready");
        }

        bool isHost = network.IsHost;
        lobbySizeMinusButton.interactable = !busy && isHost;
        lobbySizePlusButton.interactable = !busy && isHost;
        toggleMapModeButton.interactable = !busy && isHost;
        applySettingsButton.interactable = !busy && isHost;
        readyButton.interactable = !busy && lobby != null;
        leaveRoomButton.interactable = !busy;
        startGameButton.gameObject.SetActive(isHost);
        startGameButton.interactable = !busy && network.CanStartMatch(out _);
    }

    private string BuildPlayerList(ObjectHeadLobbyState lobby)
    {
        if (lobby == null || lobby.players == null || lobby.players.Length == 0)
        {
            return L("waiting_for_players");
        }

        return string.Join("\n", lobby.players
            .OrderBy(player => player.playerIndex)
            .Select(player =>
            {
                string hostMark = player.userId == lobby.hostUserId ? "  " + L("host_mark") : string.Empty;
                string readyMark = L(player.ready ? "ready_state" : "not_ready_state");
                return $"P{player.playerIndex}   {player.username}   {readyMark}{hostMark}";
            }));
    }

    private ObjectHeadLobbyPlayer FindLocalPlayer()
    {
        ObjectHeadLobbyState lobby = network != null ? network.LobbyState : null;
        return lobby?.players?.FirstOrDefault(player => player.userId == network.LocalUserId);
    }

    private void SetMainButtonsInteractable(bool interactable)
    {
        createRoomButton.interactable = interactable;
        joinRoomButton.interactable = interactable;
        quickMatch2Button.interactable = interactable;
        quickMatch3Button.interactable = interactable;
        quickMatch4Button.interactable = interactable;
        roomSizeMinusButton.interactable = interactable;
        roomSizePlusButton.interactable = interactable;
    }

    private void SetStatus(string key)
    {
        statusKey = key;
        rawStatus = string.Empty;
        RefreshAll();
    }

    private string L(string key)
    {
        return localization != null ? localization.Get(key, language) : key;
    }

    private static string ResolveLocalProfileId()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-objectHeadProfile", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return "device-default";
    }

    private void ApplyRuntimeFont()
    {
        runtimeFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" },
            24);
        if (runtimeFont == null)
        {
            return;
        }

        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            text.font = runtimeFont;
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadTitleScreen : MonoBehaviour
{
    private const string LanguagePreferenceKey = "ObjectHead.Language";
    private const string DisplayNamePreferenceKey = "ObjectHead.DisplayName";

    [Header("Data")]
    public ObjectHeadLocalizationTable localization;
    public ObjectHeadContent content;
    public Font uiFont;

    [Header("Character selection and map")]
    public Button[] slotButtons;
    public Image[] slotPortraits;
    public Text[] slotLabels;
    public Button[] characterButtons;
    public Text selectionHint;
    public Text characterDescription;
    public Button previousMapButton;
    public Button nextMapButton;
    public Image mapPreview;
    public Text mapName;
    public Button localPlayButton;
    public Button helpButton;
    public GameObject helpPanel;
    public Button closeHelpButton;
    public Button quitButton;
    [Header("Connection progress (authored in the title prefab)")]
    public GameObject loadingOverlay;
    public Text loadingTitle;
    public Text loadingDetail;
    public RectTransform loadingSpinner;
    private string pendingOperation;
    private float busySince;
    private bool changingScene;
    private bool applyingRoomSettings;
    public Text localPlayerLabel;
    public ObjectHeadCharacterBrowser characterBrowser;
    public ObjectHeadFrontEnd frontEnd;
    public bool InLobby => localLobby || (network != null && network.IsInMatch);
    public bool HasMenuStatus => statusKey != "status_offline" && statusKey != "status_connected";
    public ObjectHeadLanguage Language => language;
    public event Action LanguageChanged;
    public string Translate(string key) => L(key);

    private ObjectHeadCharacterKind[] selection = Array.Empty<ObjectHeadCharacterKind>();
    private int selectedSlot;
    private int selectedMap;
    private bool localLobby;
    private int localSelectionPlayer;
    private ObjectHeadPlayerAssignment[] localPlayers;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    [Tooltip("Title decorations hidden while the room / team setup screen is open.")]
    public GameObject[] menuOnlyDecorations;

    [Header("Main menu")]
    public InputField nicknameInput;
    public InputField roomCodeInput;
    public ObjectHeadRoomAccessPanel roomAccess;
    public Text roomSizeValueText;
    public Button koreanButton;
    public Button englishButton;
    public Button roomSizeMinusButton;
    public Button roomSizePlusButton;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button quickMatch2Button;
    public Button quickMatch3Button; // Legacy serialized field, not shown by 0916 scenes.
    public Button quickMatchTeamsButton;
    public Text lobbyModeText;
    private ObjectHeadMatchMode selectedMode;
    public Button quickMatch4Button;
    public Button cancelMatchmakingButton;

    [Header("Lobby")]
    public Text lobbyTitleText;
    public Text lobbyRoomCodeText;
    public Text lobbyPlayerListText;
    public Text[] lobbyPlayerRows;
    public Image[] lobbyPlayerTeamMarks;
    public Color[] lobbyTeamColors;
    public Text lobbyCapacityText;
    public Text lobbyMapModeText;
    public Button copyRoomCodeButton;
    public Button lobbySizeMinusButton;
    public Button lobbySizePlusButton;
    public Button toggleMapModeButton;
    public Button applySettingsButton;
    public Button readyButton;
    public Text readyButtonText;
    public Button aiRosterModeButton;
    public Text aiRosterModeText;
    public Button aiDifficultyButton;
    public Text aiDifficultyText;
    public Button onlineAIAddButton;
    public Button onlineAIRemoveButton;
    public Button onlineAIDifficultyButton;
    public Text onlineAIDifficultyText;
    public Text lobbyStartHint;
    public Button editLocalSquadButton;
    public Text editLocalSquadText;
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
    private bool randomizeAiCharacters = true;
    private ObjectHeadAIDifficulty selectedAiDifficulty = ObjectHeadAIDifficulty.Normal;
    private readonly System.Collections.Generic.Dictionary<int,ObjectHeadCharacterKind[]> manualAiRosters = new System.Collections.Generic.Dictionary<int,ObjectHeadCharacterKind[]>();
    private string statusKey = "status_offline";
    private string rawStatus;

    private void Awake()
    {
        network = ObjectHeadNetworkManager.Instance;
        localization ??= ObjectHeadLocalizationTable.Load();
        content ??= ObjectHeadContent.Load();
        uiFont ??= content != null ? content.uiFont : null;
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
        quickMatch2Button?.onClick.AddListener(() => StartQuickMatch(ObjectHeadMatchMode.Duel));
        quickMatchTeamsButton?.onClick.AddListener(() => StartQuickMatch(ObjectHeadMatchMode.Teams));
        quickMatch4Button?.onClick.AddListener(() => StartQuickMatch(ObjectHeadMatchMode.FreeForAll));
        cancelMatchmakingButton?.onClick.AddListener(CancelMatchmaking);
        copyRoomCodeButton?.onClick.AddListener(CopyRoomCode);
        toggleMapModeButton?.onClick.AddListener(ToggleMapMode);
        applySettingsButton?.onClick.AddListener(ApplyRoomSettings);
        readyButton?.onClick.AddListener(ToggleReady);
        aiRosterModeButton?.onClick.AddListener(ToggleAIRosterMode);
        aiDifficultyButton?.onClick.AddListener(CycleAIDifficulty);
        onlineAIAddButton?.onClick.AddListener(() => Run(network.AddAIAsync, "status_adding_ai"));
        onlineAIRemoveButton?.onClick.AddListener(() => Run(network.RemoveAIAsync, "status_removing_ai"));
        onlineAIDifficultyButton?.onClick.AddListener(CycleAIDifficulty);
        editLocalSquadButton?.onClick.AddListener(ChangeLocalSelectionPlayer);
        startGameButton?.onClick.AddListener(StartGame);
        leaveRoomButton?.onClick.AddListener(LeaveRoom);
        localPlayButton?.onClick.AddListener(EnterLocalLobby);
        helpButton?.onClick.AddListener(() => helpPanel.SetActive(true));
        closeHelpButton?.onClick.AddListener(() => helpPanel.SetActive(false));
        quitButton?.onClick.AddListener(QuitGame);
        previousMapButton?.onClick.AddListener(() => ChangeMap(-1));
        nextMapButton?.onClick.AddListener(() => ChangeMap(1));
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i;
            slotButtons[i].onClick.AddListener(() => { selectedSlot = slot; RefreshSelection(); });
        }
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int index = i;
            characterButtons[i].onClick.AddListener(() => SelectCharacter(content.characters[index].kind));
        }
    }

    public void SetLanguage(ObjectHeadLanguage nextLanguage)
    {
        language = nextLanguage;
        PlayerPrefs.SetInt(LanguagePreferenceKey, (int)language);
        PlayerPrefs.Save();
        RefreshAll();
        LanguageChanged?.Invoke();
    }

    private void ChangeRoomSize(int delta)
    {
        if (busy || (network != null && network.IsInMatch && !network.IsHost)) return;
        if (!TryFindNextMode(delta, out var nextMode)) return;
        selectedMode = nextMode;
        roomSize = content.Mode(nextMode).players;
        if (localLobby) { EnterLocalLobby(); return; }
        CommitMapSettings();
    }

    private bool TryFindNextMode(int direction, out ObjectHeadMatchMode nextMode)
    {
        nextMode = selectedMode;
        if (content?.modes == null || content.modes.Length < 2) return false;
        int current = Array.FindIndex(content.modes, mode => mode.mode == selectedMode);
        int step = direction < 0 ? -1 : 1;
        var lobby = !localLobby && network != null && network.IsInMatch ? network.LobbyState : null;
        for (int offset = 1; offset < content.modes.Length; offset++)
        {
            var candidate = content.modes[(current + step * offset + content.modes.Length) % content.modes.Length];
            // Skip a smaller mode before changing the displayed state or sending a request.
            if (lobby != null && !ObjectHeadLobbyRules.CanChangeMode(lobby, candidate.mode, out _)) continue;
            nextMode = candidate.mode;
            return true;
        }
        return false;
    }

    private void CreateRoom()
    {
        bool isPrivate = roomAccess != null && roomAccess.IsPrivate;
        string password = roomAccess?.CreatePassword;
        var settings = BuildRoomSettings();
        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.CreateRoomAsync(settings,isPrivate,password);
            roomAccess?.ClearPasswords();
            statusKey = "status_room_created";
        }, "creating_room");
    }

    public async Task JoinDiscoveredRoomAsync(string code)
    {
        if (busy) return;
        await RunAsync(async () => {
            await EnsureConnectedAsync();
            await network.JoinRoomAsync(code);
            statusKey = "status_joined_room";
        }, "joining_room");
    }

    private void JoinRoom()
    {
        string password = roomAccess?.JoinPassword;
        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("status_room_code_required");
            return;
        }

        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.JoinRoomAsync(code,password);
            roomAccess?.ClearPasswords();
            statusKey = "status_joined_room";
        }, "joining_room");
    }

    private void StartQuickMatch(ObjectHeadMatchMode mode)
    {
        requestedMatchSize = content.Mode(mode).players;
        Run(async () =>
        {
            await EnsureConnectedAsync();
            await network.StartQuickMatchAsync(mode);
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
        CommitMapSettings();
    }

    private void CommitMapSettings()
    {
        if (localLobby || network == null || !network.IsInMatch) { RefreshAll(); return; }
        if (!network.IsHost || busy) return;
        var settings = BuildRoomSettings();
        Run(async () => {
            applyingRoomSettings = true;
            try { await network.UpdateRoomSettingsAsync(settings); }
            finally {
                applyingRoomSettings = false;
                if (this != null) HandleLobbyChanged(network.LobbyState);
            }
        }, "status_applying_settings");
    }

    private void ApplyRoomSettings()
    {
        if (localLobby) { EnterLocalLobby(); return; }
        CommitMapSettings();
    }

    private void ToggleReady()
    {
        if (localLobby) { ConfirmLocalTeam(); return; }
        if (selection.Length == 0) return;
        ObjectHeadLobbyPlayer localPlayer = FindLocalPlayer();
        bool nextReady = localPlayer == null || !localPlayer.ready;
        Run(async () =>
        {
            if (nextReady && (localPlayer == null || !localPlayer.characters.SequenceEqual(selection)))
            {
                await network.SetSelectionAsync(selection);
                DateTime deadline = DateTime.UtcNow.AddSeconds(8);
                while (DateTime.UtcNow < deadline && !(FindLocalPlayer()?.characters.SequenceEqual(selection) ?? false)) await Task.Delay(50);
                if (!(FindLocalPlayer()?.characters.SequenceEqual(selection) ?? false)) throw new TimeoutException("connection_help");
            }
            await network.SetReadyAsync(nextReady);
        }, nextReady ? "status_readying" : "status_unreadying");
    }

    private void StartGame()
    {
        if (localLobby) { StartLocalGame(); return; }
        Run(network.StartGameAsync, "status_starting_game");
    }

    private void LeaveRoom()
    {
        ObjectHeadTraining.Pending = false;
        if (localLobby) { localLobby = false; RefreshAll(); return; }
        Run(async () =>
        {
            await network.DisconnectAsync();
            statusKey = "status_offline";
        }, "status_leaving_room");
    }

    public async Task EnsureConnectedAsync()
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
        settings.mode = selectedMode;
        settings.minPlayers = 2;
        settings.maxPlayers = roomSize;
        settings.mapSelectionMode = selectedMapMode;
        settings.fixedMapId = content.maps[selectedMap].id;
        settings.randomMapPool = content.maps.Select(m => m.id).ToArray();
        return settings;
    }

    private async void Run(Func<Task> operation, string pendingStatusKey)
    {
        await RunAsync(operation, pendingStatusKey);
    }

    private async Task RunAsync(Func<Task> operation, string pendingStatusKey)
    {
        if (busy || operation == null)
        {
            return;
        }

        busy = true;
        pendingOperation = pendingStatusKey;
        busySince = Time.unscaledTime;
        rawStatus = string.Empty;
        statusKey = pendingStatusKey;
        try
        {
            // Rendering may fail before an async operation even starts. Keep it
            // inside the guarded scope so busy/loading is always released.
            RefreshAll();
            await operation();
        }
        catch (Exception exception)
        {
            if (!localLobby && network != null && network.IsInMatch)
            {
                applyingRoomSettings = false;
                RestoreLobbySelection(network.LobbyState);
            }
            statusKey = "status_error";
            string errorKey=ObjectHeadRoomAccessPanel.ErrorKey(exception.Message);
            rawStatus = L(errorKey) != errorKey ? L(errorKey) : L("connection_help");
            // Admission errors are expected. Never log a request containing a password.
            Debug.LogWarning("[Title] Operation failed: "+errorKey);
        }
        finally
        {
            busy = false;
            if (this != null) RefreshAll();
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
        if (lobby != null && !applyingRoomSettings)
        {
            RestoreLobbySelection(lobby);
            statusKey = "status_in_lobby";
        }

        RefreshAll();
    }

    private void RestoreLobbySelection(ObjectHeadLobbyState lobby)
    {
        if (lobby?.settings == null || content == null) return;
        var mode = content.Mode(lobby.settings.mode);
        if (mode == null) return;
        selectedMode = lobby.settings.mode;
        roomSize = mode.players;
        selectedMapMode = lobby.settings.mapSelectionMode;
        int mapIndex = Array.FindIndex(content.maps, map => map.id == lobby.settings.fixedMapId);
        if (mapIndex >= 0) selectedMap = mapIndex;
        var aiPlayer = lobby.players?.FirstOrDefault(player => player != null && player.isAi);
        if (aiPlayer != null) selectedAiDifficulty = aiPlayer.aiDifficulty;
    }

    private void HandleMatchStarting(GameStartData unused)
    {
        changingScene = true;
        pendingOperation = "status_starting_game";
        busySince = Time.unscaledTime;
        SetStatus("status_starting_game");
    }

    private void Update()
    {
        bool loading = busy || changingScene;
        if (loadingOverlay == null) return;
        if (loadingOverlay.activeSelf != loading) loadingOverlay.SetActive(loading);
        if (!loading) return;
        loadingOverlay.transform.SetAsLastSibling();
        bool ko = language == ObjectHeadLanguage.Korean;
        string label = pendingOperation == "creating_room" ? (ko ? "방을 만드는 중" : "Creating room")
            : pendingOperation == "joining_room" ? (ko ? "방에 참가하는 중" : "Joining room")
            : pendingOperation == "quitting_game" ? (ko ? "연결을 정리하는 중" : "Disconnecting")
            : L(pendingOperation ?? "status_connecting");
        loadingTitle.text = label + new string('.', 1 + (int)(Time.unscaledTime * 2) % 3);
        loadingDetail.text = Time.unscaledTime - busySince > 7
            ? (ko ? "응답을 기다리고 있어요. 연결이 지연되면 안내해 드릴게요." : "Waiting for a response. Connection delays will be reported.")
            : (ko ? "잠시만 기다려 주세요." : "Please wait a moment.");
        if (loadingSpinner != null) loadingSpinner.localRotation = Quaternion.Euler(0,0,-Time.unscaledTime*150);
    }

    private void QuitGame()
    {
        Run(async () => {
            try { if (network != null) await network.DisconnectAsync(); }
            finally { Application.Quit(); }
        }, "quitting_game");
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

        // The English name is much longer than its Korean counterpart. Keep it
        // clear of the character illustration without shrinking the Korean logo.
        var gameTitle = transform.Find("GameTitle")?.GetComponent<Text>();
        if (gameTitle != null) gameTitle.fontSize = language == ObjectHeadLanguage.English ? 49 : 62;

        ObjectHeadLobbyState lobby = network != null ? network.LobbyState : null;
        bool inMatch = localLobby || (network != null && network.IsInMatch);
        mainMenuPanel?.SetActive(!inMatch);
        lobbyPanel?.SetActive(inMatch);
        if (menuOnlyDecorations != null)
            foreach (var decoration in menuOnlyDecorations)
                if (decoration != null) decoration.SetActive(!inMatch);
        frontEnd?.Sync(inMatch);
        cancelMatchmakingButton?.gameObject.SetActive(network != null && network.IsMatchmaking);

        if (roomSizeValueText != null)
        {
            roomSizeValueText.text = L(content.Mode(selectedMode).nameKey);
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

        EnsureSelection();
        RefreshSelection();
        if (lobbyModeText != null) lobbyModeText.text = L(content.Mode(selectedMode).nameKey);
        if (localLobby)
        {
            SetOnlineAIControls(false, null);
            if (lobbyStartHint != null) lobbyStartHint.gameObject.SetActive(false);
            bool aiBattle=localPlayers.Any(player=>player.isAi);
            lobbyTitleText.text = L("local_title");
            lobbyRoomCodeText.text = L("local_shared_screen");
            lobbyCapacityText.text = string.Format(L("player_count_value"), roomSize, roomSize);
            lobbyMapModeText.text = L(selectedMapMode == ObjectHeadMapSelectionMode.Fixed ? "map_fixed" : "map_random");
            var localRoster = localPlayers.OrderBy(p => selectedMode == ObjectHeadMatchMode.Teams ? p.allianceId : p.playerIndex)
                .ThenBy(p => p.playerIndex).Select(p => new LobbyRosterLine
                {
                    playerIndex = p.playerIndex, allianceId = p.allianceId,
                    name = p.username,
                    detail = p.isAi ? L("ai_difficulty_" + p.aiDifficulty.ToString().ToLowerInvariant())
                        : L(content.ValidSelection(p.characters, roomSize) ? "ready_state" : "not_ready_state"),
                    characters = p.characters
                }).ToArray();
            DisplayRoster(localRoster, selectedMode);
            readyButtonText.text = L("confirm_team");
            readyButton.interactable = !busy;
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = localPlayers.All(p => content.ValidSelection(p.characters, roomSize));
            copyRoomCodeButton.gameObject.SetActive(false);
            if(aiRosterModeButton!=null)aiRosterModeButton.gameObject.SetActive(aiBattle);
            if(aiDifficultyButton!=null)aiDifficultyButton.gameObject.SetActive(aiBattle);
            if(aiRosterModeText!=null)aiRosterModeText.text=L(randomizeAiCharacters?"ai_roster_random":"ai_roster_select");
            if(aiDifficultyText!=null)aiDifficultyText.text=L("ai_difficulty_"+selectedAiDifficulty.ToString().ToLowerInvariant());
            if(editLocalSquadButton!=null)editLocalSquadButton.gameObject.SetActive(!aiBattle || !randomizeAiCharacters);
            if(editLocalSquadText!=null)editLocalSquadText.text=string.Format(L("edit_local_squad"),localSelectionPlayer+1);
            SetHostControls(true);
            return;
        }
        if(aiRosterModeButton!=null)aiRosterModeButton.gameObject.SetActive(false);
        if(aiDifficultyButton!=null)aiDifficultyButton.gameObject.SetActive(false);
        if(editLocalSquadButton!=null)editLocalSquadButton.gameObject.SetActive(false);
        copyRoomCodeButton.gameObject.SetActive(true);

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
        DisplayRoster(BuildPlayerRoster(lobby), lobby?.settings?.mode ?? selectedMode);

        ObjectHeadLobbyPlayer localPlayer = FindLocalPlayer();
        bool localReady = localPlayer != null && localPlayer.ready;
        if (readyButtonText != null)
        {
            readyButtonText.text = L(localReady ? "cancel_ready" : "ready");
        }

        bool isHost = network.IsHost;
        SetHostControls(isHost);
        SetOnlineAIControls(isHost && network.UseEpicOnlineServices, lobby);
        readyButton.interactable = !busy && !network.IsRoomUpdating && localPlayer != null;
        leaveRoomButton.interactable = !busy;
        startGameButton.gameObject.SetActive(isHost);
        startGameButton.interactable = !busy && network.CanStartMatch(out _);
        RefreshLobbyHint(lobby);
    }

    private void SetOnlineAIControls(bool visible, ObjectHeadLobbyState lobby)
    {
        int count = lobby?.players?.Length ?? 0;
        bool hasAI = lobby?.players?.Any(player => player.isAi) ?? false;
        bool available = !busy && network != null && !network.IsGameStarting && !network.IsRoomUpdating;
        if (onlineAIAddButton != null)
        {
            onlineAIAddButton.gameObject.SetActive(visible);
            onlineAIAddButton.interactable = available && lobby != null && count < lobby.settings.maxPlayers;
        }
        if (onlineAIRemoveButton != null)
        {
            onlineAIRemoveButton.gameObject.SetActive(visible);
            onlineAIRemoveButton.interactable = available && hasAI;
        }
        if (onlineAIDifficultyButton != null)
        {
            onlineAIDifficultyButton.gameObject.SetActive(visible);
            onlineAIDifficultyButton.interactable = available && hasAI;
        }
        if (onlineAIDifficultyText != null)
            onlineAIDifficultyText.text = L("ai_difficulty_" + selectedAiDifficulty.ToString().ToLowerInvariant());
    }

    private void RefreshLobbyHint(ObjectHeadLobbyState lobby)
    {
        if (lobbyStartHint == null) return;
        lobbyStartHint.gameObject.SetActive(true);
        // Menu status is hidden in the room. Keep failures visible beside the start controls.
        if (statusKey == "status_error" && !string.IsNullOrWhiteSpace(rawStatus))
        {
            lobbyStartHint.text = rawStatus;
            return;
        }
        if (lobby?.players == null)
        {
            lobbyStartHint.text = L("waiting_for_players");
            return;
        }
        int count = lobby.players.Length;
        if (count == 3)
        {
            lobbyStartHint.text = L(lobby.settings.mode == ObjectHeadMatchMode.Teams ? "lobby_three_team_hint" : "lobby_three_ffa_hint");
            return;
        }
        lobbyStartHint.text = L(count < 2 ? "lobby_need_opponent" :
            lobby.players.Any(player => !player.ready) ? "lobby_ready_hint" : "lobby_can_start");
    }

    private sealed class LobbyRosterLine
    {
        public int playerIndex, allianceId;
        public string name, detail;
        public ObjectHeadCharacterKind[] characters;
    }

    private LobbyRosterLine[] BuildPlayerRoster(ObjectHeadLobbyState lobby)
    {
        if (lobby?.players == null) return Array.Empty<LobbyRosterLine>();
        // StateChanged can run before the mode-selection callback. Never map an
        // incoming 4-player roster through a stale local Duel selection.
        var mode = lobby.settings?.mode ?? ObjectHeadMatchMode.FreeForAll;
        return lobby.players.Where(p => p != null).OrderBy(p => mode == ObjectHeadMatchMode.Teams
                ? LobbyAlliance(content, mode, p.playerIndex) : p.playerIndex)
            .ThenBy(p => p.playerIndex).Select(p => new LobbyRosterLine
            {
                playerIndex = p.playerIndex,
                allianceId = LobbyAlliance(content, mode, p.playerIndex),
                name = p.username,
                detail = (p.isAi ? L("ai_difficulty_" + p.aiDifficulty.ToString().ToLowerInvariant())
                         : L(p.ready ? "ready_state" : "not_ready_state")) +
                         (p.userId == lobby.hostUserId ? " · " + L("host_mark") : ""),
                characters = p.characters
            }).ToArray();
    }

    public static int LobbyAlliance(ObjectHeadContent content, ObjectHeadMatchMode mode, int playerIndex)
    {
        var definition = content != null ? content.Mode(mode) : null;
        return definition?.alliances != null && playerIndex > 0 && playerIndex <= definition.alliances.Length
            ? definition.alliances[playerIndex - 1] : Mathf.Max(1, playerIndex);
    }

    private void DisplayRoster(LobbyRosterLine[] players, ObjectHeadMatchMode mode)
    {
        string[] lines = players.Select(p =>
        {
            string team = mode == ObjectHeadMatchMode.Teams
                ? string.Format(L("alliance_label"), p.allianceId) + " · " : "";
            string characters = p.characters != null && p.characters.Length > 0
                ? string.Join(" / ", p.characters.Select(k => L(content.Character(k).nameKey)))
                : string.Empty;
            return $"{team}P{p.playerIndex}  {p.name}\n{ComposeRosterDetail(p.detail, characters)}";
        }).ToArray();
        bool cards = lobbyPlayerRows != null && lobbyPlayerRows.Length > 0;
        if (lobbyPlayerListText != null)
        {
            lobbyPlayerListText.gameObject.SetActive(!cards);
            lobbyPlayerListText.text = lines.Length == 0 ? L("waiting_for_players") : string.Join("\n", lines);
        }
        if (!cards) return;
        for (int i = 0; i < lobbyPlayerRows.Length; i++)
        {
            Text row = lobbyPlayerRows[i];
            if (row == null) continue;
            bool visible = i < lines.Length;
            row.transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            row.text = lines[i];
            if (lobbyPlayerTeamMarks != null && i < lobbyPlayerTeamMarks.Length && lobbyPlayerTeamMarks[i] != null &&
                lobbyTeamColors != null && lobbyTeamColors.Length > 0)
                lobbyPlayerTeamMarks[i].color = lobbyTeamColors[(Mathf.Max(1, players[i].allianceId) - 1) % lobbyTeamColors.Length];
        }
    }

    public static string ComposeRosterDetail(string status, string characters)
    {
        // An unconfirmed squad has no head list yet. Show its readiness exactly once.
        if (string.IsNullOrWhiteSpace(characters)) return status ?? string.Empty;
        if (string.IsNullOrWhiteSpace(status)) return characters;
        return status + " · " + characters;
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
        if (quickMatchTeamsButton != null) quickMatchTeamsButton.interactable = interactable;
        if (localPlayButton != null) localPlayButton.interactable = interactable;
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
        runtimeFont = uiFont;
        if (runtimeFont == null)
        {
            return;
        }

        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            if (text.font == null) text.font = runtimeFont;
        }
    }

    private void SetHostControls(bool host)
    {
        bool editable = host && !busy && (localLobby || network == null || !network.IsRoomUpdating);
        previousMapButton.interactable = nextMapButton.interactable = editable;
        lobbySizeMinusButton.interactable = editable && TryFindNextMode(-1, out _);
        lobbySizePlusButton.interactable = editable && TryFindNextMode(1, out _);
        toggleMapModeButton.interactable = applySettingsButton.interactable = editable;
    }

    private void ChangeMap(int delta)
    {
        selectedMap = (selectedMap + delta + content.maps.Length) % content.maps.Length;
        selectedMapMode = ObjectHeadMapSelectionMode.Fixed;
        CommitMapSettings();
    }

    private void EnsureSelection()
    {
        int count = content.CharactersPerPlayer(roomSize);
        if (selection.Length != count)
        {
            selection = content.DefaultSelection(roomSize);
            selectedSlot = 0;
        }
    }

    public void SelectCharacter(ObjectHeadCharacterKind kind)
    {
        EnsureSelection();
        selection[selectedSlot] = kind;
        if (localLobby) localPlayers[localSelectionPlayer].characters = Array.Empty<ObjectHeadCharacterKind>();
        if (!localLobby) Run(() => network.SetSelectionAsync(selection), "saving_team");
        RefreshAll();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool active = i < selection.Length;
            slotButtons[i].gameObject.SetActive(active);
            if (!active) continue;
            ObjectHeadCharacterDefinition definition = content.Character(selection[i]);
            slotPortraits[i].sprite = definition.portrait;
            slotLabels[i].text = L(definition.nameKey);
            slotButtons[i].interactable = !busy;
            var outline = slotButtons[i].GetComponent<Outline>();
            if (outline != null) outline.enabled = i == selectedSlot;
        }
        foreach (Button button in characterButtons) button.interactable = !busy;
        characterBrowser?.Refresh(!busy);
        selectionHint.text = string.Format(L("selection_hint"), selection.Length);
        characterDescription.text = selection.Length > 0 ? L(content.Character(selection[selectedSlot]).descriptionKey) : string.Empty;
        mapPreview.sprite = content.maps[selectedMap].preview;
        mapName.text = L(content.maps[selectedMap].nameKey) + "\n" + L(content.maps[selectedMap].descriptionKey);
        localPlayerLabel.text = localLobby ? string.Format(L("select_player_team"), localSelectionPlayer + 1) : L("your_team");
    }

    private void EnterLocalLobby()
    {
        bool training=ObjectHeadTraining.Pending;
        if(!training)ObjectHeadTraining.Pending = false;
        localLobby = true;
        localSelectionPlayer = 0;
        manualAiRosters.Clear();
        var rosterRandom=new System.Random(Environment.TickCount^roomSize^((int)selectedMode<<8));
        localPlayers = Enumerable.Range(1, roomSize).Select(i => new ObjectHeadPlayerAssignment {
            playerIndex = i,
            allianceId = content.Mode(selectedMode).Alliance(i),
            username = i==1?"Player":"AI "+(i-1),
            isAi = !training && i>1,
            aiDifficulty = selectedAiDifficulty,
            characters = i==1?Array.Empty<ObjectHeadCharacterKind>():ObjectHeadAISettings.CreateRoster(content,roomSize,rosterRandom,randomizeAiCharacters)
        }).ToArray();
        selection = content.DefaultSelection(roomSize);
        statusKey = training?"training_instructions":"ai_instructions";
        RefreshAll();
    }

    private void ToggleAIRosterMode()
    {
        SaveLocalSelection();
        randomizeAiCharacters=!randomizeAiCharacters;
        localSelectionPlayer=0;
        selection=(ObjectHeadCharacterKind[])localPlayers[0].characters.Clone();
        selectedSlot=0;
        RefreshAIRosters();
    }

    private void CycleAIDifficulty()
    {
        if (!localLobby)
        {
            if (busy || network == null || !network.IsHost) return;
            var next = (ObjectHeadAIDifficulty)(((int)selectedAiDifficulty + 1) % Enum.GetValues(typeof(ObjectHeadAIDifficulty)).Length);
            Run(() => network.SetAIDifficultyAsync(next), "status_ai_difficulty");
            return;
        }
        selectedAiDifficulty=(ObjectHeadAIDifficulty)(((int)selectedAiDifficulty+1)%Enum.GetValues(typeof(ObjectHeadAIDifficulty)).Length);
        if(localPlayers!=null)foreach(var player in localPlayers.Where(player=>player.isAi))player.aiDifficulty=selectedAiDifficulty;
        RefreshAll();
    }

    private void RefreshAIRosters()
    {
        if(localPlayers==null)return;
        var rosterRandom=new System.Random(Environment.TickCount^roomSize^0xA17E);
        foreach(var player in localPlayers.Where(player=>player.isAi))
        {
            if(randomizeAiCharacters)
                player.characters=ObjectHeadAISettings.CreateRoster(content,roomSize,rosterRandom,true);
            else if(manualAiRosters.TryGetValue(player.playerIndex,out var saved) && content.ValidSelection(saved,roomSize))
                player.characters=(ObjectHeadCharacterKind[])saved.Clone();
            // On first manual selection keep the displayed roster as the starting point.
            else if(!content.ValidSelection(player.characters,roomSize))
                player.characters=content.DefaultSelection(roomSize);
        }
        RefreshAll();
    }

    private void SaveLocalSelection()
    {
        if(!localLobby || localPlayers==null || !content.ValidSelection(selection,roomSize))return;
        var player=localPlayers[localSelectionPlayer];
        player.characters=(ObjectHeadCharacterKind[])selection.Clone();
        if(player.isAi && !randomizeAiCharacters)manualAiRosters[player.playerIndex]=(ObjectHeadCharacterKind[])selection.Clone();
    }

    private void ChangeLocalSelectionPlayer()
    {
        if(!localLobby || localPlayers==null)return;
        SaveLocalSelection();
        if(!randomizeAiCharacters || !localPlayers.Any(player=>player.isAi))
            localSelectionPlayer=(localSelectionPlayer+1)%localPlayers.Length;
        else localSelectionPlayer=0;
        selection=(ObjectHeadCharacterKind[])localPlayers[localSelectionPlayer].characters.Clone();
        selectedSlot=0;
        EnsureSelection();
        RefreshAll();
    }

    public void StartTraining()
    {
        selectedMode=ObjectHeadMatchMode.Duel;roomSize=2;
        ObjectHeadTraining.Pending=true;
        EnterLocalLobby();
    }

    private void ConfirmLocalTeam()
    {
        ChangeLocalSelectionPlayer();
    }

    private void StartLocalGame()
    {
        if (localPlayers.Any(p => !content.ValidSelection(p.characters, roomSize))) return;
        int seed = new System.Random().Next(1, int.MaxValue);
        var map = content.maps[selectedMapMode == ObjectHeadMapSelectionMode.Random ? seed % content.maps.Length : selectedMap];
        GameStartData.Apply(new GameStartData { localMatch = true, mode = selectedMode, playerCount = roomSize, players = localPlayers,
            mapId = map.id, mapSeed = seed, characterSpawnSeed = seed, startingPlayerIndex = seed % roomSize + 1, rulesetVersion = ObjectHeadRoomSettings.CurrentRulesetVersion });
        SceneManager.LoadScene(map.sceneName);
    }
}

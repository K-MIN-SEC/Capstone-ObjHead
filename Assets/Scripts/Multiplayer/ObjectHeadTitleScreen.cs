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
    public Text localPlayerLabel;
    public ObjectHeadCharacterBrowser characterBrowser;
    public ObjectHeadFrontEnd frontEnd;
    public bool InLobby => localLobby || (network != null && network.IsInMatch);
    public ObjectHeadLanguage Language => language;
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
        startGameButton?.onClick.AddListener(StartGame);
        leaveRoomButton?.onClick.AddListener(LeaveRoom);
        localPlayButton?.onClick.AddListener(EnterLocalLobby);
        helpButton?.onClick.AddListener(() => helpPanel.SetActive(true));
        closeHelpButton?.onClick.AddListener(() => helpPanel.SetActive(false));
        quitButton?.onClick.AddListener(() => Application.Quit());
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
    }

    private void ChangeRoomSize(int delta)
    {
        int at = Array.FindIndex(content.modes, m => m.mode == selectedMode);
        var mode = content.modes[(at + delta + content.modes.Length) % content.modes.Length];
        selectedMode = mode.mode;
        roomSize = mode.players;
        if (localLobby) { EnterLocalLobby(); return; }
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

    public async Task JoinDiscoveredRoomAsync(string code)
    {
        await EnsureConnectedAsync();
        await network.JoinRoomAsync(code);
        statusKey = "status_joined_room";
        if(this != null) RefreshAll();
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
        RefreshAll();
    }

    private void ApplyRoomSettings()
    {
        if (localLobby) { EnterLocalLobby(); return; }
        Run(() => network.UpdateRoomSettingsAsync(BuildRoomSettings()), "status_applying_settings");
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
        settings.minPlayers = roomSize;
        settings.maxPlayers = roomSize;
        settings.mapSelectionMode = selectedMapMode;
        settings.fixedMapId = content.maps[selectedMap].id;
        settings.randomMapPool = content.maps.Select(m => m.id).ToArray();
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
            rawStatus = L(exception.Message) != exception.Message ? L(exception.Message) : L("connection_help");
            Debug.LogException(exception);
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
        if (lobby != null)
        {
            selectedMode = lobby.settings.mode;
            roomSize = content.Mode(selectedMode).players;
            selectedMapMode = lobby.settings.mapSelectionMode;
            int mapIndex = Array.FindIndex(content.maps, m => m.id == lobby.settings.fixedMapId);
            if (mapIndex >= 0) selectedMap = mapIndex;
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
        bool inMatch = localLobby || (network != null && network.IsInMatch);
        mainMenuPanel?.SetActive(!inMatch);
        lobbyPanel?.SetActive(inMatch);
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
            lobbyTitleText.text = L("local_title");
            lobbyRoomCodeText.text = L("local_shared_screen");
            lobbyCapacityText.text = string.Format(L("player_count_value"), roomSize, roomSize);
            lobbyMapModeText.text = L(selectedMapMode == ObjectHeadMapSelectionMode.Fixed ? "map_fixed" : "map_random");
            lobbyPlayerListText.text = string.Join("\n\n", localPlayers.Select((p, i) => $"P{i + 1}  " + L(p.characters.Length > 0 ? "ready_state" : "not_ready_state")));
            readyButtonText.text = L("confirm_team");
            readyButton.interactable = !busy;
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = localPlayers.All(p => content.ValidSelection(p.characters, roomSize));
            copyRoomCodeButton.gameObject.SetActive(false);
            SetHostControls(true);
            return;
        }
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
        SetHostControls(isHost);
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
                string team = string.Join(" / ", player.characters.Select(k => L(content.Character(k).nameKey)));
                string alliance = selectedMode == ObjectHeadMatchMode.Teams ? string.Format(L("alliance_label"), content.Mode(selectedMode).Alliance(player.playerIndex)) + "  " : "";
                return $"{alliance}P{player.playerIndex}  {player.username}   {readyMark}{hostMark}\n{team}\n";
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
        previousMapButton.interactable = nextMapButton.interactable = host && !busy;
        lobbySizeMinusButton.interactable = lobbySizePlusButton.interactable = host && !busy;
        toggleMapModeButton.interactable = applySettingsButton.interactable = host && !busy;
    }

    private void ChangeMap(int delta)
    {
        selectedMap = (selectedMap + delta + content.maps.Length) % content.maps.Length;
        RefreshAll();
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
        ObjectHeadTraining.Pending = false;
        localLobby = true;
        localSelectionPlayer = 0;
        localPlayers = Enumerable.Range(1, roomSize).Select(i => new ObjectHeadPlayerAssignment { playerIndex = i, allianceId = content.Mode(selectedMode).Alliance(i), username = "P" + i }).ToArray();
        selection = content.DefaultSelection(roomSize);
        statusKey = "local_instructions";
        RefreshAll();
    }

    public void StartTraining()
    {
        selectedMode=ObjectHeadMatchMode.Duel;roomSize=2;
        EnterLocalLobby();
        ObjectHeadTraining.Pending=true;
    }

    private void ConfirmLocalTeam()
    {
        localPlayers[localSelectionPlayer].characters = (ObjectHeadCharacterKind[])selection.Clone();
        localSelectionPlayer = (localSelectionPlayer + 1) % roomSize;
        selection = localPlayers[localSelectionPlayer].characters.Length > 0
            ? (ObjectHeadCharacterKind[])localPlayers[localSelectionPlayer].characters.Clone() : content.DefaultSelection(roomSize);
        RefreshAll();
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

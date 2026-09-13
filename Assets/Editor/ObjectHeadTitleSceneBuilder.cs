using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ObjectHeadTitleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ObjectHeadTitle.unity";
    private const string PrefabFolderPath = "Assets/Resources/UI";
    private const string PrefabPath = PrefabFolderPath + "/ObjectHeadTitleRoot.prefab";
    private const string LocalizationPath = "Assets/Resources/ObjectHeadLocalization.asset";
    private const string NetworkConfigPath = "Assets/Resources/ObjectHeadNetworkConfig.asset";

    private static readonly Color Background = new Color32(17, 30, 55, 255);
    private static readonly Color Panel = new Color32(28, 47, 78, 245);
    private static readonly Color Primary = new Color32(255, 180, 54, 255);
    private static readonly Color Secondary = new Color32(54, 104, 150, 255);
    private static readonly Color Input = new Color32(12, 25, 45, 255);
    private static readonly Color White = new Color32(242, 247, 255, 255);
    private static readonly Color Muted = new Color32(164, 185, 208, 255);

    [MenuItem("Object Head/Build Korean-English Title Scene")]
    public static void Build()
    {
        ObjectHeadLocalizationTable localization = CreateOrUpdateLocalization();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        GameObject titleRoot = new GameObject("ObjectHeadTitleRoot");
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(titleRoot.transform, false);
        eventSystemObject.GetComponent<EventSystem>().sendNavigationEvents = true;

        GameObject canvasObject = new GameObject(
            "ObjectHeadTitleCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(ObjectHeadTitleScreen));
        canvasObject.transform.SetParent(titleRoot.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage("Background", canvasObject.transform, Background);
        Stretch(background.rectTransform);

        Text title = CreateText("GameTitle", canvasObject.transform, "오브젝트 헤드 배틀", 64, TextAnchor.MiddleCenter, White);
        SetRect(title.rectTransform, new Vector2(0f, 430f), new Vector2(1100f, 100f));
        AddLocalization(title, "game_title");

        Text subtitle = CreateText("Subtitle", canvasObject.transform, "머리를 던지고, 지형을 바꾸고, 마지막까지 살아남으세요!", 24, TextAnchor.MiddleCenter, Muted);
        SetRect(subtitle.rectTransform, new Vector2(0f, 368f), new Vector2(1200f, 48f));
        AddLocalization(subtitle, "subtitle");

        Button koreanButton = CreateButton("KoreanButton", canvasObject.transform, "한국어", "language_korean", Secondary);
        SetRect(koreanButton.GetComponent<RectTransform>(), new Vector2(720f, 455f), new Vector2(130f, 48f));
        Button englishButton = CreateButton("EnglishButton", canvasObject.transform, "English", "language_english", Secondary);
        SetRect(englishButton.GetComponent<RectTransform>(), new Vector2(865f, 455f), new Vector2(130f, 48f));

        GameObject mainPanel = CreatePanel("MainMenuPanel", canvasObject.transform, new Vector2(0f, -5f), new Vector2(820f, 690f));
        GameObject lobbyPanel = CreatePanel("LobbyPanel", canvasObject.transform, new Vector2(0f, -5f), new Vector2(900f, 690f));
        lobbyPanel.SetActive(false);

        ObjectHeadTitleScreen controller = canvasObject.GetComponent<ObjectHeadTitleScreen>();
        controller.localization = localization;
        controller.mainMenuPanel = mainPanel;
        controller.lobbyPanel = lobbyPanel;
        controller.koreanButton = koreanButton;
        controller.englishButton = englishButton;

        BuildMainMenu(mainPanel.transform, controller);
        BuildLobby(lobbyPanel.transform, controller);

        Text status = CreateText("StatusText", canvasObject.transform, "서버 연결 전", 22, TextAnchor.MiddleCenter, Muted);
        SetRect(status.rectTransform, new Vector2(0f, -475f), new Vector2(1500f, 72f));
        controller.statusText = status;

        EnsureAssetFolder(PrefabFolderPath);
        PrefabUtility.SaveAsPrefabAsset(titleRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(titleRoot);

        new GameObject("ObjectHeadTitleBootstrap", typeof(ObjectHeadTitleBootstrap));
        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateNetworkConfig();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Object Head] Korean/English title scene and editable UI prefab generated.");
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    public static void BuildWindowsDemo()
    {
        Build();
        string outputPath = GetCommandLineValue("-objectHeadBuildPath");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("-objectHeadBuildPath is required.");
        }

        bool incremental = Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, "-objectHeadIncrementalBuild", StringComparison.OrdinalIgnoreCase));
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = incremental
                ? BuildOptions.StrictMode
                : BuildOptions.StrictMode | BuildOptions.CleanBuildCache
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        }

        Debug.Log($"[Object Head] Clean Windows build succeeded: {outputPath} ({report.summary.totalSize} bytes)");
    }

    private static string GetCommandLineValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    private static void BuildMainMenu(Transform parent, ObjectHeadTitleScreen controller)
    {
        Text nicknameLabel = CreateText("NicknameLabel", parent, "닉네임", 24, TextAnchor.MiddleLeft, White);
        SetRect(nicknameLabel.rectTransform, new Vector2(-275f, 278f), new Vector2(190f, 44f));
        AddLocalization(nicknameLabel, "nickname_label");
        controller.nicknameInput = CreateInput("NicknameInput", parent, "닉네임을 입력하세요", "nickname_placeholder");
        SetRect(controller.nicknameInput.GetComponent<RectTransform>(), new Vector2(90f, 278f), new Vector2(490f, 54f));

        Text roomSizeLabel = CreateText("RoomSizeLabel", parent, "방 인원", 24, TextAnchor.MiddleLeft, White);
        SetRect(roomSizeLabel.rectTransform, new Vector2(-275f, 198f), new Vector2(190f, 44f));
        AddLocalization(roomSizeLabel, "room_size_label");
        controller.roomSizeMinusButton = CreateButton("RoomSizeMinus", parent, "−", string.Empty, Secondary);
        SetRect(controller.roomSizeMinusButton.GetComponent<RectTransform>(), new Vector2(-10f, 198f), new Vector2(60f, 52f));
        controller.roomSizeValueText = CreateText("RoomSizeValue", parent, "2인", 26, TextAnchor.MiddleCenter, White);
        SetRect(controller.roomSizeValueText.rectTransform, new Vector2(90f, 198f), new Vector2(110f, 52f));
        controller.roomSizePlusButton = CreateButton("RoomSizePlus", parent, "+", string.Empty, Secondary);
        SetRect(controller.roomSizePlusButton.GetComponent<RectTransform>(), new Vector2(190f, 198f), new Vector2(60f, 52f));

        controller.createRoomButton = CreateButton("CreateRoomButton", parent, "방 만들기", "create_room", Primary);
        SetRect(controller.createRoomButton.GetComponent<RectTransform>(), new Vector2(0f, 115f), new Vector2(610f, 66f));

        Text quickTitle = CreateText("QuickMatchTitle", parent, "빠른 랜덤 매칭", 24, TextAnchor.MiddleCenter, White);
        SetRect(quickTitle.rectTransform, new Vector2(0f, 45f), new Vector2(610f, 42f));
        AddLocalization(quickTitle, "quick_match_title");
        controller.quickMatch2Button = CreateButton("QuickMatch2", parent, "2인", "quick_match_2", Secondary);
        SetRect(controller.quickMatch2Button.GetComponent<RectTransform>(), new Vector2(-210f, -15f), new Vector2(185f, 58f));
        controller.quickMatch3Button = CreateButton("QuickMatch3", parent, "3인", "quick_match_3", Secondary);
        SetRect(controller.quickMatch3Button.GetComponent<RectTransform>(), new Vector2(0f, -15f), new Vector2(185f, 58f));
        controller.quickMatch4Button = CreateButton("QuickMatch4", parent, "4인", "quick_match_4", Secondary);
        SetRect(controller.quickMatch4Button.GetComponent<RectTransform>(), new Vector2(210f, -15f), new Vector2(185f, 58f));

        Text joinTitle = CreateText("JoinTitle", parent, "방 코드로 참가", 24, TextAnchor.MiddleCenter, White);
        SetRect(joinTitle.rectTransform, new Vector2(0f, -95f), new Vector2(610f, 42f));
        AddLocalization(joinTitle, "join_title");
        controller.roomCodeInput = CreateInput("RoomCodeInput", parent, "6자리 방 코드", "room_code_placeholder");
        controller.roomCodeInput.characterLimit = 40;
        SetRect(controller.roomCodeInput.GetComponent<RectTransform>(), new Vector2(-85f, -155f), new Vector2(440f, 58f));
        controller.joinRoomButton = CreateButton("JoinRoomButton", parent, "참가", "join_room", Primary);
        SetRect(controller.joinRoomButton.GetComponent<RectTransform>(), new Vector2(245f, -155f), new Vector2(190f, 58f));

        controller.cancelMatchmakingButton = CreateButton("CancelMatchmakingButton", parent, "매칭 취소", "cancel_matchmaking", new Color32(145, 63, 73, 255));
        SetRect(controller.cancelMatchmakingButton.GetComponent<RectTransform>(), new Vector2(0f, -245f), new Vector2(300f, 54f));
        controller.cancelMatchmakingButton.gameObject.SetActive(false);

        Text hint = CreateText("MainHint", parent, "서버 연결은 메뉴 선택 시 자동으로 진행됩니다.", 18, TextAnchor.MiddleCenter, Muted);
        SetRect(hint.rectTransform, new Vector2(0f, -305f), new Vector2(700f, 36f));
        AddLocalization(hint, "connection_hint");
    }

    private static void BuildLobby(Transform parent, ObjectHeadTitleScreen controller)
    {
        controller.lobbyTitleText = CreateText("LobbyTitle", parent, "방 로비 · 방장", 34, TextAnchor.MiddleCenter, White);
        SetRect(controller.lobbyTitleText.rectTransform, new Vector2(0f, 285f), new Vector2(760f, 60f));

        controller.lobbyRoomCodeText = CreateText("RoomCodeValue", parent, "방 코드: ------", 30, TextAnchor.MiddleCenter, Primary);
        SetRect(controller.lobbyRoomCodeText.rectTransform, new Vector2(-90f, 222f), new Vector2(530f, 52f));
        controller.copyRoomCodeButton = CreateButton("CopyRoomCodeButton", parent, "코드 복사", "copy_room_code", Secondary);
        SetRect(controller.copyRoomCodeButton.GetComponent<RectTransform>(), new Vector2(290f, 222f), new Vector2(190f, 52f));

        controller.lobbyPlayerListText = CreateText("PlayerList", parent, "참가자를 기다리는 중...", 25, TextAnchor.UpperLeft, White);
        SetRect(controller.lobbyPlayerListText.rectTransform, new Vector2(-155f, 75f), new Vector2(510f, 220f));

        Text settingsTitle = CreateText("RoomSettingsTitle", parent, "방 설정", 25, TextAnchor.MiddleCenter, White);
        SetRect(settingsTitle.rectTransform, new Vector2(275f, 125f), new Vector2(260f, 42f));
        AddLocalization(settingsTitle, "room_settings");

        controller.lobbyCapacityText = CreateText("LobbyCapacity", parent, "참가자 0/2", 22, TextAnchor.MiddleCenter, Muted);
        SetRect(controller.lobbyCapacityText.rectTransform, new Vector2(275f, 77f), new Vector2(280f, 40f));
        controller.lobbySizeMinusButton = CreateButton("LobbySizeMinus", parent, "−", string.Empty, Secondary);
        SetRect(controller.lobbySizeMinusButton.GetComponent<RectTransform>(), new Vector2(190f, 25f), new Vector2(58f, 48f));
        controller.lobbySizePlusButton = CreateButton("LobbySizePlus", parent, "+", string.Empty, Secondary);
        SetRect(controller.lobbySizePlusButton.GetComponent<RectTransform>(), new Vector2(360f, 25f), new Vector2(58f, 48f));

        controller.lobbyMapModeText = CreateText("MapMode", parent, "고정 맵", 22, TextAnchor.MiddleCenter, White);
        SetRect(controller.lobbyMapModeText.rectTransform, new Vector2(275f, -35f), new Vector2(280f, 40f));
        controller.toggleMapModeButton = CreateButton("ToggleMapModeButton", parent, "맵 선택 전환", "toggle_map_mode", Secondary);
        SetRect(controller.toggleMapModeButton.GetComponent<RectTransform>(), new Vector2(275f, -85f), new Vector2(280f, 48f));
        controller.applySettingsButton = CreateButton("ApplySettingsButton", parent, "설정 적용", "apply_settings", Primary);
        SetRect(controller.applySettingsButton.GetComponent<RectTransform>(), new Vector2(275f, -145f), new Vector2(280f, 52f));

        controller.readyButton = CreateButton("ReadyButton", parent, "준비", string.Empty, Primary);
        SetRect(controller.readyButton.GetComponent<RectTransform>(), new Vector2(-225f, -235f), new Vector2(250f, 64f));
        controller.readyButtonText = controller.readyButton.GetComponentInChildren<Text>();
        controller.startGameButton = CreateButton("StartGameButton", parent, "게임 시작", "start_game", new Color32(72, 181, 110, 255));
        SetRect(controller.startGameButton.GetComponent<RectTransform>(), new Vector2(55f, -235f), new Vector2(250f, 64f));
        controller.leaveRoomButton = CreateButton("LeaveRoomButton", parent, "나가기", "leave_room", new Color32(145, 63, 73, 255));
        SetRect(controller.leaveRoomButton.GetComponent<RectTransform>(), new Vector2(335f, -235f), new Vector2(200f, 64f));
    }

    private static ObjectHeadLocalizationTable CreateOrUpdateLocalization()
    {
        return ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
    }

    private static void UpdateNetworkConfig()
    {
        ObjectHeadNetworkConfig config = AssetDatabase.LoadAssetAtPath<ObjectHeadNetworkConfig>(NetworkConfigPath);
        if (config == null) return;
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("titleSceneName").stringValue = "ObjectHeadTitle";
        SerializedProperty bindings = serialized.FindProperty("mapScenes");
        bindings.arraySize = 1;
        bindings.GetArrayElementAtIndex(0).FindPropertyRelative("mapId").stringValue = "object_head_demo_01";
        bindings.GetArrayElementAtIndex(0).FindPropertyRelative("sceneName").stringValue = "SampleScene";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        }
        .Concat(EditorBuildSettings.scenes.Where(scene => !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase) && !string.Equals(scene.path, "Assets/Scenes/SampleScene.unity", StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent, Panel);
        SetRect(image.rectTransform, position, size);
        return image.gameObject;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        // Localized and dynamic labels are populated by ObjectHeadTitleScreen.
        // Keep the player-scene serialization ASCII-only because Unity 6000.4
        // can corrupt level0 when generated legacy UI Text contains CJK data.
        text.text = value == "+" ? "+" : value == "−" ? "-" : string.Empty;
        // The runtime controller assigns an OS font before the first rendered frame.
        // Keeping generated scene font references empty avoids serializing Unity's
        // legacy built-in font into level0 on Unity 6000.4.
        text.font = null;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string value, string localizationKey, Color color)
    {
        Image image = CreateImage(name, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.94f, 0.80f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
        button.colors = colors;
        Text label = CreateText("Label", image.transform, value, 23, TextAnchor.MiddleCenter, White);
        Stretch(label.rectTransform, 8f);
        if (!string.IsNullOrWhiteSpace(localizationKey)) AddLocalization(label, localizationKey);
        return button;
    }

    private static InputField CreateInput(string name, Transform parent, string placeholderValue, string placeholderKey)
    {
        Image image = CreateImage(name, parent, Input);
        InputField field = image.gameObject.AddComponent<InputField>();
        Text value = CreateText("Text", image.transform, string.Empty, 23, TextAnchor.MiddleLeft, White);
        Stretch(value.rectTransform, 16f);
        Text placeholder = CreateText("Placeholder", image.transform, placeholderValue, 22, TextAnchor.MiddleLeft, Muted);
        Stretch(placeholder.rectTransform, 16f);
        AddLocalization(placeholder, placeholderKey);
        field.textComponent = value;
        field.placeholder = placeholder;
        field.lineType = InputField.LineType.SingleLine;
        field.caretColor = White;
        field.selectionColor = new Color32(73, 137, 195, 160);
        return field;
    }

    private static void AddLocalization(Text text, string key)
    {
        ObjectHeadLocalizedLabel localized = text.gameObject.AddComponent<ObjectHeadLocalizedLabel>();
        localized.LocalizationKey = key;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}

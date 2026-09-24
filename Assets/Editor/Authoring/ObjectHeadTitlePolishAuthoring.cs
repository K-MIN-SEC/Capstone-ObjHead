using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// A one-time, editor-only pass. Every position, colour and size remains editable in the prefab.
public static class ObjectHeadTitlePolishAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/UI/ObjectHeadTitle.prefab";
    private static readonly Color Ink = new Color32(15, 39, 50, 250);
    private static readonly Color Blue = new Color32(43, 82, 96, 255);
    private static readonly Color BlueHover = new Color32(61, 111, 124, 255);
    private static readonly Color Gold = new Color32(232, 154, 89, 255);
    private static readonly Color Cream = new Color32(255, 242, 220, 255);
    private static readonly Color Muted = new Color32(189, 216, 215, 255);
    private static Sprite panelSprite;
    private static Font font;

    [MenuItem("Object Head/0923/Polish Title UI")]
    public static void Apply()
    {
        panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/InkPanel.png");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansCJKkr-Regular.otf");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var title = root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            var front = title.GetComponent<ObjectHeadFrontEnd>();
            var settings = front.settingsController;
            var main = title.mainMenuPanel.transform;

            foreach (var card in new[] {front.home, front.play, front.quick, front.create, front.find, front.settings})
                StyleCard(card);

            StyleTop(title);
            StyleHome(title, front, main);
            StylePlay(title, front);
            StyleQuick(title, front);
            StyleCreate(title, front);
            StyleFind(title, front);
            StyleSettings(front, settings);
            StyleLobby(title);

            StyleButton(front.backButton, Blue, 21, false);
            Place(front.backButton.transform, -260, -356, 200, 49);
            front.compactBackPosition = new Vector2(-260, -356);
            front.roomBrowserBackPosition = new Vector2(-540, -347);
            if (title.helpButton != null) StyleButton(title.helpButton, Blue, 18, false);
            if (title.quitButton != null) StyleButton(title.quitButton, Blue, 18, false);
            StyleStatus(title);

            foreach (var label in root.GetComponentsInChildren<ObjectHeadLocalizedLabel>(true))
                label.Preview();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Object Head] Title prefab polished. Layout and settings styling remain editable in the Inspector.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StyleTop(ObjectHeadTitleScreen title)
    {
        var root = title.transform;
        var brand = root.Find("Brand")?.GetComponent<Text>();
        if (brand != null) { brand.font = font; brand.color = Gold; brand.fontSize = 20; }
        var name = root.Find("GameTitle")?.GetComponent<Text>();
        if (name != null) { name.font = font; name.color = Cream; name.fontSize = 62; }
        Decoration(root, "TitleUnderline", -636, 266, 150, 5, Gold);
        StyleButton(title.koreanButton, Blue, 18, false);
        StyleButton(title.englishButton, Blue, 18, false);
    }

    private static void StyleStatus(ObjectHeadTitleScreen title)
    {
        if (title.statusText == null) return;
        var status = title.statusText;
        Place(status.transform, -410, -412, 690, 34);
        status.font = font; status.fontSize = 17; status.color = Cream;
        status.alignment = TextAnchor.MiddleLeft;
        var backing = status.transform.parent.Find("StatusBacking");
        if (backing == null)
        {
            var go = new GameObject("StatusBacking", typeof(RectTransform));
            go.transform.SetParent(status.transform.parent, false);
            backing = go.transform;
        }
        Place(backing, -405, -412, 750, 40);
        TintImage(backing.GetComponent<Image>() ?? backing.gameObject.AddComponent<Image>(), new Color32(15, 39, 50, 225));
        backing.GetComponent<Image>().raycastTarget = false;
        backing.SetSiblingIndex(status.transform.GetSiblingIndex());
    }

    private static void StyleCard(GameObject card)
    {
        if (card == null) return;
        Place(card.transform, -428, -82, 680, 622);
        var image = card.GetComponent<Image>();
        if (image != null) { image.sprite = panelSprite; image.type = Image.Type.Sliced; image.color = Ink; }
        Decoration(card.transform, "CardAccent", -272, 291, 96, 7, Gold);
        Decoration(card.transform, "CardRule", 0, 213, 564, 2, new Color32(106, 145, 149, 210));
    }

    private static void StyleHome(ObjectHeadTitleScreen title, ObjectHeadFrontEnd front, Transform main)
    {
        var home = front.home.transform;
        var introduction = PlainText(home, "Introduction", 0, 248, 558, 74, 19, Muted);
        var introductionKey = introduction.GetComponent<ObjectHeadLocalizedLabel>() ?? introduction.gameObject.AddComponent<ObjectHeadLocalizedLabel>();
        introductionKey.LocalizationKey = "menu_hint";
        StyleButton(front.playButton, Gold, 28, true);
        StyleButton(front.createButton, Blue, 25, true);
        StyleButton(front.settingsButton, Blue, 25, true);
        Place(front.playButton.transform, 0, 126, 558, 74);
        Place(front.createButton.transform, 0, 34, 558, 74);
        Place(front.settingsButton.transform, 0, -58, 558, 74);
        MoveAndStyleInput(title.nicknameInput, home, 0, -220, 558, 54);
        var nickname = main.Find("nickname_label");
        if (nickname != null)
        {
            nickname.SetParent(home, false);
            Place(nickname, -207, -174, 144, 30);
            var text = nickname.GetComponent<Text>();
            text.fontSize = 19; text.color = Muted; text.alignment = TextAnchor.MiddleLeft;
        }
    }

    private static void StylePlay(ObjectHeadTitleScreen title, ObjectHeadFrontEnd front)
    {
        Heading(front.play.transform, "menu_play");
        StyleButton(front.quickButton, Gold, 25, true);
        StyleButton(front.findButton, Blue, 25, true);
        StyleButton(front.trainingButton, Blue, 25, true);
        StyleButton(title.localPlayButton, Blue, 25, true);
        Place(front.quickButton.transform, 0, 143, 558, 69);
        Place(front.findButton.transform, 0, 54, 558, 69);
        Place(front.trainingButton.transform, 0, -35, 558, 69);
        Place(title.localPlayButton.transform, 0, -124, 558, 69);
    }

    private static void StyleQuick(ObjectHeadTitleScreen title, ObjectHeadFrontEnd front)
    {
        var quick = front.quick.transform;
        var heading = quick.Find("quick_match_title")?.GetComponent<Text>();
        if (heading != null) StyleHeading(heading);
        StyleButton(title.quickMatch2Button, Gold, 24, true);
        StyleButton(title.quickMatch4Button, Blue, 24, true);
        StyleButton(title.quickMatchTeamsButton, Blue, 24, true);
        Place(title.quickMatch2Button.transform, 0, 134, 558, 76);
        Place(title.quickMatch4Button.transform, 0, 32, 558, 76);
        Place(title.quickMatchTeamsButton.transform, 0, -70, 558, 76);
    }

    private static void StyleCreate(ObjectHeadTitleScreen title, ObjectHeadFrontEnd front)
    {
        var create = front.create.transform;
        var heading = create.Find("create_room")?.GetComponent<Text>();
        if (heading != null) StyleHeading(heading);
        var mode = create.Find("mode_select")?.GetComponent<Text>();
        if (mode != null) { Place(mode.transform, -174, 130, 210, 35); mode.alignment = TextAnchor.MiddleLeft; mode.color = Muted; mode.fontSize = 20; }
        StyleButton(title.roomSizeMinusButton, Blue, 25, false);
        StyleButton(title.roomSizePlusButton, Blue, 25, false);
        Place(title.roomSizeMinusButton.transform, -235, 67, 55, 55);
        Place(title.roomSizeValueText.transform, 0, 67, 378, 54);
        Place(title.roomSizePlusButton.transform, 235, 67, 55, 55);
        title.roomSizeValueText.color = Cream;
        title.roomSizeValueText.fontSize = 25;
        var access = title.roomAccess;
        if (access != null)
        {
            Place(access.visibilityButton.transform, 0, -12, 558, 56);
            StyleButton(access.visibilityButton, Blue, 23, false);
            MoveAndStyleInput(access.createPassword, create, 0, -88, 558, 56);
        }
        StyleButton(title.createRoomButton, Gold, 27, true);
        Place(title.createRoomButton.transform, 0, -170, 558, 76);
        var privateNotice = create.Find("PrivateRoomNotice")?.GetComponent<Text>();
        if (privateNotice != null)
        {
            Place(privateNotice.transform, 0, -232, 558, 42);
            privateNotice.font = font; privateNotice.fontSize = 19;
            privateNotice.color = Muted; privateNotice.alignment = TextAnchor.MiddleCenter;
        }
    }

    private static void StyleFind(ObjectHeadTitleScreen title, ObjectHeadFrontEnd front)
    {
        var find = front.find.transform;
        Place(find, 0, 0, 1560, 790);
        SolidPanel(find.GetComponent<Image>(), new Color32(15, 39, 50, 255));
        Decoration(find, "CardAccent", -620, 332, 124, 7, Gold);
        Decoration(find, "CardRule", 0, 259, 1370, 2, new Color32(106, 145, 149, 210));
        Decoration(find, "CodeRule", 0, -196, 1370, 2, new Color32(106, 145, 149, 210));
        var heading = find.Find("find_room")?.GetComponent<Text>();
        if (heading != null)
        {
            Place(heading.transform, -485, 305, 390, 65);
            heading.font = font; heading.fontSize = 38; heading.color = Cream;
            heading.alignment = TextAnchor.MiddleLeft;
        }
        var browser = find.GetComponent<ObjectHeadRoomBrowser>();
        if (browser != null)
        {
            StyleButton(browser.refreshButton, Blue, 23, false);
            Place(browser.refreshButton.transform, 577, 305, 210, 58);
            Place(browser.statusLabel.transform, 0, 26, 1200, 100);
            browser.statusLabel.font = font; browser.statusLabel.fontSize = 27;
            browser.statusLabel.color = Cream; browser.statusLabel.alignment = TextAnchor.MiddleCenter;
            var viewport = browser.content.parent;
            Place(viewport, 0, 26, 1370, 388);
            var viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null) SolidPanel(viewportImage, new Color32(27, 65, 78, 255));
            var content = browser.content;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.spacing = 12; layout.padding = new RectOffset(16, 16, 16, 16); }
            var row = browser.rowTemplate;
            StyleButton(row, Blue, 24, false);
            var rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout != null) rowLayout.preferredHeight = 88;
            var rowText = row.GetComponentInChildren<Text>(true);
            if (rowText != null)
            {
                rowText.font = font; rowText.fontSize = 24; rowText.color = Cream;
                rowText.alignment = TextAnchor.MiddleLeft;
                var rect = rowText.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(30, 9); rect.offsetMax = new Vector2(-30, -9);
            }
            browser.statusLabel.transform.SetAsLastSibling();
        }
        MoveAndStyleInput(title.roomCodeInput, find, -388, -257, 480, 60);
        var access = title.roomAccess;
        if (access != null) MoveAndStyleInput(access.joinPassword, find, 125, -257, 480, 60);
        StyleButton(title.joinRoomButton, Gold, 25, false);
        Place(title.joinRoomButton.transform, 530, -257, 240, 60);
        var hint = find.Find("join_code_hint")?.GetComponent<Text>();
        if (hint != null) { Place(hint.transform, 0, -320, 1100, 40); hint.color = Muted; hint.fontSize = 20; }
    }

    // Room and AI setup use the same authored layout. All coordinates are saved in the prefab.
    private static void StyleLobby(ObjectHeadTitleScreen title)
    {
        var lobby = title.lobbyPanel.transform;
        Place(lobby, 0, 0, 1560, 790);
        SolidPanel(lobby.GetComponent<Image>(), new Color32(15, 39, 50, 255));
        Decoration(lobby, "LobbyLeftPane", -530, 0, 405, 685, new Color32(22, 54, 66, 255));
        Decoration(lobby, "LobbyRightPane", 208, 0, 936, 685, new Color32(22, 54, 66, 255));
        lobby.Find("LobbyLeftPane")?.SetAsFirstSibling();
        lobby.Find("LobbyRightPane")?.SetAsFirstSibling();
        Decoration(lobby, "LobbyLeftAccent", -683, 319, 86, 6, Gold);
        Decoration(lobby, "LobbyRightAccent", -168, 319, 86, 6, Gold);
        Decoration(lobby, "LobbyLeftRule", -530, 166, 350, 2, new Color32(106, 145, 149, 210));
        var obsoleteRightRule = lobby.Find("LobbyRightRule");
        if (obsoleteRightRule != null) obsoleteRightRule.gameObject.SetActive(false);

        LobbyText(title.lobbyTitleText, -530, 277, 355, 52, 29, TextAnchor.MiddleCenter);
        LobbyText(title.lobbyRoomCodeText, -530, 229, 355, 38, 20, TextAnchor.MiddleCenter);
        StyleButton(title.copyRoomCodeButton, Blue, 19, false);
        Place(title.copyRoomCodeButton.transform, -530, 188, 350, 40);
        LobbyText(title.lobbyCapacityText, -568, 124, 200, 34, 20, TextAnchor.MiddleCenter);
        StyleButton(title.lobbySizeMinusButton, Blue, 20, false);
        StyleButton(title.lobbySizePlusButton, Blue, 20, false);
        Place(title.lobbySizeMinusButton.transform, -435, 124, 42, 38);
        Place(title.lobbySizePlusButton.transform, -384, 124, 42, 38);
        LobbyText(title.lobbyModeText, -530, 84, 350, 34, 19, TextAnchor.MiddleCenter);
        LobbyText(title.lobbyPlayerListText, -530, -35, 340, 220, 17, TextAnchor.UpperLeft);
        title.lobbyPlayerRows = new Text[4];
        title.lobbyPlayerTeamMarks = new Image[4];
        title.lobbyTeamColors = new Color[]
        {
            (Color)new Color32(91, 173, 212, 255), (Color)new Color32(237, 160, 92, 255),
            (Color)new Color32(169, 197, 111, 255), (Color)new Color32(185, 144, 212, 255)
        };
        for (int i = 0; i < title.lobbyPlayerRows.Length; i++)
        {
            string name = "LobbyPlayerRow" + (i + 1);
            var row = lobby.Find(name);
            if (row == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(lobby, false);
                row = go.transform;
            }
            Place(row, -530, 36 - i * 58, 350, 53);
            SolidPanel(row.GetComponent<Image>(), new Color32(34, 75, 88, 255));
            row.GetComponent<Image>().raycastTarget = false;
            Decoration(row, "TeamMark", -169, 0, 6, 49, title.lobbyTeamColors[i]);
            title.lobbyPlayerTeamMarks[i] = row.Find("TeamMark").GetComponent<Image>();
            var label = PlainText(row, "PlayerAndHeads", 6, 0, 322, 49, 17, Cream);
            label.alignment = TextAnchor.MiddleLeft;
            label.lineSpacing = .93f;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 17;
            title.lobbyPlayerRows[i] = label;
        }
        StyleButton(title.aiRosterModeButton, Blue, 18, false);
        StyleButton(title.aiDifficultyButton, Blue, 18, false);
        if (title.aiRosterModeButton != null) Place(title.aiRosterModeButton.transform, -624, -218, 162, 46);
        if (title.aiDifficultyButton != null) Place(title.aiDifficultyButton.transform, -436, -218, 162, 46);
        StyleButton(title.leaveRoomButton, Blue, 22, false);
        Place(title.leaveRoomButton.transform, -530, -303, 350, 56);

        LobbyText(title.localPlayerLabel, 173, 301, 640, 46, 28, TextAnchor.MiddleCenter);
        LobbyText(title.selectionHint, 208, 255, 850, 35, 19, TextAnchor.MiddleCenter);
        StyleButton(title.editLocalSquadButton, Blue, 17, false);
        if (title.editLocalSquadButton != null) Place(title.editLocalSquadButton.transform, 570, 285, 195, 42);
        for (int i = 0; i < title.slotButtons.Length; i++)
        {
            var slot = title.slotButtons[i];
            if (slot == null) continue;
            StyleButton(slot, Blue, 22, false);
            Place(slot.transform, -101 + i * 292, 183, 265, 82);
            if (i < title.slotLabels.Length && title.slotLabels[i] != null)
                LobbyText(title.slotLabels[i], 55, 0, 142, 66, 22, TextAnchor.MiddleCenter);
            if (i < title.slotPortraits.Length && title.slotPortraits[i] != null)
                Place(title.slotPortraits[i].transform, -56, 0, 70, 72);
        }
        var browser = title.characterBrowser;
        if (browser != null)
        {
            MoveAndStyleInput(browser.search, lobby, 20, 115, 520, 44);
            StyleButton(browser.roleButton, Blue, 18, false);
            Place(browser.roleButton.transform, 424, 115, 190, 44);
            LobbyText(browser.countLabel, 610, 115, 96, 40, 17, TextAnchor.MiddleCenter);
            Place(browser.content.parent, 208, -18, 900, 215);
            var viewportImage = browser.content.parent.GetComponent<Image>();
            if (viewportImage != null) SolidPanel(viewportImage, new Color32(29, 67, 80, 255));
            var scrollbar = browser.content.parent.parent.Find("CharacterScrollbar");
            if (scrollbar != null) Place(scrollbar, 665, -18, 14, 211);
            var grid = browser.content.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(198, 94); grid.spacing = new Vector2(12, 10);
                grid.padding = new RectOffset(12, 12, 9, 9);
            }
            StyleButton(browser.cardTemplate, Blue, 18, false);
            Place(browser.cardTemplate.transform, 0, 0, 198, 94);
            var cardName = browser.cardTemplate.transform.Find("Label")?.GetComponent<Text>();
            if (cardName != null) LobbyText(cardName, 0, -31, 188, 25, 18, TextAnchor.MiddleCenter);
            var portrait = browser.cardTemplate.transform.Find("Portrait");
            if (portrait != null) Place(portrait, 0, 12, 116, 57);
        }
        LobbyText(title.characterDescription, 208, -149, 850, 36, 19, TextAnchor.MiddleCenter);
        Place(title.mapPreview.transform, -116, -226, 212, 84);
        StyleButton(title.previousMapButton, Blue, 20, false);
        StyleButton(title.nextMapButton, Blue, 20, false);
        Place(title.previousMapButton.transform, -249, -226, 39, 48);
        Place(title.nextMapButton.transform, 17, -226, 39, 48);
        LobbyText(title.mapName, 230, -226, 320, 70, 19, TextAnchor.MiddleCenter);
        StyleButton(title.toggleMapModeButton, Blue, 18, false);
        StyleButton(title.applySettingsButton, Blue, 18, false);
        Place(title.toggleMapModeButton.transform, 545, -205, 176, 38);
        Place(title.applySettingsButton.transform, 545, -249, 176, 38);
        StyleButton(title.readyButton, Gold, 22, false);
        StyleButton(title.startGameButton, Blue, 22, false);
        Place(title.readyButton.transform, -12, -324, 410, 54);
        Place(title.startGameButton.transform, 438, -324, 410, 54);

        var root = title.transform;
        title.menuOnlyDecorations = new[]
        {
            root.Find("Brand")?.gameObject,
            root.Find("GameTitle")?.gameObject,
            root.Find("TitleUnderline")?.gameObject,
            title.helpButton?.gameObject,
            title.quitButton?.gameObject,
            title.statusText?.gameObject,
            root.Find("StatusBacking")?.gameObject,
            title.koreanButton?.gameObject,
            title.englishButton?.gameObject
        };
    }

    private static void LobbyText(Text label, float x, float y, float width, float height, int size, TextAnchor alignment)
    {
        if (label == null) return;
        Place(label.transform, x, y, width, height);
        label.font = font; label.fontSize = size; label.color = Cream; label.alignment = alignment;
    }

    private static void StyleSettings(ObjectHeadFrontEnd front, ObjectHeadSettingsPanel settings)
    {
        var parent = front.settings.transform;
        var heading = parent.Find("menu_settings")?.GetComponent<Text>();
        if (heading != null) StyleHeading(heading);
        string[] names = {"resolution", "window_mode", "language", "volume_bgm", "volume_sfx"};
        float[] y = {145, 77, 9, -59, -127};
        for (int i = 0; i < names.Length; i++)
        {
            var label = parent.Find(names[i])?.GetComponent<Text>();
            if (label == null) continue;
            Place(label.transform, -207, y[i], 170, 44);
            label.font = font; label.fontSize = 20; label.color = Cream; label.alignment = TextAnchor.MiddleLeft;
        }
        BuildSelector(parent, settings.resolution, "Resolution", y[0], out settings.resolutionValue, out settings.resolutionPrev, out settings.resolutionNext);
        BuildSelector(parent, settings.windowMode, "WindowMode", y[1], out settings.windowModeValue, out settings.windowModePrev, out settings.windowModeNext);
        BuildSelector(parent, settings.language, "Language", y[2], out settings.languageValue, out settings.languagePrev, out settings.languageNext);
        StyleSlider(settings.bgm, y[3]);
        StyleSlider(settings.sfx, y[4]);
        settings.bgmValue = Percentage(parent, "BgmValue", 257, y[3]);
        settings.sfxValue = Percentage(parent, "SfxValue", 257, y[4]);
        StyleButton(settings.apply, Gold, 23, false);
        StyleButton(settings.confirm, Blue, 23, false);
        Place(settings.apply.transform, -143, -225, 268, 60);
        Place(settings.confirm.transform, 143, -225, 268, 60);
        Place(settings.status.transform, 0, -279, 558, 38);
        settings.status.color = Cream; settings.status.fontSize = 17;
    }

    private static void BuildSelector(Transform parent, Dropdown dropdown, string name, float y,
        out Text value, out Button previous, out Button next)
    {
        dropdown.gameObject.SetActive(false); // Kept as editable option data, never opened as a broken native popup.
        var field = parent.Find(name + "Selector");
        if (field == null) { var go = new GameObject(name + "Selector", typeof(RectTransform)); go.transform.SetParent(parent, false); field = go.transform; }
        Place(field, 94, y, 340, 51);
        TintImage(field.GetComponent<Image>() ?? field.gameObject.AddComponent<Image>(), Blue);
        value = PlainText(field, "Value", 0, 0, 235, 46, 20, Cream);
        previous = SelectorButton(field, "Previous", -144, "‹");
        next = SelectorButton(field, "Next", 144, "›");
    }

    private static Button SelectorButton(Transform parent, string name, float x, string glyph)
    {
        var child = parent.Find(name);
        if (child == null) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); child = go.transform; }
        Place(child, x, 0, 47, 46);
        TintImage(child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>(), Ink);
        var button = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
        button.targetGraphic = child.GetComponent<Image>();
        var label = PlainText(child, "Glyph", 0, 0, 44, 44, 29, Cream);
        label.text = glyph;
        return button;
    }

    private static void StyleSlider(Slider slider, float y)
    {
        if (slider == null) return;
        Place(slider.transform, 57, y, 265, 34);
        var background = slider.transform.Find("Background")?.GetComponent<Image>();
        if (background != null) { background.sprite = panelSprite; background.type = Image.Type.Sliced; background.color = Blue; }
        var fill = slider.fillRect?.GetComponent<Image>();
        if (fill != null) fill.color = Gold;
        var handle = slider.handleRect?.GetComponent<Image>();
        if (handle != null) { handle.sprite = panelSprite; handle.type = Image.Type.Sliced; handle.color = Cream; }
    }

    private static void MoveAndStyleInput(InputField input, Transform parent, float x, float y, float w, float h)
    {
        if (input == null) return;
        input.transform.SetParent(parent, false);
        Place(input.transform, x, y, w, h);
        TintImage(input.GetComponent<Image>(), Blue);
        if (input.textComponent != null) { input.textComponent.font = font; input.textComponent.fontSize = 21; input.textComponent.color = Cream; }
        var placeholder = input.placeholder as Text;
        if (placeholder != null) { placeholder.font = font; placeholder.fontSize = 20; placeholder.color = Muted; }
    }

    private static void StyleButton(Button button, Color color, int size, bool arrow)
    {
        if (button == null) return;
        if (button.GetComponent<ObjectHeadMenuButtonMotion>() == null)
            button.gameObject.AddComponent<ObjectHeadMenuButtonMotion>();
        TintImage(button.targetGraphic as Image, color);
        var text = button.transform.Find("Label")?.GetComponent<Text>() ?? button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.font = font; text.fontSize = size; text.color = Cream;
            text.alignment = arrow ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(arrow ? 22 : 10, 4);
            rect.offsetMax = new Vector2(arrow ? -60 : -10, -4);
        }
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(246, 247, 228, 255);
        colors.pressedColor = new Color32(189, 208, 204, 255);
        colors.disabledColor = new Color32(130, 144, 143, 180);
        button.colors = colors;
        var existingMarker = button.transform.Find("Chevron");
        if (existingMarker != null) existingMarker.gameObject.SetActive(arrow);
        if (arrow)
        {
            var marker = PlainText(button.transform, "Chevron", 0, 0, 55, 62, 36, Cream);
            var markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(1, .5f);
            markerRect.anchoredPosition = new Vector2(-33, 0);
            marker.text = "›";
            marker.gameObject.SetActive(true);
        }
    }

    private static void TintImage(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = panelSprite; image.type = Image.Type.Sliced; image.color = color;
    }

    private static void SolidPanel(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = null; image.type = Image.Type.Simple; image.color = color;
    }

    private static void Heading(Transform parent, string key)
    {
        var label = PlainText(parent, "PageHeading", -2, 258, 560, 54, 32, Cream);
        var loc = label.GetComponent<ObjectHeadLocalizedLabel>() ?? label.gameObject.AddComponent<ObjectHeadLocalizedLabel>();
        loc.LocalizationKey = key;
    }

    private static void StyleHeading(Text label)
    {
        Place(label.transform, 0, 258, 560, 54);
        label.font = font; label.fontSize = 32; label.color = Cream; label.alignment = TextAnchor.MiddleCenter;
    }

    private static Text Percentage(Transform parent, string name, float x, float y)
    {
        var text = PlainText(parent, name, x, y, 82, 40, 20, Cream);
        text.text = "100%";
        return text;
    }

    private static Text PlainText(Transform parent, string name, float x, float y, float w, float h, int size, Color color)
    {
        var child = parent.Find(name);
        if (child == null) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); child = go.transform; }
        Place(child, x, y, w, h);
        var text = child.GetComponent<Text>() ?? child.gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.color = color;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        return text;
    }

    private static void Decoration(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var child = parent.Find(name);
        if (child == null) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); child = go.transform; }
        Place(child, x, y, w, h);
        var image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
        image.sprite = null; image.color = color; image.raycastTarget = false;
    }

    private static void Place(Transform transform, float x, float y, float w, float h)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }
}

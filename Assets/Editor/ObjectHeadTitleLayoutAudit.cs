using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Runs headlessly against the authored prefab; it never opens or focuses the player window.
public static class ObjectHeadTitleLayoutAudit
{
    private const string PrefabPath = "Assets/Prefabs/UI/ObjectHeadTitle.prefab";
    private static Transform root;
    private static int checks;

    public static void Run()
    {
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            checks = 0;
            var title = prefab.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            root = title.mainMenuPanel.transform;
            var front = title.frontEnd;
            var access = title.roomAccess;
            var settings = front.settingsController;
            var browser = front.find.GetComponent<ObjectHeadRoomBrowser>();

            var screen = new Rect(-800, -450, 1600, 900);
            foreach (var panel in new[] {front.home, front.play, front.quick, front.create, front.settings, front.find})
                Inside(panel.name, RectOf(panel.transform), screen, 0);

            Page("Home", front.home,
                front.playButton, front.createButton, front.settingsButton,
                title.quitButton, title.nicknameInput);
            Page("Play", front.play,
                front.quickButton, front.findButton, front.trainingButton,
                title.localPlayButton, front.backButton);
            Page("Quick match", front.quick,
                title.quickMatch2Button, title.quickMatch4Button,
                title.quickMatchTeamsButton, front.backButton);

            var create = (RectTransform)title.createRoomButton.transform;
            Vector2 initialCreatePosition = create.anchoredPosition;
            create.anchoredPosition = access.publicCreatePosition;
            Page("Public room", front.create,
                title.roomSizeMinusButton, title.roomSizePlusButton,
                access.visibilityButton, title.createRoomButton, front.backButton);
            create.anchoredPosition = access.privateCreatePosition;
            Page("Private room", front.create,
                title.roomSizeMinusButton, title.roomSizePlusButton,
                access.visibilityButton, access.createPassword,
                title.createRoomButton, front.backButton);
            create.anchoredPosition = initialCreatePosition;

            Page("Settings", front.settings,
                settings.resolutionPrev, settings.resolutionNext,
                settings.windowModePrev, settings.windowModeNext,
                settings.languagePrev, settings.languageNext,
                settings.bgm, settings.sfx, settings.apply,
                settings.confirm, front.backButton);
            Page("Room browser", front.find,
                browser.refreshButton, browser.content.parent,
                title.roomCodeInput, access.joinPassword,
                title.joinRoomButton, front.backButton);

            CheckCharacterNames(title);
            CheckRosterDetails(title.localization);
            CheckOnlineLobby(title);
            CheckIncomingLobbyRoster(title);
            Debug.Log($"[TITLE_LAYOUT_PASS] {checks} layout, Korean/English text geometry and roster checks at 1600x900 reference layout; 1280x720 and 1920x1080 use the same 16:9 canvas scaling.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static void CheckOnlineLobby(ObjectHeadTitleScreen title)
    {
        Transform previousRoot = root;
        root = title.transform;
        try
        {
            Page("Online lobby AI controls", title.lobbyPanel,
                title.lobbyPlayerRows[0].transform.parent, title.lobbyPlayerRows[1].transform.parent,
                title.lobbyPlayerRows[2].transform.parent, title.lobbyPlayerRows[3].transform.parent,
                title.onlineAIAddButton, title.onlineAIRemoveButton, title.onlineAIDifficultyButton,
                title.leaveRoomButton);
            if (title.lobbyStartHint == null) throw new InvalidOperationException("Lobby start/error hint is not bound");
            Rect hintBounds = RectOf(title.lobbyStartHint.transform);
            Inside("Lobby start hint", hintBounds, RectOf(title.lobbyPanel.transform), 5);
            foreach (var neighbour in new Component[] {title.mapPreview, title.mapName, title.previousMapButton,
                         title.nextMapButton, title.toggleMapModeButton, title.applySettingsButton,
                         title.readyButton, title.startGameButton})
            {
                if (hintBounds.Overlaps(RectOf(neighbour.transform)))
                    throw new InvalidOperationException("Lobby hint overlaps " + neighbour.name);
                checks++;
            }
            foreach (ObjectHeadLanguage language in Enum.GetValues(typeof(ObjectHeadLanguage)))
            {
                var addLabel = title.onlineAIAddButton.GetComponentInChildren<Text>(true);
                var removeLabel = title.onlineAIRemoveButton.GetComponentInChildren<Text>(true);
                CheckTextGeometry(addLabel, title.localization.Get("online_ai_add", language),
                    RectOf(addLabel.transform).size, "Online AI add/" + language);
                CheckTextGeometry(removeLabel, title.localization.Get("online_ai_remove", language),
                    RectOf(removeLabel.transform).size, "Online AI remove/" + language);
                foreach (ObjectHeadAIDifficulty difficulty in Enum.GetValues(typeof(ObjectHeadAIDifficulty)))
                    CheckTextGeometry(title.onlineAIDifficultyText,
                        title.localization.Get("ai_difficulty_" + difficulty.ToString().ToLowerInvariant(), language),
                        RectOf(title.onlineAIDifficultyText.transform).size, "Online AI difficulty/" + language + "/" + difficulty);
                foreach (string key in new[] { "lobby_three_ffa_hint", "lobby_three_team_hint", "lobby_need_opponent",
                             "lobby_ready_hint", "lobby_can_start", "lobby_not_ready", "not_enough_players",
                             "all_players_ready_required", "room_update_in_progress", "room_join_in_progress",
                             "ai_host_only", "ai_not_found", "room_capacity_too_small", "room_full" })
                {
                    string value = title.localization.Get(key, language);
                    if (value == key) throw new InvalidOperationException("Missing lobby localization: " + key);
                    CheckTextGeometry(title.lobbyStartHint, value, hintBounds.size, "Lobby hint/" + language + "/" + key);
                }
            }
        }
        finally { root = previousRoot; }
    }

    private static void CheckIncomingLobbyRoster(ObjectHeadTitleScreen title)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var selected = typeof(ObjectHeadTitleScreen).GetField("selectedMode", flags);
        var capacity = typeof(ObjectHeadTitleScreen).GetField("roomSize", flags);
        var buildRoster = typeof(ObjectHeadTitleScreen).GetMethod("BuildPlayerRoster", flags);
        var displayRoster = typeof(ObjectHeadTitleScreen).GetMethod("DisplayRoster", flags);
        var restore = typeof(ObjectHeadTitleScreen).GetMethod("RestoreLobbySelection", flags);
        object previousMode = selected.GetValue(title);
        object previousCapacity = capacity.GetValue(title);
        try
        {
            foreach (var mode in new[] {ObjectHeadMatchMode.FreeForAll, ObjectHeadMatchMode.Teams})
            {
                // A freshly joining client still has Duel selected when the third
                // player's room state arrives. This used to throw Alliance(3).
                selected.SetValue(title, ObjectHeadMatchMode.Duel);
                var lobby = new ObjectHeadLobbyState
                {
                    settings = new ObjectHeadRoomSettings { mode = mode, minPlayers = 2, maxPlayers = 4 },
                    players = new[]
                    {
                        new ObjectHeadLobbyPlayer { userId = "host", username = "Host", playerIndex = 1 },
                        new ObjectHeadLobbyPlayer { userId = "guest2", username = "Guest 2", playerIndex = 2 },
                        new ObjectHeadLobbyPlayer { userId = "guest3", username = "Guest 3", playerIndex = 3 }
                    }
                };
                var roster = (Array)buildRoster.Invoke(title, new object[] {lobby});
                if (roster.Length != 3) throw new InvalidOperationException("Incoming 3-player roster lost a player");
                displayRoster.Invoke(title, new object[] {roster, mode});
                for (int i = 0; i < 3; i++)
                    if (string.IsNullOrWhiteSpace(title.lobbyPlayerRows[i].text))
                        throw new InvalidOperationException("Incoming roster row was not rendered");
                checks += 4;
                restore.Invoke(title, new object[] {lobby});
                if ((ObjectHeadMatchMode)selected.GetValue(title) != mode || (int)capacity.GetValue(title) != 4)
                    throw new InvalidOperationException("Rejected room changes do not restore authoritative mode/capacity");
                checks++;
            }
            if (ObjectHeadTitleScreen.LobbyAlliance(title.content, ObjectHeadMatchMode.Duel, 3) != 3 ||
                ObjectHeadTitleScreen.LobbyAlliance(title.content, ObjectHeadMatchMode.Teams, 99) != 99 ||
                ObjectHeadTitleScreen.LobbyAlliance(null, ObjectHeadMatchMode.Duel, 0) != 1)
                throw new InvalidOperationException("Malformed/stale roster indices are not safely handled");
            checks += 3;
        }
        finally
        {
            selected.SetValue(title, previousMode);
            capacity.SetValue(title, previousCapacity);
        }
    }

    private static void CheckCharacterNames(ObjectHeadTitleScreen title)
    {
        var browser = title.characterBrowser;
        if (browser == null || browser.cardTemplate == null || title.content == null || title.localization == null)
            throw new InvalidOperationException("The character browser is not fully bound");
        var template = browser.cardTemplate;
        var label = template.transform.Find("Label").GetComponent<Text>();
        var portrait = (RectTransform)template.transform.Find("Portrait");
        var cardRect = (RectTransform)template.transform;
        Rect cardBounds = new Rect(-Vector2.Scale(cardRect.sizeDelta, cardRect.pivot), cardRect.sizeDelta);
        Rect labelBounds = ChildRect(label.rectTransform, cardBounds);
        Rect portraitBounds = ChildRect(portrait, cardBounds);
        Inside("Character card name", labelBounds, cardBounds, 2);
        Inside("Character card portrait", portraitBounds, cardBounds, 2);
        if (labelBounds.Overlaps(portraitBounds))
            throw new InvalidOperationException("Character card name overlaps its portrait");
        checks++;

        foreach (ObjectHeadLanguage language in Enum.GetValues(typeof(ObjectHeadLanguage)))
        foreach (var character in title.content.characters)
        {
            string name = title.localization.Get(character.nameKey, language);
            string context = $"{language}/{character.kind}";
            CheckTextGeometry(label, name, labelBounds.size, "Template/" + context);
            // The runtime browser instantiates this exact template for every card.
            var clone = UnityEngine.Object.Instantiate(template, browser.content);
            try
            {
                clone.gameObject.SetActive(true);
                var cloneLabel = clone.transform.Find("Label").GetComponent<Text>();
                cloneLabel.text = name;
                CheckTextGeometry(cloneLabel, name, labelBounds.size, "Clone/" + context);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone.gameObject); }

            for (int i = 0; i < title.slotLabels.Length; i++)
            {
                var slotLabel = title.slotLabels[i];
                CheckTextGeometry(slotLabel, name, slotLabel.rectTransform.sizeDelta, $"Selected slot {i + 1}/" + context);
            }
        }
    }

    private static void CheckTextGeometry(Text label, string value, Vector2 size, string context)
    {
        if (label == null || label.font == null || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(context + " has no renderable name/font");
        var settings = label.GetGenerationSettings(size);
        settings.scaleFactor = 1; // Check design pixels, independent of the batch editor's display.
        label.font.RequestCharactersInTexture(value, label.fontSize, label.fontStyle);
        using (var generator = new TextGenerator())
        {
            float neededHeight = generator.GetPreferredHeight(value, settings);
            float neededWidth = generator.GetPreferredWidth(value, settings);
            if (neededHeight > size.y + .1f || neededWidth > size.x + .1f)
                throw new InvalidOperationException($"{context} text does not fit: needs {neededWidth:F1}x{neededHeight:F1}, has {size}");
            // Non-empty strings alone missed the bug: vertical truncation generated no visible glyphs.
            if (!generator.Populate(value, settings) || generator.vertexCount <= 4)
                throw new InvalidOperationException(context + " generates no visible text geometry");
            Rect bounds = new Rect(-Vector2.Scale(size, settings.pivot), size);
            var vertices = generator.verts;
            for (int i = 0; i < vertices.Count - 4; i++)
            {
                Vector3 position = vertices[i].position;
                if (position.x < bounds.xMin - 1 || position.x > bounds.xMax + 1 ||
                    position.y < bounds.yMin - 1 || position.y > bounds.yMax + 1)
                    throw new InvalidOperationException(context + " has glyphs clipped by its text rectangle");
            }
        }
        checks++;
    }

    private static void CheckRosterDetails(ObjectHeadLocalizationTable localization)
    {
        foreach (ObjectHeadLanguage language in Enum.GetValues(typeof(ObjectHeadLanguage)))
        {
            string notReady = localization.Get("not_ready_state", language);
            string ready = localization.Get("ready_state", language);
            string host = " · " + localization.Get("host_mark", language);
            ExpectRoster(notReady, null, notReady);
            ExpectRoster(notReady, string.Empty, notReady);
            ExpectRoster(notReady + host, string.Empty, notReady + host);
            ExpectRoster(ready, "A / B / C", ready + " · A / B / C");
            ExpectRoster(ready + host, "A / B / C", ready + host + " · A / B / C");
            ExpectRoster(null, "A / B / C", "A / B / C");
        }
    }

    private static void ExpectRoster(string status, string characters, string expected)
    {
        string actual = ObjectHeadTitleScreen.ComposeRosterDetail(status, characters);
        if (actual != expected)
            throw new InvalidOperationException($"Roster detail is duplicated or malformed: '{actual}', expected '{expected}'");
        checks++;
    }

    private static Rect ChildRect(RectTransform rect, Rect parent)
    {
        Vector2 size = Vector2.Scale(parent.size, rect.anchorMax - rect.anchorMin) + rect.sizeDelta;
        Vector2 anchor = parent.min + Vector2.Scale(parent.size,
            new Vector2(Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x),
                Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y)));
        return new Rect(anchor + rect.anchoredPosition - Vector2.Scale(size, rect.pivot), size);
    }

    private static void Page(string name, GameObject panel, params Component[] controls)
    {
        Rect bounds = RectOf(panel.transform);
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] == null) throw new InvalidOperationException($"{name} has an unbound control at index {i}");
            Rect a = RectOf(controls[i].transform);
            Inside(name + "/" + controls[i].name, a, bounds, 5);
            if (a.width < 38 || a.height < 34)
                throw new InvalidOperationException($"{name}/{controls[i].name} is too small: {a}");
            checks++;
            for (int j = i + 1; j < controls.Length; j++)
            {
                Rect b = RectOf(controls[j].transform);
                float gapX = Mathf.Max(0, a.xMin - b.xMax, b.xMin - a.xMax);
                float gapY = Mathf.Max(0, a.yMin - b.yMax, b.yMin - a.yMax);
                if (Mathf.Max(gapX, gapY) < 8)
                    throw new InvalidOperationException($"{name}: {controls[i].name} and {controls[j].name} overlap or have less than 8 design pixels of clearance");
                checks++;
            }
        }
    }

    private static void Inside(string name, Rect inner, Rect outer, float margin)
    {
        if (inner.xMin < outer.xMin + margin || inner.xMax > outer.xMax - margin ||
            inner.yMin < outer.yMin + margin || inner.yMax > outer.yMax - margin)
            throw new InvalidOperationException($"{name} extends past its page: {inner} outside {outer}");
        checks++;
    }

    private static Rect RectOf(Transform transform)
    {
        // PrefabUtility.LoadPrefabContents is not attached to a display, so
        // GetWorldCorners returns zeroes in batch mode. Resolve the serialized
        // anchor and size values from the 1600x900 reference canvas instead.
        if (transform == root) return new Rect(-800, -450, 1600, 900);
        if (transform == null || transform.parent == null)
            throw new InvalidOperationException("A title control is outside the prefab root");
        Rect parent = RectOf(transform.parent);
        var rect = (RectTransform)transform;
        float width = parent.width * (rect.anchorMax.x - rect.anchorMin.x) + rect.sizeDelta.x;
        float height = parent.height * (rect.anchorMax.y - rect.anchorMin.y) + rect.sizeDelta.y;
        float anchorX = parent.xMin + parent.width * Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x);
        float anchorY = parent.yMin + parent.height * Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y);
        return new Rect(anchorX + rect.anchoredPosition.x - width * rect.pivot.x,
            anchorY + rect.anchoredPosition.y - height * rect.pivot.y, width, height);
    }
}

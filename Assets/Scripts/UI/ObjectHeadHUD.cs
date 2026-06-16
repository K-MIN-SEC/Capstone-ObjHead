using UnityEngine;

[DisallowMultipleComponent]
public class ObjectHeadHUD : MonoBehaviour
{
    private const int LoadoutColumns = 3;
    private const float LoadoutCellWidth = 72f;
    private const float LoadoutCellHeight = 70f;
    private const float LoadoutCellGap = 6f;
    private const float LoadoutPadding = 10f;
    private const float TeamHealthPanelWidth = 420f;
    private const float TeamHealthPanelHeight = 76f;
    private const float TeamHealthSwapDuration = 0.28f;

    [SerializeField] private TurnManager turnManager;
    [SerializeField] private bool showLegacyStatusPanel = false;
    [SerializeField] private bool showHelp = true;

    private readonly string[] skillNames = { "1 Basic", "2 Tactical", "3 Power" };
    private PlayerInventoryManager inventoryManager;
    private GUIStyle panelStyle;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle warningStyle;
    private GUIStyle selectedStyle;
    private GUIStyle bottomTimerLabelStyle;
    private GUIStyle bottomTimerNumberStyle;
    private GUIStyle loadoutKeyStyle;
    private GUIStyle loadoutStatusStyle;
    private GUIStyle loadoutEmptyStyle;
    private GUIStyle teamHealthValueStyle;
    private Texture2D whiteTexture;
    private bool teamHealthOrderInitialized;
    private int teamHealthTopTeamIndex = 1;
    private int teamHealthTargetTopTeamIndex = 1;
    private int teamHealthAnimationFromTopTeamIndex = 1;
    private int teamHealthAnimationFromBottomTeamIndex = 2;
    private float teamHealthSwapStartTime = -1f;

    private void Start()
    {
        if (turnManager == null)
        {
            turnManager = FindAny<TurnManager>();
        }

        if (inventoryManager == null)
        {
            inventoryManager = FindAny<PlayerInventoryManager>();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (showLegacyStatusPanel)
        {
            GUILayout.BeginArea(new Rect(14f, 14f, 380f, Screen.height - 28f), panelStyle);
            DrawTurnInfo();
            GUILayout.Space(8f);
            DrawSkillInfo();
            GUILayout.Space(8f);
            DrawCommonHeadSlots();
            GUILayout.Space(8f);
            DrawTeamInfo();
            if (showHelp)
            {
                GUILayout.Space(8f);
                DrawHelp();
            }
            GUILayout.EndArea();
        }

        DrawMatchResult();
        DrawBottomRemainingTimer();
        DrawTeamHealthBars();
        DrawHeadLoadoutGrid();
    }

    private void DrawTurnInfo()
    {
        GUILayout.Label("Object Head Battle", titleStyle);

        if (turnManager == null)
        {
            GUILayout.Label("TurnManager not found", warningStyle);
            return;
        }

        TurnCharacterController current = turnManager.CurrentCharacter;
        if (current == null)
        {
            GUILayout.Label("Current turn: none", warningStyle);
            return;
        }

        ObjectHeadTeamMember member = current.GetComponent<ObjectHeadTeamMember>();
        CharacterCombat combat = current.GetComponent<CharacterCombat>();
        DemoSkillSelector selector = current.GetComponent<DemoSkillSelector>();
        PowerChargeController power = current.GetComponent<PowerChargeController>();

        string playerText = member != null ? $"P{member.PlayerIndex} / Slot {member.TeamSlotIndex}" : current.name;
        string kindText = selector != null ? selector.CharacterKind.ToString() : "Unknown";
        bool settlementTimer = turnManager.IsSettlementTimeActive;
        bool residualTimer = turnManager.IsResidualTimeActive;
        float timerSeconds = settlementTimer
            ? turnManager.RemainingSettlementSeconds
            : residualTimer ? turnManager.RemainingResidualSeconds : turnManager.RemainingTurnSeconds;
        float timer01 = settlementTimer
            ? turnManager.SettlementTime01
            : residualTimer ? turnManager.ResidualTime01 : turnManager.TurnTime01;
        string timerLabel = settlementTimer ? "정산시간" : residualTimer ? "잔존시간" : "TURN TIMER";
        Color timerColor = settlementTimer
            ? new Color(1f, 0.2f, 0.2f, 0.95f)
            : residualTimer
            ? new Color(0.3f, 1f, 0.65f, 0.95f)
            : new Color(0.25f, 0.85f, 1f, 0.95f);
        GUILayout.Label($"Turn: {playerText}  {kindText}", labelStyle);
        GUILayout.Label($"Round: {turnManager.RoundSerial}", labelStyle);
        GUILayout.Label($"Phase: {turnManager.CurrentPhase}", labelStyle);
        GUILayout.Label($"{timerLabel}: {Mathf.CeilToInt(timerSeconds)}s", timerSeconds <= 5f ? warningStyle : labelStyle);
        DrawHorizontalMeter(timer01, timerColor, 348f, 8f);

        if (combat != null)
        {
            GUILayout.Label($"HP: {combat.CurrentHp}/{combat.MaxHp}", labelStyle);
        }

        if (power != null)
        {
            GUILayout.Label($"Power: {(power.CurrentPower * 100f):0}%", labelStyle);
            DrawHorizontalMeter(power.CurrentPower, new Color(1f, 0.85f, 0.2f, 0.95f), 348f, 8f);
            if (power.IsCharging)
            {
                GUILayout.Label("C : 차징 취소", selectedStyle);
            }
        }
    }

    private void DrawCommonHeadSlots()
    {
        if (turnManager == null || turnManager.CurrentCharacter == null)
        {
            return;
        }

        CommonHeadInventory inventory = GetCurrentPlayerCommonHeadInventory(turnManager.CurrentCharacter);
        CommonHeadUseController commonHeadUse =
            turnManager.CurrentCharacter.GetComponent<CommonHeadUseController>();
        if (inventory == null)
        {
            return;
        }

        GUILayout.Label("Common Heads", titleStyle);
        for (int i = 0; i < 3; i++)
        {
            CommonHeadType type = inventory.GetSlot(i);
            string state = type == CommonHeadType.None ? "Empty" : type.ToString();
            bool selected = commonHeadUse != null && commonHeadUse.SelectedSlotIndex == i;
            string prefix = selected ? "> " : "  ";
            GUIStyle style = selected ? selectedStyle : type == CommonHeadType.None ? warningStyle : labelStyle;
            DrawCommonHeadSlot($"{prefix}{i + 6}: {state}", style, selected);
        }

        if (commonHeadUse != null && commonHeadUse.HasSelectedCommonHead)
        {
            string action = commonHeadUse.SelectedType == CommonHeadType.Mobility
                ? "Charged jet jump"
                : "Charged throw";
            GUILayout.Label($"Selected: {commonHeadUse.SelectedType} / {action}", selectedStyle);
        }
    }

    private void DrawSkillInfo()
    {
        if (turnManager == null || turnManager.CurrentCharacter == null)
        {
            return;
        }

        DemoSkillSelector selector = turnManager.CurrentCharacter.GetComponent<DemoSkillSelector>();
        if (selector == null)
        {
            return;
        }

        GUILayout.Label("Skills", titleStyle);
        for (int i = 0; i < 3; i++)
        {
            int cooldown = selector.GetRemainingCooldown(i);
            string selected = selector.SelectedSkillIndex == i ? "> " : "  ";
            string state = cooldown > 0 ? $"{cooldown}" : "Ready";
            GUILayout.Label($"{selected}{skillNames[i]} - {state}", cooldown > 0 ? warningStyle : labelStyle);
        }
    }

    private void DrawTeamInfo()
    {
        if (turnManager == null)
        {
            return;
        }

        GUILayout.Label("Teams", titleStyle);
        TurnCharacterController[] characters = turnManager.Characters;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null)
            {
                continue;
            }

            ObjectHeadTeamMember member = characters[i].GetComponent<ObjectHeadTeamMember>();
            CharacterCombat combat = characters[i].GetComponent<CharacterCombat>();
            string nameText = member != null ? member.DisplayName : characters[i].name;
            string hpText = combat != null ? $"{combat.CurrentHp}/{combat.MaxHp}" : "?";
            GUILayout.Label($"{nameText}: {hpText}", combat != null && combat.IsDead ? warningStyle : labelStyle);
        }
    }

    private void DrawHelp()
    {
        GUILayout.Label("Controls", titleStyle);
        GUILayout.Label("A/D move, W jump, mouse aim", labelStyle);
        GUILayout.Label("Space charge/fire, 1/2/3 skill", labelStyle);
        GUILayout.Label("6/7/8 select common head, Space use, Esc cancel", labelStyle);
        GUILayout.Label("Tab end turn", labelStyle);
        GUILayout.Label("O/K/L/; camera, I focus character, P overview", labelStyle);
        GUILayout.Label("Mouse wheel or +/- zoom, mouse drag camera", labelStyle);
    }

    private void DrawHorizontalMeter(float value01, Color fillColor, float width, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), whiteTexture);
        GUI.color = previous;
    }

    private void DrawCommonHeadSlot(string text, GUIStyle textStyle, bool selected)
    {
        Rect rect = GUILayoutUtility.GetRect(348f, 26f, GUILayout.Width(348f), GUILayout.Height(26f));
        GUI.Box(rect, GUIContent.none);

        if (selected)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.3f, 1f, 0.65f, 1f);
            const float border = 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - border, rect.width, border), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, border, rect.height), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - border, rect.y, border, rect.height), whiteTexture);
            GUI.color = previous;
        }

        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height - 4f), text, textStyle);
    }

    private void DrawMatchResult()
    {
        if (turnManager == null || !turnManager.IsMatchOver)
        {
            return;
        }

        Rect box = new Rect(Screen.width * 0.5f - 180f, 28f, 360f, 72f);
        GUI.Box(box, GUIContent.none, panelStyle);
        GUI.Label(new Rect(box.x + 18f, box.y + 15f, box.width - 36f, 42f), $"P{turnManager.WinningPlayerIndex} Wins", titleStyle);
    }

    private void DrawBottomRemainingTimer()
    {
        if (turnManager == null || turnManager.CurrentCharacter == null || turnManager.IsMatchOver)
        {
            return;
        }

        float remainingSeconds = GetCurrentPhaseRemainingSeconds(out string phaseLabel);
        Rect box = new Rect(14f, Mathf.Max(14f, Screen.height - 82f), 190f, 62f);
        GUI.Box(box, GUIContent.none, panelStyle);
        GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 18f), $"{phaseLabel} 남은 시간", bottomTimerLabelStyle);
        GUI.Label(new Rect(box.x + 12f, box.y + 27f, box.width - 24f, 28f), $"{remainingSeconds:0.0}s", bottomTimerNumberStyle);
    }

    private void DrawHeadLoadoutGrid()
    {
        if (turnManager == null || turnManager.CurrentCharacter == null || turnManager.IsMatchOver)
        {
            return;
        }

        TurnCharacterController current = turnManager.CurrentCharacter;
        DemoSkillSelector selector = current.GetComponent<DemoSkillSelector>();
        CharacterVisual visual = current.GetComponent<CharacterVisual>();
        CommonHeadUseController commonHeadUse = current.GetComponent<CommonHeadUseController>();
        CommonHeadInventory inventory = GetCurrentPlayerCommonHeadInventory(current);

        float panelWidth = LoadoutPadding * 2f +
            LoadoutCellWidth * LoadoutColumns +
            LoadoutCellGap * (LoadoutColumns - 1);
        float panelHeight = LoadoutPadding * 2f + LoadoutCellHeight * 2f + LoadoutCellGap;
        Rect panelRect = new Rect(
            Mathf.Max(14f, Screen.width - panelWidth - 18f),
            Mathf.Max(14f, Screen.height - panelHeight - 18f),
            panelWidth,
            panelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        float startX = panelRect.x + LoadoutPadding;
        float startY = panelRect.y + LoadoutPadding;
        for (int i = 0; i < LoadoutColumns; i++)
        {
            Rect slotRect = new Rect(
                startX + i * (LoadoutCellWidth + LoadoutCellGap),
                startY,
                LoadoutCellWidth,
                LoadoutCellHeight);
            CommonHeadType type = inventory != null ? inventory.GetSlot(i) : CommonHeadType.None;
            Sprite sprite = inventory != null ? inventory.GetSlotSprite(i) : null;
            if (sprite == null && type != CommonHeadType.None)
            {
                sprite = CommonHeadItem.GetDefaultSprite(type);
            }

            bool selected = commonHeadUse != null && commonHeadUse.SelectedSlotIndex == i;
            DrawHeadLoadoutCell(
                slotRect,
                sprite,
                $"{i + 6}",
                selected,
                false,
                type == CommonHeadType.None ? "EMPTY" : string.Empty,
                type == CommonHeadType.None,
                0f);
        }

        float basicRowY = startY + LoadoutCellHeight + LoadoutCellGap;
        bool commonHeadSelected = commonHeadUse != null && commonHeadUse.HasSelectedCommonHead;
        for (int i = 0; i < LoadoutColumns; i++)
        {
            Rect slotRect = new Rect(
                startX + i * (LoadoutCellWidth + LoadoutCellGap),
                basicRowY,
                LoadoutCellWidth,
                LoadoutCellHeight);
            Sprite sprite = visual != null ? visual.GetSkillHeadSprite(i) : null;
            int cooldown = selector != null ? selector.GetRemainingCooldown(i) : 0;
            bool selected = !commonHeadSelected && selector != null && selector.SelectedSkillIndex == i;
            float cooldownFill01 = CalculateCooldownFill01(selector, i, cooldown);
            DrawHeadLoadoutCell(
                slotRect,
                sprite,
                $"{i + 1}",
                selected,
                cooldown > 0,
                cooldown > 0 ? $"{cooldown}" : string.Empty,
                sprite == null,
                cooldownFill01);
        }
    }

    private void DrawTeamHealthBars()
    {
        if (turnManager == null || turnManager.Characters == null || turnManager.Characters.Length == 0)
        {
            return;
        }

        TeamHealthTotal teamOne = CalculateTeamHealthTotal(1);
        TeamHealthTotal teamTwo = CalculateTeamHealthTotal(2);
        if (teamOne.maxHp <= 0 && teamTwo.maxHp <= 0)
        {
            return;
        }

        Rect panelRect = new Rect(
            Mathf.Max(14f, Screen.width * 0.5f - TeamHealthPanelWidth * 0.5f),
            Mathf.Max(14f, Screen.height - TeamHealthPanelHeight - 18f),
            TeamHealthPanelWidth,
            TeamHealthPanelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        UpdateTeamHealthOrder(teamOne, teamTwo);

        float rowX = panelRect.x + 14f;
        float rowWidth = panelRect.width - 28f;
        float topY = panelRect.y + 12f;
        float bottomY = panelRect.y + 42f;
        if (IsTeamHealthSwapAnimating(out float swapT))
        {
            DrawTeamHealthRow(
                new Rect(rowX, Mathf.Lerp(topY, bottomY, swapT), rowWidth, 22f),
                GetTeamHealthTotal(teamHealthAnimationFromTopTeamIndex, teamOne, teamTwo),
                ObjectHeadTeamColors.GetColor(teamHealthAnimationFromTopTeamIndex));
            DrawTeamHealthRow(
                new Rect(rowX, Mathf.Lerp(bottomY, topY, swapT), rowWidth, 22f),
                GetTeamHealthTotal(teamHealthAnimationFromBottomTeamIndex, teamOne, teamTwo),
                ObjectHeadTeamColors.GetColor(teamHealthAnimationFromBottomTeamIndex));
            return;
        }

        int bottomTeamIndex = GetOtherTeamIndex(teamHealthTopTeamIndex);
        DrawTeamHealthRow(
            new Rect(rowX, topY, rowWidth, 22f),
            GetTeamHealthTotal(teamHealthTopTeamIndex, teamOne, teamTwo),
            ObjectHeadTeamColors.GetColor(teamHealthTopTeamIndex));
        DrawTeamHealthRow(
            new Rect(rowX, bottomY, rowWidth, 22f),
            GetTeamHealthTotal(bottomTeamIndex, teamOne, teamTwo),
            ObjectHeadTeamColors.GetColor(bottomTeamIndex));
    }

    private float GetCurrentPhaseRemainingSeconds(out string phaseLabel)
    {
        if (turnManager.IsSettlementTimeActive)
        {
            phaseLabel = "정산시간";
            return Mathf.Max(0f, turnManager.RemainingSettlementSeconds);
        }

        if (turnManager.IsResidualTimeActive)
        {
            phaseLabel = "잔존시간";
            return Mathf.Max(0f, turnManager.RemainingResidualSeconds);
        }

        phaseLabel = "TURN TIMER";
        return Mathf.Max(0f, turnManager.RemainingTurnSeconds);
    }

    private CommonHeadInventory GetCurrentPlayerCommonHeadInventory(TurnCharacterController current)
    {
        if (current == null)
        {
            return null;
        }

        if (inventoryManager == null)
        {
            inventoryManager = FindAny<PlayerInventoryManager>();
        }

        ObjectHeadTeamMember member = current.GetComponent<ObjectHeadTeamMember>();
        return member != null && inventoryManager != null
            ? inventoryManager.GetInventory(member.PlayerIndex)
            : null;
    }

    private static float CalculateCooldownFill01(DemoSkillSelector selector, int skillIndex, int cooldown)
    {
        if (selector == null || cooldown <= 0)
        {
            return 0f;
        }

        int duration = selector.GetCooldownDuration(skillIndex);
        return duration > 0
            ? Mathf.Clamp01((duration - cooldown + 1) / (float)duration)
            : 1f;
    }

    private TeamHealthTotal CalculateTeamHealthTotal(int playerIndex)
    {
        TeamHealthTotal total = new TeamHealthTotal();
        TurnCharacterController[] characters = turnManager.Characters;
        for (int i = 0; i < characters.Length; i++)
        {
            TurnCharacterController character = characters[i];
            if (character == null)
            {
                continue;
            }

            ObjectHeadTeamMember member = character.GetComponent<ObjectHeadTeamMember>();
            if (member == null || member.PlayerIndex != playerIndex)
            {
                continue;
            }

            CharacterCombat combat = character.GetComponent<CharacterCombat>();
            if (combat == null)
            {
                continue;
            }

            total.currentHp += Mathf.Max(0, combat.CurrentHp);
            total.maxHp += Mathf.Max(0, combat.MaxHp);
        }

        return total;
    }

    private void UpdateTeamHealthOrder(TeamHealthTotal teamOne, TeamHealthTotal teamTwo)
    {
        int desiredTopTeamIndex = GetDesiredTopTeamIndex(teamOne, teamTwo);
        if (!teamHealthOrderInitialized)
        {
            teamHealthOrderInitialized = true;
            teamHealthTopTeamIndex = desiredTopTeamIndex;
            teamHealthTargetTopTeamIndex = desiredTopTeamIndex;
            return;
        }

        if (desiredTopTeamIndex == teamHealthTargetTopTeamIndex)
        {
            return;
        }

        if (teamHealthSwapStartTime >= 0f && IsTeamHealthSwapAnimating(out _))
        {
            CompleteTeamHealthSwap();
        }

        teamHealthAnimationFromTopTeamIndex = teamHealthTopTeamIndex;
        teamHealthAnimationFromBottomTeamIndex = GetOtherTeamIndex(teamHealthTopTeamIndex);
        teamHealthTargetTopTeamIndex = desiredTopTeamIndex;
        teamHealthSwapStartTime = Time.unscaledTime;
    }

    private bool IsTeamHealthSwapAnimating(out float t)
    {
        t = 0f;
        if (teamHealthSwapStartTime < 0f)
        {
            return false;
        }

        float rawT = Mathf.Clamp01((Time.unscaledTime - teamHealthSwapStartTime) / TeamHealthSwapDuration);
        t = rawT * rawT * (3f - 2f * rawT);
        if (rawT < 1f)
        {
            return true;
        }

        CompleteTeamHealthSwap();
        t = 1f;
        return false;
    }

    private void CompleteTeamHealthSwap()
    {
        teamHealthTopTeamIndex = teamHealthTargetTopTeamIndex;
        teamHealthSwapStartTime = -1f;
    }

    private int GetDesiredTopTeamIndex(TeamHealthTotal teamOne, TeamHealthTotal teamTwo)
    {
        if (teamOne.currentHp > teamTwo.currentHp)
        {
            return 1;
        }

        if (teamTwo.currentHp > teamOne.currentHp)
        {
            return 2;
        }

        return teamHealthOrderInitialized ? teamHealthTopTeamIndex : 1;
    }

    private static int GetOtherTeamIndex(int teamIndex)
    {
        return teamIndex == 1 ? 2 : 1;
    }

    private static TeamHealthTotal GetTeamHealthTotal(
        int teamIndex,
        TeamHealthTotal teamOne,
        TeamHealthTotal teamTwo)
    {
        return teamIndex == 1 ? teamOne : teamTwo;
    }

    private void DrawTeamHealthRow(Rect rowRect, TeamHealthTotal total, Color teamColor)
    {
        Rect barRect = new Rect(rowRect.x, rowRect.y + 2f, rowRect.width, rowRect.height - 4f);
        DrawFixedHorizontalMeter(barRect, total.Fill01, teamColor);
        GUI.Label(barRect, $"{total.currentHp}/{total.maxHp}", teamHealthValueStyle);
    }

    private void DrawFixedHorizontalMeter(Rect rect, float fill01, Color fillColor)
    {
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, whiteTexture);
        Color tintedFill = fillColor;
        tintedFill.a = 0.82f;
        GUI.color = tintedFill;
        GUI.DrawTexture(
            new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill01), rect.height),
            whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.22f);
        DrawRectBorder(rect, 1f);
        GUI.color = previous;
    }

    private void DrawHeadLoadoutCell(
        Rect rect,
        Sprite sprite,
        string keyText,
        bool selected,
        bool disabled,
        string statusText,
        bool empty,
        float cooldownFill01)
    {
        Color previous = GUI.color;
        GUI.color = empty
            ? new Color(0f, 0f, 0f, 0.32f)
            : new Color(0f, 0f, 0f, 0.58f);
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        DrawRectBorder(rect, 1f);

        Rect iconRect = new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 22f);
        if (sprite != null)
        {
            DrawSprite(iconRect, sprite, disabled ? new Color(1f, 1f, 1f, 0.72f) : Color.white);
        }

        if (cooldownFill01 > 0f)
        {
            float fillHeight = rect.height * Mathf.Clamp01(cooldownFill01);
            Rect fillRect = new Rect(rect.x, rect.yMax - fillHeight, rect.width, fillHeight);
            GUI.color = new Color(1f, 0.86f, 0f, 0.6f);
            GUI.DrawTexture(fillRect, whiteTexture);
        }

        if (selected)
        {
            GUI.color = new Color(0.3f, 1f, 0.65f, 1f);
            DrawRectBorder(rect, 3f);
        }

        Rect keyRect = new Rect(rect.x + 5f, rect.y + 5f, 22f, 18f);
        GUI.color = new Color(0f, 0f, 0f, 0.68f);
        GUI.DrawTexture(keyRect, whiteTexture);
        GUI.color = Color.white;
        GUI.Label(keyRect, keyText, loadoutKeyStyle);

        if (!string.IsNullOrEmpty(statusText))
        {
            Rect statusRect = new Rect(rect.x + 4f, rect.yMax - 18f, rect.width - 8f, 16f);
            GUI.Label(statusRect, statusText, empty ? loadoutEmptyStyle : loadoutStatusStyle);
        }

        GUI.color = previous;
    }

    private void DrawSprite(Rect bounds, Sprite sprite, Color tint)
    {
        Texture2D texture = sprite != null ? sprite.texture : null;
        if (texture == null)
        {
            return;
        }

        Rect textureRect = sprite.textureRect;
        Rect texCoords = new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);
        Rect fittedRect = FitRect(bounds, textureRect.width / Mathf.Max(1f, textureRect.height));

        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(fittedRect, texture, texCoords, true);
        GUI.color = previous;
    }

    private void DrawRectBorder(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), whiteTexture);
    }

    private static Rect FitRect(Rect bounds, float aspect)
    {
        float safeAspect = Mathf.Max(0.01f, aspect);
        float width = bounds.width;
        float height = width / safeAspect;
        if (height > bounds.height)
        {
            height = bounds.height;
            width = height * safeAspect;
        }

        return new Rect(
            bounds.x + (bounds.width - width) * 0.5f,
            bounds.y + (bounds.height - height) * 0.5f,
            width,
            height);
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        whiteTexture = Texture2D.whiteTexture;
        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10),
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(1f, 0.75f, 0.25f, 1f) }
        };

        selectedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.3f, 1f, 0.65f, 1f) }
        };

        bottomTimerLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.75f, 0.9f, 1f, 1f) }
        };

        bottomTimerNumberStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        loadoutKeyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        loadoutStatusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.78f, 0.25f, 1f) }
        };

        loadoutEmptyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 1f, 1f, 0.45f) }
        };

        teamHealthValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }

    private struct TeamHealthTotal
    {
        public int currentHp;
        public int maxHp;
        public float Fill01 => maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}


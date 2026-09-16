using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Only binds match state to designer-authored UI. Never sets layout or creates widgets.</summary>
public sealed class ObjectHeadBattleScreen : MonoBehaviour
{
    public TurnManager turns;
    public Text turnText;
    public Text timerText;
    public Text[] teamHealthTexts;
    public Image[] teamHealthFills;
    public Image[] skillIcons;
    public Text[] skillTexts;
    public Button[] skillButtons;
    public Image[] commonIcons;
    public Text[] commonTexts;
    public Button[] commonButtons;
    public Slider powerBar;
    public Button endTurnButton;
    public Button menuButton;
    public GameObject menuPanel;
    public Button resumeButton;
    public Button exitButton;
    public GameObject resultPanel;
    public Text resultTitle;
    public Text resultDetail;
    public Button replayButton;
    public Button resultExitButton;
    public ObjectHeadContent content;
    public ObjectHeadLocalizationTable localization;
    private ObjectHeadLanguage language;
    private ObjectHeadNetworkManager network;
    private bool interrupted;
    private bool navigating;
    private ObjectHeadLanguage Language => language;
    private string L(string key) => localization.Get(key, Language);

    private void Start()
    {
        network = ObjectHeadNetworkManager.Instance;
        language = (ObjectHeadLanguage)PlayerPrefs.GetInt("ObjectHead.Language", 0);
        foreach (var label in GetComponentsInChildren<ObjectHeadLocalizedLabel>(true)) label.Apply(localization, language);
        menuPanel.SetActive(false);
        resultPanel.SetActive(false);
        menuButton.onClick.AddListener(() => menuPanel.SetActive(true));
        resumeButton.onClick.AddListener(() => menuPanel.SetActive(false));
        exitButton.onClick.AddListener(Exit);
        resultExitButton.onClick.AddListener(Exit);
        replayButton.onClick.AddListener(Replay);
        endTurnButton.onClick.AddListener(EndTurn);
        for (int i = 0; i < skillButtons.Length; i++)
        {
            int slot = i;
            skillButtons[i].onClick.AddListener(() => { if (CanControl()) { turns.CurrentCharacter.GetComponent<CommonHeadUseController>()?.CancelSelectionAndRestoreUniqueHead(); turns.CurrentCharacter.GetComponent<DemoSkillSelector>().SetSkillIndex(slot); } });
        }
        for (int i = 0; i < commonButtons.Length; i++)
        {
            int slot = i;
            commonButtons[i].onClick.AddListener(() => { if (CanControl()) turns.CurrentCharacter.GetComponent<CommonHeadUseController>()?.TrySelectCommonHeadSlot(slot); });
        }
        if (network != null) network.SessionInterrupted += Interrupted;
    }

    private void OnDestroy() { if (network != null) network.SessionInterrupted -= Interrupted; }
    private bool Online => GameStartData.Instance != null && !GameStartData.Instance.localMatch;
    private bool CanControl() => !interrupted && !menuPanel.activeSelf && turns != null && turns.CurrentCharacter != null && !turns.IsMatchOver &&
        (!Online || GameStartData.Instance.players.Any(p => p.userId == network.LocalUserId && p.playerIndex == turns.CurrentPlayerIndex));

    private void Update()
    {
        if (turns == null) return;
        TurnCharacterController current = turns.CurrentCharacter;
        turnText.text = current != null ? string.Format(L("turn_player"), turns.CurrentPlayerIndex, turns.RoundSerial) : L("loading_battle");
        float seconds = turns.IsSettlementTimeActive ? turns.RemainingSettlementSeconds : turns.IsResidualTimeActive ? turns.RemainingResidualSeconds : turns.RemainingTurnSeconds;
        timerText.text = string.Format(L(turns.IsSettlementTimeActive ? "timer_settlement" : turns.IsResidualTimeActive ? "timer_residual" : "timer_turn"), seconds);
        timerText.color = seconds <= 5 ? new Color32(255, 172, 122, 255) : new Color32(255, 241, 216, 255);
        for (int i = 0; i < teamHealthTexts.Length; i++)
        {
            var team = turns.Characters.Where(c => c != null && c.GetComponent<ObjectHeadTeamMember>().PlayerIndex == i + 1).Select(c => c.GetComponent<CharacterCombat>()).ToArray();
            teamHealthTexts[i].transform.parent.gameObject.SetActive(team.Length > 0);
            if (team.Length == 0) continue;
            int hp = team.Sum(c => Math.Max(0, c.CurrentHp)), max = team.Sum(c => c.MaxHp);
            string alliance = ObjectHeadMatchRules.IsTeamMatch ? string.Format(L("alliance_label"), ObjectHeadMatchRules.Alliance(i+1)) + " · " : "";
            teamHealthTexts[i].text = $"{alliance}P{i + 1}  {hp}/{max}";
            teamHealthFills[i].fillAmount = max > 0 ? hp / (float)max : 0;
            teamHealthFills[i].color = ObjectHeadTeamColors.GetColor(i + 1);
        }
        bool canControl = CanControl();
        endTurnButton.interactable = canControl && !navigating;
        if (current != null)
        {
            var selector = current.GetComponent<DemoSkillSelector>();
            var visual = current.GetComponent<CharacterVisual>();
            var use = current.GetComponent<CommonHeadUseController>();
            var power = current.GetComponent<PowerChargeController>();
            var inventories = FindAnyObjectByType<PlayerInventoryManager>();
            var inventory = inventories != null ? inventories.GetInventory(turns.CurrentPlayerIndex) : null;
            powerBar.value = power != null ? power.CurrentPower : 0;
            for (int i = 0; i < skillButtons.Length; i++)
            {
                skillIcons[i].sprite = visual.GetSkillHeadSprite(i);
                skillIcons[i].enabled = skillIcons[i].sprite != null;
                int cooldown = selector.GetRemainingCooldown(i);
                skillTexts[i].text = cooldown > 0 ? string.Format(L("cooldown_rounds"), cooldown) : (i + 1).ToString();
                skillButtons[i].interactable = canControl && cooldown == 0 && !turns.ActionUsedThisTurn;
                skillButtons[i].GetComponent<Outline>().enabled = selector.SelectedSkillIndex == i && !use.HasSelectedCommonHead;
                var type = inventory != null ? inventory.GetSlot(i) : CommonHeadType.None;
                commonIcons[i].sprite = inventory != null ? inventory.GetSlotSprite(i) : null;
                commonIcons[i].enabled = commonIcons[i].sprite != null;
                commonIcons[i].color = Color.white;
                commonTexts[i].text = type == CommonHeadType.None ? L("empty_slot") : (i + 6).ToString();
                commonButtons[i].interactable = canControl && type != CommonHeadType.None && !turns.ActionUsedThisTurn;
            }
        }
        if (turns.IsMatchOver && !interrupted)
        {
            resultPanel.SetActive(true);
            resultTitle.text = turns.WinningPlayerIndex > 0 ? string.Format(L(ObjectHeadMatchRules.IsTeamMatch ? "team_winner" : "match_winner"), turns.WinningPlayerIndex) : L("match_draw");
            resultDetail.text = L(Online ? "result_online" : "result_local");
            replayButton.gameObject.SetActive(!Online || network.IsHost);
            replayButton.interactable = !navigating;
        }
    }

    private void Interrupted(string key)
    {
        interrupted = true;
        resultPanel.SetActive(true);
        resultTitle.text = L(key);
        resultDetail.text = L("disconnect_return");
        replayButton.gameObject.SetActive(false);
        foreach (var c in turns.Characters) if (c != null) c.SetControlEnabled(false);
    }

    private void EndTurn()
    {
        if (!CanControl()) return;
        if (Online && !network.IsHost) FindAnyObjectByType<ObjectHeadGameplayBridge>()?.RequestEndTurn();
        else turns.EndCurrentTurn();
    }

    private async void Exit()
    {
        if (navigating) return;
        navigating = true;
        try { if (network != null) await network.DisconnectAsync(); }
        catch (Exception error) { Debug.LogWarning(error.Message); }
        GameStartData.Clear();
        SceneManager.LoadScene(network.Config.TitleSceneName);
    }

    private async void Replay()
    {
        if (navigating) return;
        navigating = true;
        try
        {
            if (Online) await network.ReturnToLobbyAsync();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        catch (Exception error) { navigating = false; resultDetail.text = L("connection_help"); Debug.LogWarning(error.Message); }
    }
}

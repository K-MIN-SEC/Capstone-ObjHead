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
    public Button trainingResetButton;
    public Button trainingTimerButton;
    public Button trainingHealButton;
    public Button trainingSkipButton;
    public Button trainingHeadsButton;
    public ObjectHeadHeadInventoryPanel headInventory;
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
    [SerializeField, Min(.02f)] private float statusRefreshSeconds=.1f;
    private float nextStatusRefresh;
    [Header("HUD motion (visual only)")]
    [SerializeField,Min(.01f)] private float healthEaseSeconds=.24f;
    [SerializeField,Min(.01f)] private float turnAccentSeconds=.45f;
    [SerializeField,Range(0,.15f)] private float turnAccentScale=.07f;
    private float[] targetHealth;
    private bool healthInitialized;
    private int accentTurn=-1;
    private float accentUntil;
    private Vector3 authoredTurnScale;
    public bool BlocksCameraInput => interrupted || ObjectHeadHeadInventoryPanel.AnyOpen || (menuPanel != null && menuPanel.activeSelf) || (resultPanel != null && resultPanel.activeSelf);
    private bool navigating;
    private ObjectHeadLanguage Language => language;
    private string L(string key) => localization.Get(key, Language);

    private void Start()
    {
        targetHealth=new float[teamHealthFills.Length];authoredTurnScale=turnText.transform.localScale;
        network = ObjectHeadNetworkManager.Instance;
        language = (ObjectHeadLanguage)PlayerPrefs.GetInt("ObjectHead.Language", 0);
        foreach (var label in GetComponentsInChildren<ObjectHeadLocalizedLabel>(true)) label.Apply(localization, language);
        menuPanel.SetActive(false);
        resultPanel.SetActive(false);
        menuButton.onClick.AddListener(() => menuPanel.SetActive(true));
        resumeButton.onClick.AddListener(() => menuPanel.SetActive(false));
        exitButton.onClick.AddListener(Exit);
        bool training=FindAnyObjectByType<ObjectHeadTraining>()!=null;
        if(trainingHeadsButton!=null){trainingHeadsButton.gameObject.SetActive(training);trainingHeadsButton.onClick.AddListener(OpenTrainingHeads);}
        if(trainingResetButton!=null){trainingResetButton.gameObject.SetActive(training);trainingResetButton.onClick.AddListener(ObjectHeadTraining.ResetArena);}
        if(trainingTimerButton!=null){trainingTimerButton.gameObject.SetActive(training);trainingTimerButton.onClick.AddListener(ObjectHeadTraining.ToggleTimer);}
        if(trainingHealButton!=null){trainingHealButton.gameObject.SetActive(training);trainingHealButton.onClick.AddListener(ObjectHeadTraining.HealAll);}
        if(trainingSkipButton!=null){trainingSkipButton.gameObject.SetActive(training);trainingSkipButton.onClick.AddListener(ObjectHeadTraining.SkipTurn);}
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
    public void OpenTrainingHeads()
    {
        if(!ObjectHeadTraining.Enabled || headInventory==null || turns.CurrentCharacter==null || !turns.CanCharacterFire(turns.CurrentCharacter))return;
        var actor=turns.CurrentCharacter;menuPanel.SetActive(false);actor.GetComponent<PowerChargeController>()?.CancelCharge();
        headInventory.Open(ObjectHeadHeadChoice.Catalog(content,localization,Language),choice=>
        {if(actor==turns.CurrentCharacter && turns.CanCharacterFire(actor))actor.GetComponent<DemoSkillSelector>().SelectTrainingHead(choice);});
        SetInventoryText("training_all_heads","inventory_training_help");
    }
    public void OpenGourdHeads(TurnCharacterController actor)
    {
        if(headInventory==null || actor==null || actor!=turns.CurrentCharacter || !turns.CanLocalUserControl(actor) || !turns.CanCharacterFire(actor))return;
        var gourd=actor.GetComponent<ObjectHeadGourd>()??actor.gameObject.AddComponent<ObjectHeadGourd>();
        actor.GetComponent<PowerChargeController>()?.CancelCharge();
        headInventory.Open(gourd.Choices,c=>gourd.Select(c.id),gourd.Remaining);
        SetInventoryText("gourd_inventory","gourd_inventory_help");
    }
    private void SetInventoryText(string titleKey,string helpKey)
    {
        headInventory.title.text=L(titleKey);
        var help=headInventory.transform.Find("Help")?.GetComponent<Text>();if(help!=null)help.text=L(helpKey);
    }
    private bool Online => GameStartData.Instance != null && !GameStartData.Instance.localMatch;
    private bool CanControl() => !interrupted && !menuPanel.activeSelf && turns != null && turns.CurrentCharacter != null && !turns.IsMatchOver &&
        turns.CanLocalUserControl(turns.CurrentCharacter);

    private void Update()
    {
        if (turns == null) return;
        if(accentTurn!=turns.TurnSerial){accentTurn=turns.TurnSerial;accentUntil=Time.unscaledTime+turnAccentSeconds;}
        float accent=Mathf.Clamp01((accentUntil-Time.unscaledTime)/turnAccentSeconds);
        turnText.transform.localScale=authoredTurnScale*(1+turnAccentScale*Mathf.Sin(accent*Mathf.PI));
        for(int i=0;i<teamHealthFills.Length;i++)
            if(healthInitialized)teamHealthFills[i].fillAmount=Mathf.Lerp(teamHealthFills[i].fillAmount,targetHealth[i],1-Mathf.Exp(-Time.unscaledDeltaTime/healthEaseSeconds));
        // Charge feedback stays per-frame; text/layout and inventory scans need only 10 Hz.
        if(turns.CurrentCharacter!=null)powerBar.value=turns.CurrentCharacter.GetComponent<PowerChargeController>()?.CurrentPower??0;
        if(Time.unscaledTime<nextStatusRefresh)return;
        nextStatusRefresh=Time.unscaledTime+statusRefreshSeconds;
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
            targetHealth[i] = max > 0 ? hp / (float)max : 0;
            if(!healthInitialized)teamHealthFills[i].fillAmount=targetHealth[i];
            teamHealthFills[i].color = ObjectHeadTeamColors.GetColor(i + 1);
        }
        healthInitialized=true;
        bool canControl = CanControl();
        if(trainingHeadsButton!=null)trainingHeadsButton.interactable=ObjectHeadTraining.Enabled && current!=null && turns.CanCharacterFire(current);
        endTurnButton.interactable = canControl && !navigating;
        if (current != null)
        {
            var selector = current.GetComponent<DemoSkillSelector>();
            var visual = current.GetComponent<CharacterVisual>();
            var use = current.GetComponent<CommonHeadUseController>();
            var power = current.GetComponent<PowerChargeController>();
            var gourd=current.GetComponent<ObjectHeadGourd>();
            var inventories = FindAnyObjectByType<PlayerInventoryManager>();
            var inventory = inventories != null ? inventories.GetInventory(turns.CurrentPlayerIndex) : null;
            powerBar.value = power != null ? power.CurrentPower : 0;
            for (int i = 0; i < skillButtons.Length; i++)
            {
                skillIcons[i].sprite = i==0 && selector.HasTrainingSelection?selector.TrainingIcon:visual.GetSkillHeadSprite(i);
                if(i==2 && gourd?.SelectedChoice!=null)skillIcons[i].sprite=gourd.SelectedChoice.icon;
                skillIcons[i].enabled = skillIcons[i].sprite != null;
                int cooldown = selector.GetRemainingCooldown(i);
                skillTexts[i].text = cooldown > 0 ? string.Format(L("cooldown_rounds"), cooldown) : (i + 1).ToString();
                skillButtons[i].interactable = canControl && cooldown == 0 && !turns.ActionUsedThisTurn;
                skillButtons[i].GetComponent<Outline>().enabled = selector.SelectedSkillIndex == i && !use.HasSelectedCommonHead;
            }
            for (int i = 0; i < commonButtons.Length; i++)
            {
                var type = inventory != null ? inventory.GetSlot(i) : CommonHeadType.None;
                commonIcons[i].sprite = inventory != null ? inventory.GetSlotSprite(i) : null;
                commonIcons[i].enabled = commonIcons[i].sprite != null;
                commonIcons[i].color = Color.white;
                commonTexts[i].text = type == CommonHeadType.None ? L("empty_slot") : CommonHeadUseController.SlotLabel(i);
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
        if(ObjectHeadCommonAuthority.IsDedicatedMatch)FindAnyObjectByType<ObjectHeadDedicatedGameplay>()?.RequestEndTurn();
        else if (Online && !network.IsHost) FindAnyObjectByType<ObjectHeadGameplayBridge>()?.RequestEndTurn();
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

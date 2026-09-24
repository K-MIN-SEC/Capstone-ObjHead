using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectHeadFrontEnd : MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public GameObject home,play,quick,create,find,settings;
    public Button playButton,createButton,settingsButton,quickButton,findButton,trainingButton,backButton;
    [Header("Editable back-button positions")]
    public Vector2 compactBackPosition = new Vector2(-260, -356);
    public Vector2 roomBrowserBackPosition = new Vector2(-540, -347);
    public ObjectHeadSettingsPanel settingsController;
    private GameObject current;
    private void Awake()
    {
        current=home;
        playButton.onClick.AddListener(()=>Show(play));
        createButton.onClick.AddListener(()=>Show(create));
        settingsButton.onClick.AddListener(()=>{Show(settings);settingsController.Load();});
        quickButton.onClick.AddListener(()=>Show(quick));
        findButton.onClick.AddListener(()=>Show(find));
        trainingButton.onClick.AddListener(title.StartTraining);
        backButton.onClick.AddListener(Back);
        Show(home);
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape) && !title.InLobby && current!=home)Back();
    }
    private void Back()
    {
        if(current==settings)settingsController.RevertPending();
        Show(current==quick || current==find ? play : home);
    }
    public void Show(GameObject panel){current=panel;Sync(title.InLobby);}
    public void Sync(bool inLobby)
    {
        if(current==null)current=home;
        foreach(var panel in new[]{home,play,quick,create,find,settings})if(panel!=null)panel.SetActive(!inLobby && panel==current);
        if(title.menuOnlyDecorations!=null)
            foreach(var decoration in title.menuOnlyDecorations)
                if(decoration!=null)decoration.SetActive(!inLobby && current!=find);
        if(backButton!=null)
        {
            ((RectTransform)backButton.transform).anchoredPosition=current==find?roomBrowserBackPosition:compactBackPosition;
            backButton.gameObject.SetActive(!inLobby && current!=home);
        }
    }
}

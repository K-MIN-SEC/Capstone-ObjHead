using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectHeadFrontEnd : MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public GameObject home,play,quick,create,find,settings;
    public Button playButton,createButton,settingsButton,quickButton,findButton,trainingButton,backButton;
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
        backButton.onClick.AddListener(()=>{settingsController.RevertPending();Show(home);});
        Show(home);
    }
    public void Show(GameObject panel){current=panel;Sync(title.InLobby);}
    public void Sync(bool inLobby)
    {
        if(current==null)current=home;
        foreach(var panel in new[]{home,play,quick,create,find,settings})if(panel!=null)panel.SetActive(!inLobby && panel==current);
        if(backButton!=null)backButton.gameObject.SetActive(!inLobby && current!=home);
    }
}

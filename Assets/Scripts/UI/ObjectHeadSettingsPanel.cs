using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable] public struct ObjectHeadResolution { public int width,height; }
public sealed class ObjectHeadSettingsPanel : MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public Dropdown resolution,windowMode,language;
    public Text resolutionValue,windowModeValue,languageValue;
    public Button resolutionPrev,resolutionNext,windowModePrev,windowModeNext,languagePrev,languageNext;
    public Slider bgm,sfx;
    public Text bgmValue,sfxValue;
    public Button apply,confirm;
    public Text status;
    public ObjectHeadResolution[] resolutions;
    public float confirmationSeconds=12;
    private int oldWidth,oldHeight;
    private FullScreenMode oldMode;
    private float deadline;
    private bool pending;
    private void Start()
    {
        apply.onClick.AddListener(Apply);confirm.onClick.AddListener(Confirm);
        bgm.onValueChanged.AddListener(v=>{ObjectHeadAudio.BgmVolume=v;RefreshVolumeLabels();});
        sfx.onValueChanged.AddListener(v=>{ObjectHeadAudio.SfxVolume=v;RefreshVolumeLabels();});
        language.onValueChanged.AddListener(v=>{title.SetLanguage((ObjectHeadLanguage)v);Localize();});
        resolutionPrev?.onClick.AddListener(()=>Cycle(resolution,-1));
        resolutionNext?.onClick.AddListener(()=>Cycle(resolution,1));
        windowModePrev?.onClick.AddListener(()=>Cycle(windowMode,-1));
        windowModeNext?.onClick.AddListener(()=>Cycle(windowMode,1));
        languagePrev?.onClick.AddListener(()=>Cycle(language,-1));
        languageNext?.onClick.AddListener(()=>Cycle(language,1));
    }
    public void Load()
    {
        resolution.ClearOptions();resolution.AddOptions(resolutions.Select(r=>$"{r.width} × {r.height}").ToList());
        int at=Array.FindIndex(resolutions,r=>r.width==Screen.width && r.height==Screen.height);
        resolution.SetValueWithoutNotify(Mathf.Max(0,at));
        windowMode.SetValueWithoutNotify(Screen.fullScreenMode==FullScreenMode.Windowed?0:Screen.fullScreenMode==FullScreenMode.FullScreenWindow?1:2);
        language.SetValueWithoutNotify((int)title.Language);
        bgm.SetValueWithoutNotify(ObjectHeadAudio.BgmVolume);sfx.SetValueWithoutNotify(ObjectHeadAudio.SfxVolume);
        Localize();RefreshVolumeLabels();RefreshSelectorLabels();confirm.gameObject.SetActive(pending);
    }
    private void RefreshVolumeLabels()
    {
        if(bgmValue!=null)bgmValue.text=Mathf.RoundToInt(bgm.value*100f)+"%";
        if(sfxValue!=null)sfxValue.text=Mathf.RoundToInt(sfx.value*100f)+"%";
    }
    private void Localize()
    {
        int selected=windowMode.value;windowMode.ClearOptions();windowMode.AddOptions(new System.Collections.Generic.List<string>{title.Translate("windowed"),title.Translate("borderless"),title.Translate("fullscreen")});windowMode.SetValueWithoutNotify(selected);
        language.ClearOptions();language.AddOptions(new System.Collections.Generic.List<string>{"한국어","English"});language.SetValueWithoutNotify((int)title.Language);
        RefreshSelectorLabels();
    }
    private void Cycle(Dropdown dropdown,int step)
    {
        if(dropdown.options.Count==0)return;
        int next=(dropdown.value+step+dropdown.options.Count)%dropdown.options.Count;
        dropdown.SetValueWithoutNotify(next);
        if(dropdown==language){title.SetLanguage((ObjectHeadLanguage)next);Localize();}
        RefreshSelectorLabels();
    }
    private void RefreshSelectorLabels()
    {
        SetSelectorLabel(resolution,resolutionValue);
        SetSelectorLabel(windowMode,windowModeValue);
        SetSelectorLabel(language,languageValue);
    }
    private static void SetSelectorLabel(Dropdown dropdown,Text label)
    {
        if(dropdown==null || label==null || dropdown.options.Count==0)return;
        label.text=dropdown.options[Mathf.Clamp(dropdown.value,0,dropdown.options.Count-1)].text;
    }
    private void Apply()
    {
        if(pending)RevertPending();
        oldWidth=Screen.width;oldHeight=Screen.height;oldMode=Screen.fullScreenMode;
        var choice=resolutions[resolution.value];
        var mode=windowMode.value==0?FullScreenMode.Windowed:windowMode.value==1?FullScreenMode.FullScreenWindow:FullScreenMode.ExclusiveFullScreen;
        Screen.SetResolution(choice.width,choice.height,mode);
        pending=true;deadline=Time.unscaledTime+confirmationSeconds;confirm.gameObject.SetActive(true);
        ObjectHeadAudio.Save();
    }
    private void Update()
    {
        if(!pending)return;
        status.text=string.Format(title.Translate("confirm_display"),Mathf.CeilToInt(deadline-Time.unscaledTime));
        if(Time.unscaledTime>=deadline)RevertPending();
    }
    private void Confirm()
    {
        pending=false;confirm.gameObject.SetActive(false);status.text=title.Translate("settings_saved");
        PlayerPrefs.SetInt("Display.Width",Screen.width);PlayerPrefs.SetInt("Display.Height",Screen.height);PlayerPrefs.SetInt("Display.Mode",(int)Screen.fullScreenMode);PlayerPrefs.Save();
    }
    public void RevertPending()
    {
        if(!pending)return;
        Screen.SetResolution(oldWidth,oldHeight,oldMode);pending=false;confirm.gameObject.SetActive(false);status.text="";
    }
    private void OnDisable(){RevertPending();ObjectHeadAudio.Save();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreDisplay()
    {
        if(Application.isBatchMode || !PlayerPrefs.HasKey("Display.Width"))return;
        Screen.SetResolution(PlayerPrefs.GetInt("Display.Width"),PlayerPrefs.GetInt("Display.Height"),(FullScreenMode)PlayerPrefs.GetInt("Display.Mode",3));
    }
}

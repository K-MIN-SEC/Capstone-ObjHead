using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class ObjectHeadHeadChoice
{
    public string id,label;public Sprite icon;public CommonHeadType common;
    public DemoSkillSelector source;public int skill;
    public ObjectHeadSkillSettings Resolve()=>source!=null?source.GetSkillSettings(skill):default;
    public static List<ObjectHeadHeadChoice> Catalog(ObjectHeadContent catalog,ObjectHeadLocalizationTable localization,ObjectHeadLanguage language)
    {
        var result=new List<ObjectHeadHeadChoice>();
        foreach(var character in catalog.characters)
        {
            if(character.prefab==null)continue;
            var selector=character.prefab.GetComponent<DemoSkillSelector>();var visual=character.prefab.GetComponent<CharacterVisual>();
            if(selector==null || visual==null)continue;
            for(int i=0;i<3;i++)result.Add(new ObjectHeadHeadChoice{id="character:"+(int)character.kind+":"+i,label=localization.Get(character.nameKey,language)+" "+(i+1),icon=visual.GetSkillHeadSprite(i),source=selector,skill=i});
        }
        foreach(var common in catalog.commonHeads)
        {
            string key=common.nameKey;
            if(string.IsNullOrEmpty(key))key=common.type==CommonHeadType.Attack?"common_attack":common.type==CommonHeadType.Mobility?"common_mobility":"common_terrain";
            result.Add(new ObjectHeadHeadChoice{id="common:"+(int)common.type,label=localization.Get(key,language),icon=common.sprite,common=common.type});
        }
        return result;
    }
}

/// <summary>Designer-authored template + scroll layout; reusable for finite or unlimited inventories.</summary>
public sealed class ObjectHeadHeadInventoryPanel:MonoBehaviour
{
    public Button entryTemplate,closeButton,allButton,characterButton,commonButton;
    public RectTransform contentRoot;
    public InputField searchField;
    public Text title;
    private readonly List<Button> entries=new List<Button>();
    private List<ObjectHeadHeadChoice> choices;
    private Action<ObjectHeadHeadChoice> selected;
    private Func<ObjectHeadHeadChoice,int> charges;
    private int category;
    public static bool AnyOpen {get;private set;}
    public int VisibleCount=>entries.Count;
    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        allButton.onClick.AddListener(()=>{category=0;Refresh();});
        characterButton.onClick.AddListener(()=>{category=1;Refresh();});
        commonButton.onClick.AddListener(()=>{category=2;Refresh();});
        searchField.onValueChanged.AddListener(_=>Refresh());entryTemplate.gameObject.SetActive(false);
    }
    public void Open(List<ObjectHeadHeadChoice> available,Action<ObjectHeadHeadChoice> onSelected,Func<ObjectHeadHeadChoice,int> remaining=null)
    {
        choices=available;selected=onSelected;charges=remaining;category=0;
        gameObject.SetActive(true);AnyOpen=true;searchField.SetTextWithoutNotify("");Refresh();
    }
    public void Close(){gameObject.SetActive(false);}
    private void OnDisable(){AnyOpen=false;}
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();
#else
        if(Input.GetKeyDown(KeyCode.Escape))Close();
#endif
    }
    private void Refresh()
    {
        foreach(var entry in entries){entry.gameObject.SetActive(false);Destroy(entry.gameObject);}entries.Clear();
        if(choices==null)return;
        foreach(var choice in choices.Where(c=>(category==0 || (category==1)==(c.common==CommonHeadType.None)) &&
            (string.IsNullOrWhiteSpace(searchField.text)||c.label.IndexOf(searchField.text,StringComparison.CurrentCultureIgnoreCase)>=0)))
        {
            var entry=Instantiate(entryTemplate,contentRoot);entry.gameObject.SetActive(true);
            var icon=entry.transform.Find("Icon").GetComponent<Image>();icon.sprite=choice.icon;icon.enabled=choice.icon!=null;
            int count=charges?.Invoke(choice)??-1;
            entry.transform.Find("Name").GetComponent<Text>().text=choice.label;
            entry.transform.Find("Count").GetComponent<Text>().text=count<0?"∞":count.ToString();
            entry.interactable=count!=0;icon.color=count==0?new Color(1,1,1,.25f):Color.white;
            entry.onClick.AddListener(()=>{Close();selected?.Invoke(choice);});entries.Add(entry);
        }
    }
}

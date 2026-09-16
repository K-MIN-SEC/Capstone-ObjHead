using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectHeadCharacterBrowser : MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public InputField search;
    public Button roleButton;
    public Text roleLabel;
    public Text countLabel;
    public RectTransform content;
    public Button cardTemplate;
    public string[] roleKeys={"role_all","role_damage","role_control","role_support"};
    private int role;
    private readonly List<Button> cards=new List<Button>();
    private bool canChoose=true;
    private void Start()
    {
        search.onValueChanged.AddListener(_=>Refresh(canChoose));
        roleButton.onClick.AddListener(()=>{role=(role+1)%roleKeys.Length;Refresh(canChoose);});
        Refresh(true);
    }
    public static ObjectHeadCharacterDefinition[] Filter(ObjectHeadCharacterDefinition[] source,string query,string role,Func<string,string> translate)
    {
        query=(query??"").Trim();
        return source.Where(c=>(role=="role_all" || c.roleKey==role) &&
            (query.Length==0 || translate(c.nameKey).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || c.kind.ToString().IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
    }
    public void Refresh(bool enabled)
    {
        if(title==null || title.content==null || cardTemplate==null)return;
        canChoose=enabled;
        var entries=Filter(title.content.characters,search.text,roleKeys[role],title.Translate);
        while(cards.Count<entries.Length)
        {
            var card=Instantiate(cardTemplate,content);card.gameObject.SetActive(true);cards.Add(card);
        }
        for(int i=0;i<cards.Count;i++)
        {
            var card=cards[i];card.gameObject.SetActive(i<entries.Length);
            if(i>=entries.Length)continue;
            var entry=entries[i];
            card.transform.Find("Portrait").GetComponent<Image>().sprite=entry.portrait;
            card.transform.Find("Label").GetComponent<Text>().text=title.Translate(entry.nameKey);
            card.onClick.RemoveAllListeners();card.onClick.AddListener(()=>title.SelectCharacter(entry.kind));
            card.interactable=enabled;
        }
        roleLabel.text=title.Translate(roleKeys[role]);
        countLabel.text=string.Format(title.Translate("roster_count"),entries.Length,title.content.characters.Length);
    }
}

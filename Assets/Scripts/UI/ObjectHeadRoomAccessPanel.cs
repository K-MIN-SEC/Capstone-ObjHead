using System;
using UnityEngine;
using UnityEngine.UI;

// Layout is serialized in the title prefab. Secrets are transient, never preferences.
public sealed class ObjectHeadRoomAccessPanel:MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public Button visibilityButton;
    public Text visibilityLabel;
    public InputField createPassword,joinPassword;
    public bool IsPrivate {get;private set;}
    public string CreatePassword=>IsPrivate?createPassword.text:null;
    public string JoinPassword=>joinPassword.text;
    private void Awake(){visibilityButton.onClick.AddListener(Toggle);Refresh();}
    private void OnEnable(){title.LanguageChanged+=Refresh;Refresh();}
    private void OnDisable(){title.LanguageChanged-=Refresh;ClearPasswords();}
    private void Toggle(){IsPrivate=!IsPrivate;if(!IsPrivate)createPassword.text="";Refresh();}
    private void Refresh()
    {
        visibilityLabel.text=title.Translate(IsPrivate?"room_private":"room_public");
        createPassword.interactable=IsPrivate;
        createPassword.GetComponent<CanvasGroup>().alpha=IsPrivate?1:.4f;
    }
    public void ClearPasswords(){createPassword.text="";joinPassword.text="";}
    public static string ErrorKey(string message)
    {
        foreach(var key in new[]{"room_password_required","room_password_incorrect","room_password_invalid","room_password_rate_limited","private_requires_authority","incompatible_ruleset","room_not_found","room_full","match_in_progress"})
            if(message?.IndexOf(key,StringComparison.Ordinal)>=0)return key;
        return message??"connection_help";
    }
}

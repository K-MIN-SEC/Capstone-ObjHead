using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ObjectHeadRoomAccessAuthoring
{
    [MenuItem("Object Head/UI/Add Private Room Controls")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        const string path="Assets/Prefabs/UI/ObjectHeadTitle.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var title=root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            if(title.roomAccess!=null)return; // Preserve subsequent Inspector edits.
            var panel=title.gameObject.AddComponent<ObjectHeadRoomAccessPanel>();title.roomAccess=panel;panel.title=title;
            var create=title.frontEnd.create.transform;
            panel.visibilityButton=Object.Instantiate(title.joinRoomButton,create);panel.visibilityButton.name="RoomVisibility";
            Place(panel.visibilityButton.transform,0,-12,500,48);
            panel.visibilityLabel=panel.visibilityButton.GetComponentInChildren<Text>();
            Object.DestroyImmediate(panel.visibilityLabel.GetComponent<ObjectHeadLocalizedLabel>());
            panel.visibilityLabel.text="공개방 · 누구나 참가";panel.visibilityLabel.fontSize=22;
            panel.createPassword=Password(title.roomCodeInput,create,"CreatePassword","room_password_hint",0,-79,500,48);
            panel.createPassword.gameObject.AddComponent<CanvasGroup>();
            Place(title.createRoomButton.transform,0,-151,500,60);
            var note=Object.Instantiate(title.roomSizeValueText,create);note.name="PrivateRoomNotice";
            Place(note.transform,0,-222,500,48);note.fontSize=17;note.text="";
            var loc=note.GetComponent<ObjectHeadLocalizedLabel>()??note.gameObject.AddComponent<ObjectHeadLocalizedLabel>();loc.LocalizationKey="room_private_notice";
            panel.joinPassword=Password(title.roomCodeInput,title.frontEnd.find.transform,"JoinPassword","room_join_password",0,-226,530,44);
            var hint=title.frontEnd.find.transform.Find("join_code_hint");if(hint!=null)hint.gameObject.SetActive(false);
            foreach(var label in root.GetComponentsInChildren<ObjectHeadLocalizedLabel>(true))label.Preview();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    public static void ApplyContentAndBuild()
    {
        Apply();ObjectHeadCaptivityAuthoring.Apply();ObjectHeadRulesRevisionAuthoring.Apply();ObjectHeadTrainingAuthoring.Apply();ObjectHeadAIOptionsAuthoring.Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }
    private static InputField Password(InputField template,Transform parent,string name,string hint,float x,float y,float w,float h)
    {
        var field=Object.Instantiate(template,parent);field.name=name;Place(field.transform,x,y,w,h);
        field.text="";field.characterLimit=32;field.contentType=InputField.ContentType.Password;field.lineType=InputField.LineType.SingleLine;
        if(field.placeholder is Text text){text.fontSize=17;var loc=text.GetComponent<ObjectHeadLocalizedLabel>()??text.gameObject.AddComponent<ObjectHeadLocalizedLabel>();loc.LocalizationKey=hint;}
        return field;
    }
    private static void Place(Transform t,float x,float y,float w,float h)
    {var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
}

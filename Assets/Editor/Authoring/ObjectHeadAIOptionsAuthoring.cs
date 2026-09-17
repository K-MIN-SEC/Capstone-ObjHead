using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ObjectHeadAIOptionsAuthoring
{
    [MenuItem("Object Head/0916/Add Editable AI Options")]
    public static void Apply()
    {
        const string path="Assets/Prefabs/UI/ObjectHeadTitle.prefab";
        GameObject root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            ObjectHeadTitleScreen title=root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            if(title.aiRosterModeButton!=null && title.aiDifficultyButton!=null && title.editLocalSquadButton!=null)return;
            Transform parent=title.toggleMapModeButton.transform.parent;
            if(title.aiRosterModeButton==null || title.aiDifficultyButton==null)
            {
            title.aiRosterModeButton=Create(title.toggleMapModeButton,parent,"AIRosterMode",new Vector2(-610,-212),new Vector2(184,38),out Text rosterText);
            title.aiRosterModeText=rosterText;
            title.aiDifficultyButton=Create(title.toggleMapModeButton,parent,"AIDifficulty",new Vector2(-414,-212),new Vector2(184,38),out Text difficultyText);
            title.aiDifficultyText=difficultyText;
            RectTransform roster=(RectTransform)title.lobbyPlayerListText.transform;
            roster.anchoredPosition=new Vector2(-512,-54);
            roster.sizeDelta=new Vector2(376,196);
            }
            if(title.editLocalSquadButton==null)
            {
                title.editLocalSquadButton=Create(title.toggleMapModeButton,parent,"EditLocalSquad",new Vector2(579,282),new Vector2(224,40),out Text editText);
                title.editLocalSquadText=editText;
            }
            EditorUtility.SetDirty(title);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    private static Button Create(Button template,Transform parent,string name,Vector2 position,Vector2 size,out Text label)
    {
        Button button=Object.Instantiate(template,parent);
        button.name=name;
        RectTransform rect=(RectTransform)button.transform;
        rect.anchoredPosition=position;rect.sizeDelta=size;
        label=button.GetComponentInChildren<Text>(true);
        ObjectHeadLocalizedLabel localized=label.GetComponent<ObjectHeadLocalizedLabel>();
        if(localized!=null)Object.DestroyImmediate(localized);
        label.text=name;label.fontSize=18;label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=18;
        button.gameObject.SetActive(false);
        return button;
    }
}

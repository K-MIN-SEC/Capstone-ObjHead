using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ObjectHeadVacuumAuthoring
{
    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}
    [MenuItem("Object Head/Replace Magnet with Vacuum")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        var catalog=ObjectHeadContent.Load();
        // Keep the old numeric character ID and prefab GUID so saved teams do not shift.
        var entry=catalog.Character(ObjectHeadCharacterKind.Magnet);
        var heads=ObjectHeadExpansionAuthoring.Slice("NozzleHeads0917",3,1,largestConnectedShape:true);
        const string tuningPath="Assets/GameData/VacuumTuning.asset";
        var tuning=AssetDatabase.LoadAssetAtPath<ObjectHeadVacuumDefinition>(tuningPath);
        if(tuning==null){tuning=ScriptableObject.CreateInstance<ObjectHeadVacuumDefinition>();AssetDatabase.CreateAsset(tuning,tuningPath);}
        var skills=new ObjectHeadSkillDefinition[3];
        for(int i=0;i<3;i++)
        {
            string key="vacuum_"+(i+1),path="Assets/GameData/Skills/"+key+".asset";
            var skill=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();AssetDatabase.CreateAsset(skill,path);}
            skill.balancePrefix="skill.vacuum."+(i+1);skill.nameKey="vacuum_skill_"+(i+1);skill.descriptionKey=skill.nameKey+"_description";
            skill.cooldown=i==0?0:i==1?2:3;
            var settings=ObjectHeadSkillSettings.CreateDefault(heads[i],Color.white,Color.white,0,0,0);
            settings.skillId=51+i;settings.effectType=i==2?SkillEffectType.Hover:SkillEffectType.Airflow;
            settings.pullsTargets=i==1;settings.vacuum=tuning;skill.settings=settings;skills[i]=skill;EditorUtility.SetDirty(skill);
        }
        string prefabPath=AssetDatabase.GetAssetPath(entry.prefab);var root=PrefabUtility.LoadPrefabContents(prefabPath);root.name="vacuum";
        var visual=new SerializedObject(root.GetComponent<CharacterVisual>());var sprites=visual.FindProperty("authoredSkillHeads");sprites.arraySize=3;
        for(int i=0;i<3;i++)sprites.GetArrayElementAtIndex(i).objectReferenceValue=heads[i];
        var renderer=(SpriteRenderer)visual.FindProperty("headRenderer").objectReferenceValue;renderer.sprite=heads[0];
        renderer.transform.localScale=Vector3.one*(catalog.characterHeadVisualSize/Mathf.Max(heads[0].bounds.size.x,heads[0].bounds.size.y));
        visual.ApplyModifiedPropertiesWithoutUndo();
        var selector=new SerializedObject(root.GetComponent<DemoSkillSelector>());var defs=selector.FindProperty("authoredSkills");defs.arraySize=3;
        for(int i=0;i<3;i++)defs.GetArrayElementAtIndex(i).objectReferenceValue=skills[i];selector.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);PrefabUtility.UnloadPrefabContents(root);
        entry.nameKey="vacuum_name";entry.descriptionKey="vacuum_description";entry.portrait=heads[0];entry.roleKey="role_control";
        EditorUtility.SetDirty(catalog);
        foreach(var map in catalog.maps)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            InstallInventory(Object.FindAnyObjectByType<ObjectHeadBattleScreen>(),catalog);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("[VACUUM_AUTHORING] Vacuum and editable training inventory installed.");
    }

    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static Text Label(Transform parent,string name,string key,float x,float y,float w,float h,Font font,int size=22)
    {
        var r=Rect(parent,name,x,y,w,h);var text=r.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=new Color32(241,242,223,255);text.raycastTarget=false;
        if(!string.IsNullOrEmpty(key)){var local=r.gameObject.AddComponent<ObjectHeadLocalizedLabel>();local.LocalizationKey=key;local.Preview();}return text;
    }
    private static Button Button(Transform parent,string name,string key,float x,float y,float w,float h,Font font)
    {
        var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/InkPanel.png");image.type=Image.Type.Sliced;image.color=new Color32(43,81,92,255);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;Label(r,"Name",key,0,0,w-12,h-8,font);return button;
    }
    private static void InstallInventory(ObjectHeadBattleScreen hud,ObjectHeadContent catalog)
    {
        if(hud==null || hud.headInventory!=null)return; // Preserve the user's subsequent editor layout changes.
        var canvas=hud.GetComponentInParent<Canvas>();if(canvas==null)canvas=Object.FindAnyObjectByType<Canvas>();
        var launcher=Button(canvas.transform,"TrainingHeads","training_all_heads",-145,-115,250,54,catalog.uiFont);
        var lr=(RectTransform)launcher.transform;lr.anchorMin=lr.anchorMax=Vector2.one;
        hud.trainingHeadsButton=launcher;
        var root=Rect(canvas.transform,"HeadInventory",0,0,1120,680);var background=root.gameObject.AddComponent<Image>();background.color=new Color32(16,37,47,250);
        var panel=root.gameObject.AddComponent<ObjectHeadHeadInventoryPanel>();hud.headInventory=panel;
        panel.title=Label(root,"Title","training_all_heads",0,295,700,48,catalog.uiFont,30);
        panel.closeButton=Button(root,"Close","close",495,296,80,45,catalog.uiFont);
        panel.allButton=Button(root,"All","inventory_all",-435,237,155,44,catalog.uiFont);
        panel.characterButton=Button(root,"Characters","inventory_characters",-265,237,165,44,catalog.uiFont);
        panel.commonButton=Button(root,"Common","inventory_common",-90,237,165,44,catalog.uiFont);
        var search=Rect(root,"Search",300,237,400,44);search.gameObject.AddComponent<Image>().color=new Color32(29,62,73,255);
        panel.searchField=search.gameObject.AddComponent<InputField>();panel.searchField.textComponent=Label(search,"Value",null,0,0,380,40,catalog.uiFont);
        panel.searchField.placeholder=Label(search,"Placeholder","inventory_search",0,0,380,40,catalog.uiFont);
        var viewport=Rect(root,"Viewport",0,-42,1050,475);viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.12f);viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.viewport=viewport;
        var content=Rect(viewport,"Contents",0,0,1030,475);content.anchorMin=new Vector2(.5f,1);content.anchorMax=content.anchorMin;content.pivot=new Vector2(.5f,1);
        var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(155,130);grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(12,12,12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=6;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;panel.contentRoot=content;
        var template=Button(root,"EntryTemplate",null,0,0,155,130,catalog.uiFont);
        var name=template.transform.Find("Name").GetComponent<Text>();name.fontSize=19;name.rectTransform.anchoredPosition=new Vector2(0,-42);name.rectTransform.sizeDelta=new Vector2(145,34);
        var icon=Rect(template.transform,"Icon",0,12,70,70).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;
        Label(template.transform,"Count",null,55,42,42,46,catalog.uiFont,22);panel.entryTemplate=template;template.gameObject.SetActive(false);
        Label(root,"Help","inventory_training_help",0,-310,1000,38,catalog.uiFont,20);
        root.gameObject.SetActive(false);EditorUtility.SetDirty(hud);
    }
}

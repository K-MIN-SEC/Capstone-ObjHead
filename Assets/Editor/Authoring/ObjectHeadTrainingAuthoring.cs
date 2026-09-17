using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ObjectHeadTrainingAuthoring
{
    [MenuItem("Object Head/0916/Add Editable Training Controls")]
    public static void Apply()
    {
        ObjectHeadContent content=ObjectHeadContent.Load();
        foreach(ObjectHeadMapDefinition map in content.maps)
        {
            string path="Assets/Scenes/"+map.sceneName+".unity";
            Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var screen=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ObjectHeadBattleScreen>(true)).First();
            Button template=screen.trainingResetButton;
            if(template==null)continue;
            Place(template,-360);
            screen.trainingTimerButton=Ensure(template,"TrainingTimer",screen.trainingTimerButton,"training_timer",-120);
            screen.trainingHealButton=Ensure(template,"TrainingHeal",screen.trainingHealButton,"training_heal",120);
            screen.trainingSkipButton=Ensure(template,"TrainingSkip",screen.trainingSkipButton,"training_skip",360);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,path);
        }
        AssetDatabase.SaveAssets();
    }
    private static Button Ensure(Button template,string name,Button existing,string key,float x)
    {
        Button button=existing!=null?existing:Object.Instantiate(template,template.transform.parent);
        button.name=name;Place(button,x);
        Text label=button.GetComponentInChildren<Text>(true);
        var localized=label.GetComponent<ObjectHeadLocalizedLabel>()??label.gameObject.AddComponent<ObjectHeadLocalizedLabel>();
        localized.LocalizationKey=key;localized.Preview();
        return button;
    }
    private static void Place(Button button,float x)
    {
        var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(x,-205);rect.sizeDelta=new Vector2(220,48);
    }
}

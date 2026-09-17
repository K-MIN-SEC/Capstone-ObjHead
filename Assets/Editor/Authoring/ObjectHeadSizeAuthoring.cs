using System;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadSizeAuthoring
{
    [MenuItem("Object Head/Visuals/Normalize Head Sizes")]
    public static void Apply()
    {
        var content=ObjectHeadContent.Load();
        foreach(var entry in content.characters)
        {
            string path=AssetDatabase.GetAssetPath(entry.prefab);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual=root.GetComponent<CharacterVisual>();
                var serialized=new SerializedObject(visual);
                var head=(SpriteRenderer)serialized.FindProperty("headRenderer").objectReferenceValue;
                var reference=visual.GetSkillHeadSprite(0);
                if(reference==null || head==null)throw new InvalidOperationException("Missing head: "+entry.kind);
                serialized.FindProperty("normalizeHeadSprites").boolValue=true;
                serialized.FindProperty("commonHeadSizeMultiplier").floatValue=1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                float extent=Mathf.Max(reference.bounds.size.x,reference.bounds.size.y);
                head.transform.localScale=Vector3.one*(content.characterHeadVisualSize/extent);
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log($"[HEAD_SIZE] {entry.kind}: {content.characterHeadVisualSize:F3} world units; editable transform preserved on subsequent builds.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        EditorUtility.SetDirty(content);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyAndBuild()
    {
        Apply();
        ObjectHeadAIOptionsAuthoring.Apply();
        ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }

    public static void BuildCombatFixes()
    {
        var art=ObjectHeadPresentation.Load();
        if(art.healingSupplySprite==null)
        {
            art.healingSupplySprite=ObjectHeadContent.Load().Common(CommonHeadType.HealingPotion).sprite;
            EditorUtility.SetDirty(art);
        }
        EditorUtility.SetDirty(ObjectHeadContent.Load());
        AssetDatabase.SaveAssets();
        ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies only the finalized roster rules and missing designer-editable spawn
/// markers. It deliberately leaves terrain, UI and existing marker placement
/// untouched so later Inspector edits remain the source of truth.
/// </summary>
public static class ObjectHeadRulesRevisionAuthoring
{
    [MenuItem("Object Head/0916/Apply Final Roster Rules")]
    public static void Apply()
    {
        ObjectHeadContent content=AssetDatabase.LoadAssetAtPath<ObjectHeadContent>("Assets/Resources/ObjectHeadContent.asset");
        if(content==null)throw new InvalidOperationException("ObjectHeadContent asset is missing.");
        SetRule(content,2,3);
        SetRule(content,4,2);
        EditorUtility.SetDirty(content);

        foreach(ObjectHeadMapDefinition map in content.maps)
        {
            string path="Assets/Scenes/"+map.sceneName+".unity";
            Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            ObjectHeadSpawnLayout spawn=scene.GetRootGameObjects()
                .SelectMany(root=>root.GetComponentsInChildren<ObjectHeadSpawnLayout>(true)).FirstOrDefault();
            if(spawn==null)throw new InvalidOperationException("Spawn layout missing: "+path);
            bool changed=false;
            foreach(ObjectHeadModeSpawnLayout layout in spawn.layouts.Where(layout=>layout.mode!=ObjectHeadMatchMode.Duel))
            {
                for(int seatIndex=0;seatIndex<layout.seats.Length;seatIndex++)
                {
                    ObjectHeadSpawnSeat seat=layout.seats[seatIndex];
                    if(seat.characterSlots!=null && seat.characterSlots.Length>=2)continue;
                    Transform first=seat.characterSlots?.FirstOrDefault();
                    if(first==null)throw new InvalidOperationException($"First spawn marker missing: {map.id}/{layout.mode}/seat {seatIndex+1}");
                    GameObject marker=new GameObject(first.name+"_Slot2");
                    marker.transform.SetParent(first.parent,false);
                    marker.transform.position=first.position+Vector3.right*(seatIndex%2==0?.72f:-.72f);
                    seat.characterSlots=new[]{first,marker.transform};
                    changed=true;
                }
            }
            if(changed){EditorUtility.SetDirty(spawn);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,path);}
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Object Head] Final roster rules applied: 2P x3, 4P x2, editable dual spawn markers.");
    }

    private static void SetRule(ObjectHeadContent content,int players,int characters)
    {
        ObjectHeadTeamRule rule=content.teamRules.FirstOrDefault(candidate=>candidate.players==players);
        if(rule==null)
        {
            rule=new ObjectHeadTeamRule{players=players};
            content.teamRules=content.teamRules.Concat(new[]{rule}).OrderBy(candidate=>candidate.players).ToArray();
        }
        rule.charactersPerPlayer=characters;
    }
}

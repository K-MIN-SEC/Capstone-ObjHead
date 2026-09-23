using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadAIPickupSmoke : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadAIPickupSmoke"))return;
        var root=new GameObject("AIPickupSmoke");
        DontDestroyOnLoad(root);
        root.AddComponent<ObjectHeadAIPickupSmoke>();
    }

    private IEnumerator Start()
    {
        yield return null;
        var catalog=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,
            playerCount=2,mapId=catalog.maps[0].id,mapSeed=160916,characterSpawnSeed=160916,startingPlayerIndex=2,
            players=new[]{
                new ObjectHeadPlayerAssignment{playerIndex=1,allianceId=1,username="Player",characters=catalog.DefaultSelection(2)},
                new ObjectHeadPlayerAssignment{playerIndex=2,allianceId=2,username="AI",isAi=true,
                    aiDifficulty=ObjectHeadAIDifficulty.Beginner,characters=catalog.DefaultSelection(2)}
            }});
        SceneManager.LoadScene(catalog.maps[0].sceneName);
        yield return new WaitForSeconds(.08f);
        var turns=FindAnyObjectByType<TurnManager>();
        var actor=turns?.CurrentCharacter;
        var terrain=FindAnyObjectByType<TerrainManager>();
        var manager=FindAnyObjectByType<PlayerInventoryManager>();
        int owner=actor?.GetComponent<ObjectHeadTeamMember>()?.PlayerIndex??0;
        var inventory=manager?.GetInventory(owner);
        int startingItems=inventory?.Count??-1;
        bool placed=false;
        CommonHeadItem sample=null;
        if(actor!=null && terrain!=null)
        {
            int index=0;
            foreach(var other in turns.Characters.Where(c=>c!=null && c!=actor))
            {
                other.transform.position=actor.transform.position+new Vector3(actor.transform.position.x>0?-30-index:30+index,4,0);
                other.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                index++;
            }
            var navigation=new ObjectHeadAINavigation(actor,terrain);
            foreach(float offset in new[]{2f,-2f,3f,-3f,4f,-4f,5f,-5f,6f,-6f})
            {
                if(!navigation.GroundAt(actor.transform.position.x+offset,actor.transform.position.y,out var landing))continue;
                if(turns.Characters.Any(c=>c!=null && c!=actor && Vector2.Distance(c.transform.position,landing)<3f))continue;
                sample=CommonHeadItem.Create(CommonHeadType.Attack,landing+Vector2.up*.25f,null);
                placed=true;
                break;
            }
            Physics2D.SyncTransforms();
        }
        float deadline=Time.realtimeSinceStartup+16f;
        while(placed && sample!=null && Time.realtimeSinceStartup<deadline)
            yield return null;
        bool pass=placed && sample==null && inventory!=null && inventory.Count>startingItems && ObjectHeadAIDirector.DistanceMoved>0f;
        Debug.Log($"[AI_PICKUP_DETAIL] placed={placed}, sampleCollected={sample==null}, items={inventory?.Count??-1}, startingItems={startingItems}, moved={ObjectHeadAIDirector.DistanceMoved:F2}, candidates={ObjectHeadAIDirector.MovementCandidates}");
        Debug.Log(pass?"[AI_PICKUP_PASS] AI moved to collect team loot":"[AI_PICKUP_FAIL] AI did not collect team loot");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(pass?0:2);
#else
        Application.Quit(pass?0:2);
#endif
    }
}

using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in regression run; never runs during ordinary play.
public sealed class ObjectHeadCombatFixSmoke : MonoBehaviour
{
    private bool failed;
    private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadCombatFixSmoke"))return;
        var root=new GameObject("CombatFixSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadCombatFixSmoke>();
    }
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[COMBAT_FIX_FAIL] timeout");Application.Quit(2);}}
    private void Check(bool ok,string label){if(!ok)failed=true;Debug.Log((ok?"[COMBAT_FIX_CHECK] ":"[COMBAT_FIX_FAIL] ")+label);}
    private IEnumerator Start()
    {
        yield return null;
        var content=ObjectHeadContent.Load();
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=content.maps[0].id,mapSeed=916,characterSpawnSeed=916,startingPlayerIndex=1,
            players=new[]{new ObjectHeadPlayerAssignment{playerIndex=1,allianceId=1,characters=content.DefaultSelection(2)},new ObjectHeadPlayerAssignment{playerIndex=2,allianceId=2,characters=content.DefaultSelection(2)}}});
        SceneManager.LoadScene(content.maps[0].sceneName);yield return new WaitForSeconds(.8f);
        var turns=FindAnyObjectByType<TurnManager>();var terrain=FindAnyObjectByType<TerrainManager>();
        turns.SetTrainingTimerPaused(true);
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
        Check(ObjectHeadCameraController.EdgePanDirection(new Vector2(500,300),new Vector2(1000,600),.025f)==Vector2.zero,"camera stays still at center");
        Check(ObjectHeadCameraController.EdgePanDirection(new Vector2(999,300),new Vector2(1000,600),.025f).x>.9f,"camera edge pan without drag");
        Check(ObjectHeadCameraController.EdgePanDirection(new Vector2(-1,300),new Vector2(1000,600),.025f)==Vector2.zero,"camera ignores cursor outside window");

        Vector2 sky=terrain.GetTerrainBounds().center;
        sky.y=terrain.GetTerrainBounds().max.y-2;
        terrain.CreateCircle(sky,40,TerrainType.Created,null);
        var loot=CommonHeadItem.Create(CommonHeadType.Attack,sky+Vector2.up*1.1f,null);
        yield return new WaitForSeconds(1);
        float supportedY=loot.transform.position.y;
        terrain.DestroyCircle(sky,85);
        yield return new WaitForSeconds(.6f);
        Check(loot==null || loot.transform.position.y<supportedY-.3f,"world loot falls after its support is destroyed");
        if(loot!=null)loot.Retire();

        var item=CommonHeadItem.Create(CommonHeadType.Attack,sky,null);
        item.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        var shot=new GameObject("PassThroughTest").AddComponent<SkillProjectile>();shot.transform.position=sky+Vector2.left*2;
        var settings=ObjectHeadSkillSettings.CreateDefault(null,Color.white,Color.white,1,.2f,0);
        shot.Initialize(Vector2.right*5,.1f,0,10,Color.white,null,null,1,.2f,.1f,Color.white,0,settings);
        Check(item.GetComponents<Collider2D>().All(c=>Physics2D.GetIgnoreCollision(c,shot.GetComponent<Collider2D>())),"all loot colliders ignore projectiles at launch");
        var lateItem=CommonHeadItem.Create(CommonHeadType.Ice,sky+Vector2.right*.7f,null);
        lateItem.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        Check(lateItem.GetComponents<Collider2D>().All(c=>Physics2D.GetIgnoreCollision(c,shot.GetComponent<Collider2D>())),"new loot also ignores shots already in flight");
        yield return new WaitForSeconds(.65f);
        Check(shot!=null && shot.IsFlying && shot.transform.position.x>sky.x+.8f,"shot physically traverses loot without exploding");
        if(shot!=null)Destroy(shot.gameObject);item.Retire();lateItem.Retire();

        foreach(string skill in new[]{"revolver_2","revolver_3","common_lock"})
        {
            var definition=Resources.LoadAll<ObjectHeadSkillDefinition>("").FirstOrDefault(d=>d.name==skill);
            // Definitions are referenced by the content catalog rather than Resources directly.
            if(definition==null)
            {
                if(skill=="common_lock")definition=content.Common(CommonHeadType.Lock).skill;
                else
                {
                    var prefab=content.Character(ObjectHeadCharacterKind.Revolver).prefab;
                    var instance=Instantiate(prefab);var selector=instance.GetComponent<DemoSkillSelector>();
                    selector.SetSkillIndex(skill=="revolver_2"?1:2);
                    settings=selector.GetCurrentSkillSettings();Destroy(instance);
                }
            }
            if(definition!=null)settings=definition.Resolve(null);
            Check(settings.resolveAtTurnEnd,skill+" configured for turn-end resolution");
            if(skill=="revolver_3")Check(Mathf.Abs(settings.explosionRadiusWorld-2.5f)<.001f,"expanded airstrike radius loaded from balance sheet");
            var actor=turns.CurrentCharacter;
            var a=turns.Characters[0].GetComponent<CharacterCombat>();var b=turns.Characters[1].GetComponent<CharacterCombat>();
            Vector2 point=sky-Vector2.up*1.5f;
            terrain.CreateCircle(point-Vector2.up*.65f,55,TerrainType.Created,null);
            for(int i=0;i<turns.Characters.Length;i++)
            {
                var c=turns.Characters[i];c.transform.position=point+Vector2.right*(i<2?(i==0?-.2f:.2f):5+i);
                c.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            }
            a.Heal(999);b.Heal(999);a.TakeDamage(10);b.TakeDamage(10);a.ApplyPendingDamage();b.ApplyPendingDamage();
            int hpA=a.CurrentHp,hpB=b.CurrentHp,serial=turns.TurnSerial;
            Physics2D.SyncTransforms();
            Check(turns.TryBeginAction(actor),skill+" begins real turn action");
            var effect=new GameObject("DeferredTest_"+skill).AddComponent<SkillProjectile>();effect.transform.position=point;
            effect.Initialize(Vector2.zero,.1f,0,10,Color.white,actor.GetComponent<CharacterCombat>(),turns,settings.maxDamage,settings.explosionRadiusWorld,.1f,Color.white,settings.knockbackForce,settings);
            effect.SendMessage("ResolveImpact",point);
            yield return new WaitForSeconds(.3f);
            Check(a.CurrentHp==hpA && b.CurrentHp==hpB && !ObjectHeadCaptivity.Captured(a),skill+" remains a marker during residual movement");
            turns.SetTrainingTimerPaused(false);
            bool sawDrop=false;
            while(turns.TurnSerial==serial && !turns.IsMatchOver)
            {
                if(turns.IsTurnEndResolving){sawDrop=true;Check(!turns.CanCharacterMove(actor),skill+" cannot move during resolution");break;}
                yield return null;
            }
            while(turns.TurnSerial==serial && !turns.IsMatchOver)yield return null;
            turns.SetTrainingTimerPaused(true);
            Check(sawDrop,skill+" resolves before advancing turn");
            if(skill=="revolver_2")Check(a.CurrentHp>hpA && b.CurrentHp>hpB,"healing supply restores both factions");
            if(skill=="revolver_3")Check(a.CurrentHp<hpA && b.CurrentHp<hpB,"airstrike damages both factions at settlement");
            if(skill=="common_lock")Check(ObjectHeadCaptivity.Captured(a)&&ObjectHeadCaptivity.Captured(b),"lock captures both factions at turn end");
        }
        Debug.Log(failed?"[COMBAT_FIX_FAIL] regression failed":"[COMBAT_FIX_PASS] loot collision/fall, deferred heal/airstrike/captivity, camera edge controls");
        Application.Quit(failed?2:0);
    }
}

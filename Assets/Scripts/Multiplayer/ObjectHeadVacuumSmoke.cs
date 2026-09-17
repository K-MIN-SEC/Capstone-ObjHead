using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadVacuumSmoke:MonoBehaviour
{
    private bool failed;private float deadline;private string folder;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(!Environment.GetCommandLineArgs().Contains("-objectHeadVacuumSmoke"))return;var go=new GameObject("VacuumSmoke");DontDestroyOnLoad(go);go.AddComponent<ObjectHeadVacuumSmoke>();}
    private void Awake(){deadline=Time.realtimeSinceStartup+70;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-objectHeadCapture");folder=i>=0?args[i+1]:null;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[VACUUM_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Log;
    private void OnDisable()=>Application.logMessageReceived-=Log;
    private void Log(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error)failed=true;}
    private void Check(bool ok,string label){Debug.Log("[VACUUM_DETAIL] "+label+"="+ok);if(!ok)failed=true;}
    private IEnumerator Start()
    {
        yield return null;Screen.SetResolution(1600,900,false);
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId="twin_citadels",mapSeed=1717,characterSpawnSeed=1717,startingPlayerIndex=1,
            players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=new[]{ObjectHeadCharacterKind.Magnet,ObjectHeadCharacterKind.Seed,ObjectHeadCharacterKind.Bomb}}).ToArray()});
        SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.7f);
        var turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        var actor=turns.Characters.First(c=>c.GetComponent<DemoSkillSelector>().CharacterKind==ObjectHeadCharacterKind.Magnet && c.GetComponent<ObjectHeadTeamMember>().PlayerIndex==1);
        int attempts=0;while(turns.CurrentCharacter!=actor && attempts++<20){turns.EndCurrentTurn();yield return new WaitForSeconds(.1f);}
        Check(turns.CurrentCharacter==actor,"vacuum can take a turn");
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;}
        var owner=actor.GetComponent<CharacterCombat>();var selector=actor.GetComponent<DemoSkillSelector>();var settings=selector.GetSkillSettings(0);var tuning=settings.vacuum;
        Check(settings.effectType==SkillEffectType.Airflow && tuning!=null,"replaced magnet skill definition");if(tuning==null){Application.Quit(2);yield break;}
        var target=turns.Characters.First(c=>c.GetComponent<ObjectHeadTeamMember>().PlayerIndex==2).GetComponent<CharacterCombat>();var targetBody=target.GetComponent<Rigidbody2D>();
        Vector2 origin=new Vector2(-10,16);actor.transform.position=origin;target.transform.position=origin+Vector2.right*6;targetBody.constraints=RigidbodyConstraints2D.FreezePositionY|RigidbodyConstraints2D.FreezeRotation;Physics2D.SyncTransforms();
        int hp=target.CurrentHp;
        ObjectHeadVacuum.Apply(owner,origin,Vector2.right,0,false,tuning);Check(targetBody.linearVelocity.x==0,"short charge cannot reach distant target");
        ObjectHeadVacuum.Apply(owner,origin,Vector2.right,1,false,tuning);Check(targetBody.linearVelocity.x>0 && target.CurrentHp==hp && target.PendingDamage==0,"full charge pushes without damage");
        targetBody.linearVelocity=Vector2.zero;ObjectHeadVacuum.Apply(owner,origin,Vector2.right,1,true,tuning);Check(targetBody.linearVelocity.x<0,"suction pulls toward caster");
        targetBody.linearVelocity=Vector2.zero;target.transform.position=origin+new Vector2(2,3);Physics2D.SyncTransforms();
        ObjectHeadVacuum.Apply(owner,origin,Vector2.right,1,false,tuning);Check(targetBody.linearVelocity.x==0,"off-axis target excluded");
        target.transform.position=origin+Vector2.right*6;Physics2D.SyncTransforms();
        var terrain=FindAnyObjectByType<TerrainManager>();terrain.CreateCircle(origin+Vector2.right*3,24,TerrainType.Created,null);
        ObjectHeadVacuum.Apply(owner,origin,Vector2.right,1,false,tuning);Check(targetBody.linearVelocity.x==0,"terrain blocks airflow");terrain.DestroyCircle(origin+Vector2.right*3,32);
        var loot=Enumerable.Range(0,4).Select(i=>CommonHeadItem.Create(CommonHeadType.Attack,origin+new Vector2(2+i,.3f),null)).ToArray();Physics2D.SyncTransforms();
        ObjectHeadVacuum.Apply(owner,origin,Vector2.right,1,true,tuning);
        Check(loot.Take(3).All(x=>x.IsVacuumPulled) && !loot[3].IsVacuumPulled,"only nearest three items pulled");
        var cam=Camera.main;cam.GetComponent<ObjectHeadCameraController>().enabled=false;cam.transform.position=new Vector3(-6,16,-10);cam.orthographicSize=4.2f;
        StartCoroutine(ObjectHeadVacuum.Present(origin,Vector2.right,1,true,tuning));yield return new WaitForSeconds(.12f);
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"vacuum-suction.png"));
        foreach(var item in loot)if(item!=null)item.Retire();
        terrain.CreateCircle(new Vector2(-10,12),64,TerrainType.Created,null);actor.transform.position=new Vector3(-10,14.8f);Physics2D.SyncTransforms();
        var body=actor.GetComponent<Rigidbody2D>();body.constraints=RigidbodyConstraints2D.FreezeRotation;body.linearVelocity=Vector2.zero;
        selector.SetSkillIndex(2);float before=actor.transform.position.y;actor.GetComponent<SkillFireController>().Fire(1);
        Check(actor.IsVacuumHovering && turns.RemainingResidualSeconds>turns.ResidualMovementSeconds,"hover extends movement timer");
        for(float t=0;t<1;t+=Time.deltaTime){actor.SetNetworkInput(.15f,false);yield return null;}
        Check(actor.transform.position.y>before+.3f && actor.transform.position.x>-9.8f,"hover gains height and allows horizontal input");
        Check(actor.IgnoreFallDamageUntilGrounded,"hover landing protection active");
        cam.transform.position=new Vector3(-9,16,-10);
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"vacuum-hover.png"));
        yield return new WaitForSeconds(tuning.hoverSeconds+.2f);Check(!actor.IsVacuumHovering,"hover expires");
        Check(selector.GetRemainingCooldown(2)>0,"hover consumes its own skill cooldown");
        // Exercise the same scene entry used by the title's training button.
        ObjectHeadTraining.Pending=true;SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.7f);
        turns=FindAnyObjectByType<TurnManager>();actor=turns.CurrentCharacter;
        owner=actor.GetComponent<CharacterCombat>();selector=actor.GetComponent<DemoSkillSelector>();
        Check(selector.GetRemainingCooldown(2)==0,"training ignores cooldown immediately");
        var hud=FindAnyObjectByType<ObjectHeadBattleScreen>();
        Check(hud.trainingHeadsButton.gameObject.activeInHierarchy,"training entry exposes the inventory button");
        hud.trainingHeadsButton.onClick.Invoke();yield return null;
        var choices=ObjectHeadHeadChoice.Catalog(hud.content,hud.localization,ObjectHeadLanguage.Korean);
        Check(hud.headInventory.VisibleCount==choices.Count && choices.Count==hud.content.characters.Length*3+hud.content.commonHeads.Length,"all implemented heads listed without fixed character limit");
        Check(!actor.AcceptsLocalInput,"inventory blocks movement/fire input");
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"training-head-inventory.png"));
        hud.headInventory.searchField.text="청소기";yield return null;Check(hud.headInventory.VisibleCount==3,"inventory search filters character heads");hud.headInventory.Close();
        var foreign=choices.First(c=>c.id=="character:0:0");Check(selector.SelectTrainingHead(foreign) && selector.GetCurrentSkillSettings().skillId==11,"vacuum can equip bulb skill in training");
        var revolver=choices.First(c=>c.id=="character:3:0");selector.SelectTrainingHead(revolver);
        Check(selector.GetCurrentSkillSettings().projectileSprite==revolver.Resolve().projectileSprite,"borrowed skill preserves its projectile sprite");
        var helmet=choices.First(c=>c.common==CommonHeadType.IronHelmet);selector.SelectTrainingHead(helmet);
        var inventory=FindAnyObjectByType<PlayerInventoryManager>().GetInventory(turns.CurrentPlayerIndex);int items=Enumerable.Range(0,CommonHeadInventory.SlotCount).Count(i=>inventory.GetSlot(i)!=CommonHeadType.None);
        actor.GetComponent<SkillFireController>().Fire(1);Check(owner.ShieldAbsorption>0,"borrowed non-projectile head uses real shield effect");
        while(turns.ActionUsedThisTurn)yield return null;
        Check(turns.CurrentCharacter==actor && turns.CanCharacterFire(actor),"training rearms same character after resolving with timer paused");
        actor.GetComponent<SkillFireController>().Fire(1);Check(turns.ActionUsedThisTurn,"same common head can be used again");
        Check(items==Enumerable.Range(0,CommonHeadInventory.SlotCount).Count(i=>inventory.GetSlot(i)!=CommonHeadType.None),"training never consumes team inventory");
        Debug.Log(failed?"[VACUUM_FAIL]":"[VACUUM_PASS] charge, direction, terrain, nearest loot, hover, unlimited training inventory");Application.Quit(failed?2:0);
    }
}

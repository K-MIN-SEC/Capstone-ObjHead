using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadCompletionSmoke:MonoBehaviour
{
    private bool failed;private float deadline;private string folder;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){if(!Environment.GetCommandLineArgs().Contains("-objectHeadCompletionSmoke"))return;var go=new GameObject("CompletionSmoke");DontDestroyOnLoad(go);go.AddComponent<ObjectHeadCompletionSmoke>();}
    private void Awake(){deadline=Time.realtimeSinceStartup+100;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-objectHeadCapture");folder=i>=0?args[i+1]:null;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[COMPLETION_FAIL] timeout");Application.Quit(2);}}
    private void OnEnable()=>Application.logMessageReceived+=Log;
    private void OnDisable()=>Application.logMessageReceived-=Log;
    private void Log(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error)failed=true;}
    private void Check(bool ok,string label){Debug.Log("[COMPLETION_DETAIL] "+label+"="+ok);if(!ok)failed=true;}
    private IEnumerator CycleTo(TurnManager turns,TurnCharacterController actor)
    {
        turns.EndCurrentTurn();while(turns.ActionUsedThisTurn)yield return null;
        for(int n=0;n<20 && turns.CurrentCharacter!=actor;n++){turns.EndCurrentTurn();yield return null;}
        Check(turns.CurrentCharacter==actor,"returned to gourd legally");
    }
    private IEnumerator Start()
    {
        yield return null;Screen.SetResolution(1600,900,false);
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId="twin_citadels",mapSeed=1719,characterSpawnSeed=1719,startingPlayerIndex=1,
            players=Enumerable.Range(1,2).Select(p=>new ObjectHeadPlayerAssignment{playerIndex=p,allianceId=p,characters=new[]{ObjectHeadCharacterKind.Gourd,ObjectHeadCharacterKind.Magnet,ObjectHeadCharacterKind.Bomb}}).ToArray()});
        SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.7f);
        var turns=FindAnyObjectByType<TurnManager>();var actor=turns.Characters.First(c=>c.GetComponent<DemoSkillSelector>().CharacterKind==ObjectHeadCharacterKind.Gourd);
        if(turns.CurrentCharacter!=actor)yield return CycleTo(turns,actor);
        var gourd=actor.GetComponent<ObjectHeadGourd>();var selector=actor.GetComponent<DemoSkillSelector>();var fire=actor.GetComponent<SkillFireController>();
        Check(gourd!=null && gourd.Choices.Count==ObjectHeadContent.Load().characters.Length*3+ObjectHeadContent.Load().commonHeads.Length-3,"catalogue covers other characters and all common heads");
        Check(gourd.Choices.All(c=>gourd.Remaining(c)==1),"each head starts with one charge");
        selector.SetSkillIndex(2);yield return null;var hud=FindAnyObjectByType<ObjectHeadBattleScreen>();
        Check(ObjectHeadHeadInventoryPanel.AnyOpen,"third skill opens finite inventory");
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"gourd-inventory.png"));hud.headInventory.Close();
        var helmet=gourd.Choices.First(c=>c.common==CommonHeadType.IronHelmet);gourd.Select(helmet.id);fire.Fire(1);
        Check(actor.GetComponent<CharacterCombat>().ShieldAbsorption>0 && gourd.Remaining(helmet)==0,"chosen common effect executes and consumes exactly one gourd charge");
        Check(selector.GetRemainingCooldown(2)==0,"inventory skill has no cooldown");
        yield return CycleTo(turns,actor);selector.SetSkillIndex(2,false);fire.Fire(1);
        Check(!turns.ActionUsedThisTurn && !gourd.Select(helmet.id),"exhausted choice cannot consume a turn or be selected");
        selector.SetSkillIndex(1,false);fire.Fire(1);
        Check(gourd.Remaining(helmet)==1 && gourd.Choices.Where(c=>c!=helmet).All(c=>gourd.Remaining(c)==2),"refill adds one to every head");
        Check(selector.GetRemainingCooldown(1)==5,"refill has long cooldown");
        yield return CycleTo(turns,actor);selector.SetSkillIndex(0,false);int total=gourd.Capture().Sum(c=>c.count);
        fire.Fire(.7f);Check(gourd.Choices.Any(c=>c.id==gourd.LastResolvedId) && total==gourd.Capture().Sum(c=>c.count),"random draw resolves at fire without spending finite arsenal");
        // Fresh scene avoids random projectile side effects contaminating the swept-hit fixture.
        SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.7f);turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        var chars=turns.Characters;foreach(var c in chars){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;c.transform.position=new Vector3(-18,22);}
        chars[0].transform.position=new Vector3(-9,16);chars[1].transform.position=new Vector3(0,16);chars[3].transform.position=new Vector3(9,16);Physics2D.SyncTransforms();
        var terrain=FindAnyObjectByType<TerrainManager>();terrain.CreateCircle(new Vector2(0,16),48,TerrainType.Created,null);
        var skill=ObjectHeadContent.Load().Common(CommonHeadType.RetroTV).skill.Resolve(null);bool done=false;
        Check(turns.TryBeginAction(turns.CurrentCharacter),"sweep begins a legal action before deferred damage");
        IEnumerator Sweep(){yield return ObjectHeadRainbowSweep.Play(new Vector2(0,16),chars[0].GetComponent<CharacterCombat>(),terrain,skill);done=true;}
        StartCoroutine(Sweep());yield return new WaitForSeconds(ObjectHeadPresentation.Load().rainbowSeconds*.5f);
        if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"rainbow-sweep.png"));while(!done)yield return null;
        Check(ObjectHeadRainbowSweep.LastHitCount==3,"sweep hits owner ally and enemy exactly once");
        Check(new[]{chars[0],chars[1],chars[3]}.All(c=>c.GetComponent<CharacterCombat>().PendingDamage==skill.maxDamage),"equal sweep damage without frame-rate stacking");
        Check(!terrain.IsSolidWorld(new Vector2(0,16)) && chars[2].GetComponent<CharacterCombat>().PendingDamage==0,"sweep destroys strip but excludes off-height characters");
        var audio=Resources.Load<ObjectHeadAudioLibrary>("ObjectHeadAudioLibrary");Check(audio.helicopterLoop!=null&&audio.rainbowLoop!=null&&audio.supportFinal!=null,"special sounds are authored clip assets");
        var source=ObjectHeadSpecialAudio.Play("rotor",null,true);float old=ObjectHeadAudio.SfxVolume;ObjectHeadAudio.SfxVolume=0;yield return null;
        Check(source!=null&&source.volume==0,"SFX slider applies to active loop");ObjectHeadAudio.SfxVolume=old;if(source!=null)Destroy(source.gameObject);
        Check(ObjectHeadPresentation.Load().supportLauncherPilot!=ObjectHeadPresentation.Load().supportPilot,"launcher and MG have distinct pilot artwork");
        // Validate the real AI hover planner and legal skill execution on two separated islands.
        SceneManager.LoadScene("TwinCitadels");yield return new WaitForSeconds(.7f);turns=FindAnyObjectByType<TurnManager>();turns.SetTrainingTimerPaused(true);
        actor=turns.Characters.First(c=>c.GetComponent<DemoSkillSelector>().CharacterKind==ObjectHeadCharacterKind.Magnet);
        for(int n=0;n<12 && turns.CurrentCharacter!=actor;n++){turns.EndCurrentTurn();yield return null;}
        var enemy=turns.Characters.First(c=>!ObjectHeadAIPlanner.SameTeam(actor,c));
        foreach(var c in turns.Characters){c.UseNetworkInput=true;c.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;c.transform.position=new Vector3(15,22);}
        terrain=FindAnyObjectByType<TerrainManager>();terrain.DestroyCircle(new Vector2(-6,18),300);
        terrain.CreateCircle(new Vector2(-9,15),64,TerrainType.Created,null);terrain.CreateCircle(new Vector2(-3,15),64,TerrainType.Created,null);
        actor.transform.position=new Vector3(-9,17.8f);enemy.transform.position=new Vector3(-3,17.8f);Physics2D.SyncTransforms();
        var actorBody=actor.GetComponent<Rigidbody2D>();actorBody.constraints=RigidbodyConstraints2D.FreezeRotation;actorBody.linearVelocity=Vector2.zero;
        yield return new WaitForSeconds(.3f);
        bool planned=ObjectHeadAIHover.TryPlan(actor,enemy,terrain,out var goal);Check(planned,"AI finds safe hover landing across a gap");
        if(planned)
        {
            var start=actor.transform.position;int uses=ObjectHeadAIHover.Uses;bool finished=false;
            IEnumerator Hover(){yield return ObjectHeadAIHover.Move(actor,goal,turns,turns.TurnSerial);finished=true;}
            StartCoroutine(Hover());yield return new WaitForSeconds(1);
            Debug.Log("[COMPLETION_DETAIL] hover start="+start+" actual="+actor.transform.position+" goal="+goal);
            Check(actor.IsVacuumHovering && actor.transform.position.y>start.y+.3f,"AI uses charged hover skill and actually rises");
            if(folder!=null)yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder,"ai-hover-crossing.png"));
            while(!finished)yield return null;
            Check(ObjectHeadAIHover.Uses==uses+1 && actor.transform.position.x>start.x+1 && actor.GetComponent<DemoSkillSelector>().GetRemainingCooldown(2)>0,"AI crosses toward target and pays skill cooldown");
        }
        var cues=ObjectHeadActionCues.Load();Check(cues!=null&&cues.revealPrefab!=null&&cues.refillPrefab!=null&&cues.hoverPrefab!=null&&cues.televisionPrefab!=null,"all new action cues are editable authored assets");
        Debug.Log(failed?"[COMPLETION_FAIL]":"[COMPLETION_PASS] finite arsenal, random/refill, TV sweep, sound and support variants");Application.Quit(failed?2:0);
    }
}

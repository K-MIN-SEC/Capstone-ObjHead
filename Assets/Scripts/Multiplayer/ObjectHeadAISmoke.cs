using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadAISmoke : MonoBehaviour
{
    private bool aiFired;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadAISmoke"))return;
        var root=new GameObject("AISmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadAISmoke>();
    }
    private void OnEnable()=>SkillFireController.FireCommitted+=Fired;
    private void OnDisable()=>SkillFireController.FireCommitted-=Fired;
    private void Fired(SkillFireController fire,float power,Vector2 aim,int skill)
    {
        if(fire.GetComponent<ObjectHeadTeamMember>()?.PlayerIndex==2)aiFired=true;
    }
    private IEnumerator Start()
    {
        yield return null;
        var catalog=ObjectHeadContent.Load();
        var roster=ObjectHeadAISettings.CreateRoster(catalog,2,new System.Random(916),true);
        ObjectHeadAIProfile beginner=ObjectHeadAISettings.Profile(ObjectHeadAIDifficulty.Beginner);
        ObjectHeadAIProfile normal=ObjectHeadAISettings.Profile(ObjectHeadAIDifficulty.Normal);
        ObjectHeadAIProfile pro=ObjectHeadAISettings.Profile(ObjectHeadAIDifficulty.Pro);
        bool optionsPass=roster.Length==catalog.CharactersPerPlayer(2) && roster.All(kind=>catalog.Character(kind)!=null) &&
            beginner.decisionDelay>normal.decisionDelay && normal.decisionDelay>pro.decisionDelay &&
            beginner.aimErrorDegrees>normal.aimErrorDegrees && normal.aimErrorDegrees>pro.aimErrorDegrees &&
            beginner.powerError>normal.powerError && pro.aimErrorDegrees==0 && pro.powerError==0 &&
            ObjectHeadAITuning.Load().PowerSamples(ObjectHeadAIDifficulty.Beginner)==ObjectHeadAITuning.Load().PowerSamples(ObjectHeadAIDifficulty.Normal) &&
            ObjectHeadAITuning.Load().PowerSamples(ObjectHeadAIDifficulty.Normal)==ObjectHeadAITuning.Load().PowerSamples(ObjectHeadAIDifficulty.Pro);
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=catalog.maps[0].id,mapSeed=160916,characterSpawnSeed=160916,startingPlayerIndex=2,
            players=new[]{
                new ObjectHeadPlayerAssignment{playerIndex=1,allianceId=1,username="Player",characters=catalog.DefaultSelection(2)},
                new ObjectHeadPlayerAssignment{playerIndex=2,allianceId=2,username="AI 1",isAi=true,aiDifficulty=ObjectHeadAIDifficulty.Pro,characters=roster}
            }});
        SceneManager.LoadScene(catalog.maps[0].sceneName);
        yield return new WaitForSeconds(.08f);
        var initialTurns=FindAnyObjectByType<TurnManager>();
        var initialActor=initialTurns.CurrentCharacter;
        // Isolate decision quality from the intentionally wide opening spawn distance.
        // Movement is checked separately; here a reachable enemy must produce a real shot.
        var enemy=initialTurns.Characters.First(c=>c.GetComponent<ObjectHeadTeamMember>().PlayerIndex==1);
        enemy.transform.position=initialActor.transform.position+new Vector3(initialActor.transform.position.x>0?-7:7,2,0);
        enemy.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        Physics2D.SyncTransforms();
        bool inputPass=!initialTurns.CanLocalUserControl(initialActor);
#if ENABLE_INPUT_SYSTEM
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        int serial=initialTurns.TurnSerial,selected=initialActor.GetComponent<DemoSkillSelector>().SelectedSkillIndex;
        Vector2 aimBefore=initialActor.GetComponent<AimController>().AimDirection;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(
            UnityEngine.InputSystem.Key.D,UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.Digit2,
            UnityEngine.InputSystem.Key.UpArrow,UnityEngine.InputSystem.Key.Space,UnityEngine.InputSystem.Key.Tab));
        UnityEngine.InputSystem.InputSystem.Update();
        initialActor.SendMessage("Update");initialTurns.SendMessage("Update");
        inputPass &= initialTurns.TurnSerial==serial && initialActor.InputMoveX==0 &&
            !initialActor.GetComponent<PowerChargeController>().IsCharging &&
            initialActor.GetComponent<DemoSkillSelector>().SelectedSkillIndex==selected &&
            initialActor.GetComponent<AimController>().AimDirection==aimBefore;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
        UnityEngine.InputSystem.InputSystem.Update();UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
#endif
        GameStartData.Instance.localMatch=false;
        initialTurns.ConfigureNetworkControl(1,true);
        inputPass &= !initialTurns.CanLocalUserControl(initialActor);
        initialTurns.ConfigureNetworkControl(2,true);
        inputPass &= initialTurns.CanLocalUserControl(initialActor);
        GameStartData.Instance.localMatch=true;initialTurns.ConfigureNetworkControl(0,false);
        Debug.Log("[AI_DETAIL] injected input and network seat ownership="+inputPass);
        float deadline=Time.realtimeSinceStartup+12;
        while(!aiFired && Time.realtimeSinceStartup<deadline)yield return null;
        var turns=FindAnyObjectByType<TurnManager>();
        bool ownership=turns!=null && !turns.CanLocalUserControl(turns.CurrentCharacter) && !turns.CurrentCharacter.AcceptsLocalInput;
        bool pass=optionsPass && inputPass && aiFired && ObjectHeadAIDirector.ActionsTaken>0 && turns?.ActionUsedThisTurn==true && ownership && ObjectHeadAIDirector.CandidatesEvaluated>0;
        Debug.Log($"[AI_DETAIL] moved={ObjectHeadAIDirector.DistanceMoved:F3}, humanInputBlocked={ownership}");
        Debug.Log(pass?"[AI_PASS] random roster, three difficulty profiles, real skill action":"[AI_FAIL] AI options or combat action failed");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(pass?0:2);
#else
        Application.Quit(pass?0:2);
#endif
    }
}

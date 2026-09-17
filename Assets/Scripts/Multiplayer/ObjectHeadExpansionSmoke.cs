using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadExpansionSmoke:MonoBehaviour
{
    private bool failed;
    private float deadline;
    private void Awake(){deadline=Time.realtimeSinceStartup+90;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[EXPANSION_FAIL] Timeout");Application.Quit(2);}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadExpansionSmoke"))return;
        var root=new GameObject("ExpansionSmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadExpansionSmoke>();
    }
    private void Check(bool ok,string name){if(!ok){failed=true;Debug.LogError("[EXPANSION_FAIL] "+name);}else Debug.Log("[EXPANSION_CHECK] "+name);}
    private void OnEnable(){Application.logMessageReceived+=Log;}
    private void OnDisable(){Application.logMessageReceived-=Log;}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)failed=true;}
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(.3f);
        var catalog=ObjectHeadContent.Load();
        Check(catalog.characters.Length>=6,"six selectable characters");
        Check(catalog.commonHeads.Length>=8,"eight common head definitions including lock");
        var many=Enumerable.Range(0,80).Select(i=>new ObjectHeadCharacterDefinition{kind=(ObjectHeadCharacterKind)i,nameKey="head_"+i,roleKey=i%2==0?"role_support":"role_damage"}).ToArray();
        Check(ObjectHeadCharacterBrowser.Filter(many,"","role_all",s=>s).Length==80,"80 character catalog");
        Check(ObjectHeadCharacterBrowser.Filter(many,"","role_support",s=>s).Length==40,"80 entry role filter");
        Check(ObjectHeadCharacterBrowser.Filter(many,"head_79","role_all",s=>s).Length==1,"80 entry exact search");
        GameStartData.Apply(new GameStartData{localMatch=true,mode=ObjectHeadMatchMode.Duel,playerCount=2,mapId=catalog.maps[0].id,mapSeed=916,characterSpawnSeed=916,startingPlayerIndex=1,
            players=new[]{new ObjectHeadPlayerAssignment{playerIndex=1,allianceId=1,characters=new[]{ObjectHeadCharacterKind.Revolver,ObjectHeadCharacterKind.Magnet,ObjectHeadCharacterKind.Kettle}},new ObjectHeadPlayerAssignment{playerIndex=2,allianceId=2,characters=new[]{ObjectHeadCharacterKind.Bulb,ObjectHeadCharacterKind.Seed,ObjectHeadCharacterKind.Bomb}}}});
        SceneManager.LoadScene(catalog.maps[0].sceneName);yield return new WaitForSeconds(.5f);
        var turns=FindAnyObjectByType<TurnManager>();var targets=turns.Characters.Select(c=>c.GetComponent<CharacterCombat>()).ToArray();
        Check(targets.Length==6,"all six characters spawned");
        foreach(var c in turns.Characters)
        {
            var v=c.GetComponent<CharacterVisual>();var selector=c.GetComponent<DemoSkillSelector>();
            var head=c.transform.Find("HeadRenderer").GetComponent<SpriteRenderer>();
            for(int slot=0;slot<3;slot++)
            {
                Check(v.GetSkillHeadSprite(slot)!=null,c.name+" head "+slot);
                selector.SetSkillIndex(slot);
                Check(Mathf.Abs(Mathf.Max(head.bounds.size.x,head.bounds.size.y)-catalog.characterHeadVisualSize)<.005f,c.name+" skill "+slot+" uniform head size");
                Check(selector.GetCurrentSkillSettings().skillId==((int)selector.CharacterKind+1)*10+slot+1,c.name+" skill ID "+slot);
                foreach(var common in catalog.commonHeads)
                {
                    v.SetTemporaryCommonHead(common.sprite);
                    Check(Mathf.Abs(Mathf.Max(head.bounds.size.x,head.bounds.size.y)-catalog.characterHeadVisualSize)<.005f,c.name+" skill "+slot+" equip "+common.type+" size");
                    v.RestoreUniqueHead();
                    Check(Mathf.Abs(Mathf.Max(head.bounds.size.x,head.bounds.size.y)-catalog.characterHeadVisualSize)<.005f,c.name+" restored size");
                }
            }
        }
        foreach(var target in targets){target.TakeDamage(40);target.ApplyPendingDamage();}
        Vector2 center=targets[0].transform.position;
        targets[1].transform.position=center+Vector2.right*.6f;targets[2].transform.position=center+Vector2.left*.6f;
        Physics2D.SyncTransforms();
        int[] before=targets.Select(c=>c.CurrentHp).ToArray();
        ObjectHeadAreaHealing.Apply(center,1.4f,15);
        Check(targets[0].CurrentHp==before[0]+15,"heals self");Check(targets[2].CurrentHp==before[2]+15,"heals ally");Check(targets[1].CurrentHp==before[1]+15,"heals opponent");
        targets[0].Heal(999);Check(targets[0].CurrentHp==targets[0].MaxHp,"healing capped at max HP");
        targets[0].GrantShield(25);int hp=targets[0].CurrentHp;targets[0].TakeDamage(30);targets[0].ApplyPendingDamage();Check(targets[0].CurrentHp==hp-5 && targets[0].ShieldAbsorption==0,"helmet absorbs next hit only");
        targets[1].GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        ObjectHeadMagneticPulse.Apply(center,2,0,6,true);Check(targets[1].GetComponent<Rigidbody2D>().linearVelocity.x<0,"magnet pulls toward center");
        targets[1].GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        ObjectHeadMagneticPulse.Apply(center,2,0,6,false);Check(targets[1].GetComponent<Rigidbody2D>().linearVelocity.x>0,"magnet pushes away");
        var revolver=catalog.Character(ObjectHeadCharacterKind.Revolver).prefab.GetComponent<DemoSkillSelector>();
        foreach(var entry in catalog.commonHeads)
        {
            var item=CommonHeadItem.Create(entry.type,center+Vector2.up*8,entry.sprite);
            var renderer=item.GetComponent<SpriteRenderer>();
            Check(Mathf.Abs(Mathf.Max(renderer.bounds.size.x,renderer.bounds.size.y)-entry.worldVisualSize)<.01f,entry.type+" world pickup size");
            Check(renderer.sprite==entry.sprite,entry.type+" field uses catalog artwork");
            if((int)entry.type<=3)Check(entry.sprite.name.StartsWith("LegacyCommonHeads_"),entry.type+" legacy artwork replaced");
            var inventory=FindAnyObjectByType<PlayerInventoryManager>().GetInventory(1);
            Check(inventory.TryAdd(entry.type,out int index),entry.type+" inventory addition");
            Check(inventory.GetSlotSprite(index)==entry.sprite,entry.type+" inventory artwork");
            var use=targets[0].GetComponent<CommonHeadUseController>();
            Check(use.TrySelectCommonHeadSlot(index),entry.type+" selection");
            Check(targets[0].GetComponent<CharacterVisual>().CurrentHeadSprite==entry.sprite,entry.type+" equipped artwork");
            use.CancelSelectionAndRestoreUniqueHead();inventory.TryConsume(index,out _);
            Destroy(item.gameObject);
        }
        yield return CheckCaptivity(turns,targets);
        var terrain=FindAnyObjectByType<TerrainManager>();var at=center+Vector2.up*3;
        terrain.CreateCircle(at,30,TerrainType.Cloud,null);Check(terrain.IsSolidWorld(at),"cloud creates collidable terrain");terrain.DestroyCircle(at,40);Check(!terrain.IsSolidWorld(at),"cloud remains destructible");
        CheckTerrainReplay(terrain);
#if ENABLE_INPUT_SYSTEM
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var inputTarget=turns.CurrentCharacter;inputTarget.SetControlEnabled(true);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
        UnityEngine.InputSystem.InputSystem.Update();inputTarget.SendMessage("Update");
        inputTarget.SendMessage("FixedUpdate");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
        UnityEngine.InputSystem.InputSystem.Update();inputTarget.SendMessage("Update");
        Check(!inputTarget.InputJumpHeld && inputTarget.ConsumeJumpPress(),"short jump retained after release and physics step");
        Check(!inputTarget.ConsumeJumpPress(),"jump press consumed exactly once");
        UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
#endif
        Debug.Log(failed?"[EXPANSION_FAIL] checks failed":$"[EXPANSION_PASS] {catalog.characters.Length} characters / {catalog.characters.Length*3} skill heads / {catalog.commonHeads.Length} common heads; uniform sizes, heal factions, shield, magnet, cloud, lock captivity");
        Application.Quit(failed?2:0);
    }

    private IEnumerator CheckCaptivity(TurnManager turns,CharacterCombat[] targets)
    {
        Vector2 center=new Vector2(0,8);
        for(int i=0;i<targets.Length;i++)
        {
            targets[i].transform.position=center+Vector2.right*(i<2?(i==0?-.2f:.2f):6+i);
            targets[i].GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        }
        Physics2D.SyncTransforms();
        int firstHp=targets[0].CurrentHp,secondHp=targets[1].CurrentHp;
        int captured=ObjectHeadCaptivity.CaptureArea(center,.85f);
        var first=targets[0].GetComponent<ObjectHeadCaptivity>();
        var second=targets[1].GetComponent<ObjectHeadCaptivity>();
        Check(captured==2,"lock captures every faction inside radius only");
        Check(first!=null && first.IsCaptured && second!=null && second.IsCaptured,"lock captures ally and opponent");
        Check(!targets.Skip(2).Any(ObjectHeadCaptivity.Captured),"lock ignores characters outside radius");
        Check(!first.Capture() && first.RemainingOwnTurns==2,"lock cannot extend an active capture");

        int shield=targets[0].ShieldAbsorption;
        targets[0].TakeDamage(50);targets[0].ApplyPendingDamage();
        targets[0].ApplyKnockback(Vector2.right*10);
        Check(targets[0].CurrentHp==firstHp && targets[0].ShieldAbsorption==shield,"captive blocks outside damage without consuming shield");
        Check(targets[0].GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude<.001f,"captive blocks knockback");

        float timeout=Time.realtimeSinceStartup+8;
        while((first.IsCaptured || second.IsCaptured) && Time.realtimeSinceStartup<timeout)
        {
            var current=turns.CurrentCharacter;
            // A direct smoke-test capture can land after this turn's TurnStarted
            // event. End that one normally; once the captivity coroutine owns the
            // turn, leave it alone so it can apply its pulse and advance itself.
            if(current!=null && current.GetComponent<ObjectHeadCaptivity>()?.IsSkippingTurn!=true)
                turns.EndCurrentTurn();
            yield return null;
        }
        Check(!first.IsCaptured && !second.IsCaptured,"lock releases both targets after two rounds");
        Check(targets[0].CurrentHp==firstHp-12 && targets[1].CurrentHp==secondHp-12,"lock applies six unavoidable damage per captured round");
        Check((targets[0].GetComponent<Rigidbody2D>().constraints&RigidbodyConstraints2D.FreezeAll)!=RigidbodyConstraints2D.FreezeAll,"lock restores physics after release");
    }

    private void CheckTerrainReplay(TerrainManager terrain)
    {
        // Reuse the same clean patch, then move the collider before replay. A client
        // with a different interpolated position must still receive identical pixels.
        Vector2Int pixel=new Vector2Int(terrain.WidthPx/2,terrain.HeightPx-100);
        Vector2 point=terrain.PixelToWorld(pixel);
        var blocker=new GameObject("TerrainReplayBlocker").AddComponent<BoxCollider2D>();
        blocker.size=new Vector2(.18f,.8f);
        foreach(var type in new[]{TerrainType.Created,TerrainType.Cloud})
        {
            terrain.DestroyCircle(point,85);blocker.transform.position=point;Physics2D.SyncTransforms();
            TerrainEditOperation operation=default;
            Action<TerrainEditOperation> capture=op=>operation=op;terrain.OperationApplied+=capture;
            bool made=terrain.CreateEllipseDeferred(point,60,40,type,new[]{blocker});
            terrain.FlushDeferredTerrainChanges();terrain.OperationApplied-=capture;
            var expected=SamplePatch(terrain,pixel,65);
            Check(made && operation.excludedPixelRuns?.Length>0,type+" captures collider clearance");
            terrain.DestroyCircle(point,85);blocker.transform.position=point+Vector2.right*10;Physics2D.SyncTransforms();
            operation=JsonUtility.FromJson<TerrainEditOperation>(JsonUtility.ToJson(operation));
            terrain.ApplyOperation(operation);
            Check(expected.SequenceEqual(SamplePatch(terrain,pixel,65)),type+" exact terrain replay with moved blocker");
            operation.excludedPixelRuns=new[]{-1,5};
            Check(!terrain.ApplyOperation(operation),"invalid terrain runs rejected");
        }
        Destroy(blocker.gameObject);terrain.DestroyCircle(point,85);
    }
    private static TerrainType[] SamplePatch(TerrainManager terrain,Vector2Int center,int radius)
    {
        var result=new System.Collections.Generic.List<TerrainType>();
        for(int y=center.y-radius;y<=center.y+radius;y++)for(int x=center.x-radius;x<=center.x+radius;x++)
            result.Add(terrain.GetTerrainTypeWorld(terrain.PixelToWorld(new Vector2Int(x,y))));
        return result.ToArray();
    }
}

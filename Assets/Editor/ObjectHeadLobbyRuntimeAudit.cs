using System;
using UnityEngine;

/// <summary>Pure roster/ownership checks; never connects to EOS or modifies a scene.</summary>
public static class ObjectHeadLobbyRuntimeAudit
{
    public static void Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,label)=>
        {if(!ok)throw new InvalidOperationException("[LOBBY_RUNTIME_FAIL] "+label);checks++;};
        var catalog=ObjectHeadContent.Load();
        foreach(var mode in new[]{ObjectHeadMatchMode.FreeForAll,ObjectHeadMatchMode.Teams})
        foreach(int occupied in new[]{2,3,4})
        {
            var match=new GameStartData{mode=mode,playerCount=occupied};
            check(match.RosterPlayerCount==4,"four-seat mode retains its roster rule with "+occupied+" occupied seats: "+mode);
            check(catalog.ValidSelection(catalog.DefaultSelection(4),match.RosterPlayerCount),"four-seat selection is valid when starting under capacity");
            check(catalog.CharactersPerPlayer(match.RosterPlayerCount)>0,"under-capacity room cannot spawn an empty roster");
        }
        check(new GameStartData{mode=ObjectHeadMatchMode.Duel,playerCount=2}.RosterPlayerCount==2,"duel keeps its separate roster size");
        var data=new GameStartData{mode=ObjectHeadMatchMode.FreeForAll,playerCount=3,players=new[]{
            new ObjectHeadPlayerAssignment{userId="host",playerIndex=1},
            new ObjectHeadPlayerAssignment{userId="guest",playerIndex=2},
            new ObjectHeadPlayerAssignment{userId="ai:3",playerIndex=3,isAi=true,aiDifficulty=ObjectHeadAIDifficulty.Pro}}};
        check(data.IsAiPlayer(3) && !data.IsAiPlayer(1) && !data.IsAiPlayer(4),"AI ownership belongs to an explicit occupied assignment");
        check(ObjectHeadAIDirector.CanDriveAi(data,true),"online host drives AI");
        check(!ObjectHeadAIDirector.CanDriveAi(data,false),"online guest never simulates AI");
        data.localMatch=true;
        check(ObjectHeadAIDirector.CanDriveAi(data,false),"offline AI remains available without a network host");
        data.localMatch=false;data.dedicatedAuthority=true;
        check(!ObjectHeadAIDirector.CanDriveAi(data,true),"room host cannot become a second AI authority in dedicated mode");
        data.dedicatedAuthority=false;
        check(ObjectHeadGameplayBridge.CanSubmitLocalAction(data,3,1,true),"host can publish bot fire and use its common heads");
        check(!ObjectHeadGameplayBridge.CanSubmitLocalAction(data,3,1,false),"guest cannot issue bot actions");
        check(!ObjectHeadGameplayBridge.CanSubmitLocalAction(data,2,1,true),"host local input cannot impersonate another human");
        check(ObjectHeadGameplayBridge.CanSubmitLocalAction(data,2,2,false),"guest can still issue their own actions");
        check(!ObjectHeadGameplayBridge.CanSubmitLocalAction(data,-1,-1,true),"missing character assignment cannot submit an action");
        var replica=JsonUtility.FromJson<GameStartData>(JsonUtility.ToJson(data));
        check(replica.playerCount==3 && replica.RosterPlayerCount==4,"wire round trip keeps occupancy distinct from roster rule");
        check(replica.IsAiPlayer(3) && replica.players[2].aiDifficulty==ObjectHeadAIDifficulty.Pro,"wire round trip preserves AI ownership and difficulty");
        check(!ObjectHeadAIDirector.CanDriveAi(null,true),"no match cannot spawn AI");
        data.players[2].isAi=false;
        check(!ObjectHeadAIDirector.CanDriveAi(data,true),"human-only room does not spawn an AI director");
        Debug.Log("[LOBBY_RUNTIME_AUDIT_PASS] "+checks+" roster, AI authority, and action ownership checks");
    }
}

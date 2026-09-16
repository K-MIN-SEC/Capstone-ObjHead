using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ObjectHeadAuthoritySmoke:MonoBehaviour
{
    private float deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-objectHeadAuthoritySmoke"))return;
        var root=new GameObject("AuthoritySmoke");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadAuthoritySmoke>();
    }
    private void Awake(){deadline=Time.realtimeSinceStartup+100;}
    private void Update(){if(Time.realtimeSinceStartup>deadline){Debug.LogError("[AUTHORITY_GAME_FAIL] watchdog");Application.Quit(2);}}
    private async void Start()
    {
        try
        {
            var network=ObjectHeadNetworkManager.Instance;
            network.ConfigureServer("http","127.0.0.1",17350,"authority-test-key");
            await network.ConnectAsync("AuthorityTest",Guid.NewGuid().ToString("N"));
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-objectHeadAuthoritySmokeMode");
            var mode=at>=0?(ObjectHeadMatchMode)int.Parse(args[at+1]):ObjectHeadMatchMode.Duel;
            int count=ObjectHeadContent.Load().Mode(mode).players;
            var roster=new[]{ObjectHeadCharacterKind.Revolver,ObjectHeadCharacterKind.Magnet,ObjectHeadCharacterKind.Kettle}.Take(ObjectHeadContent.Load().CharactersPerPlayer(count)).ToArray();
            await network.StartQuickMatchAsync(mode);
            await Until(()=>network.LobbyState?.players?.Length==count,"lobby");
            await network.SetSelectionAsync(roster);
            await Until(()=>network.LobbyState.players.First(p=>p.userId==network.LocalUserId).characters.Length==roster.Length,"selection");
            await network.SetReadyAsync(true);
            await Until(()=>network.LobbyState.authorityReady && network.LobbyState.players.All(p=>p.ready),"worker and readiness");
            if(network.IsHost)await network.StartGameAsync();
            await Until(()=>FindAnyObjectByType<ObjectHeadDedicatedGameplay>()?.IsReady==true,"dedicated gameplay");
            var bridge=FindAnyObjectByType<ObjectHeadDedicatedGameplay>();
            await Until(()=>bridge.StatesReceived>2,"authoritative snapshots");
            if(network.IsCombatAuthority)throw new Exception("A player became combat authority");
            var turns=FindAnyObjectByType<TurnManager>();
            int seat=GameStartData.Instance.players.First(p=>p.userId==network.LocalUserId).playerIndex;
            int firstTurn=turns.TurnSerial;
            int firstPlayer=turns.CurrentPlayerIndex;
            var inventory=FindAnyObjectByType<PlayerInventoryManager>().GetInventory(firstPlayer);
            await Until(()=>inventory.GetSlot(0)==CommonHeadType.IronHelmet,"server inventory replication");
            if(firstPlayer==seat)
            {
                var use=turns.CurrentCharacter.GetComponent<CommonHeadUseController>();
                if(!use.TrySelectCommonHeadSlot(0)||!use.UseSelectedHead(.5f))throw new Exception("common request refused");
                // Replay the request before acknowledgement: the server must still consume only once.
                use.UseSelectedHead(.5f);
            }
            await Until(()=>bridge.CommonUsesReceived==1 && inventory.GetSlot(0)==CommonHeadType.None && turns.CurrentCharacter.GetComponent<CharacterCombat>().ShieldAbsorption==25,"helmet consumed once and shield synchronized");
            if(firstPlayer==seat)bridge.RequestEndTurn();
            await Until(()=>turns.TurnSerial>firstTurn,"server turn advance after common use");
            if(turns.CurrentPlayerIndex==seat)
            {
                var current=turns.CurrentCharacter;
                current.GetComponent<DemoSkillSelector>().SetSkillIndex(2);
                current.GetComponent<AimController>().SetAimDirection(Vector2.down);
                current.GetComponent<SkillFireController>().Fire(.15f);
            }
            await Until(()=>bridge.FiresReceived>0 && bridge.TerrainReceived>0 && bridge.ImpactsReceived>0,"server-calculated shot, terrain and impact presentation");
            await Task.Delay(1200);
            Debug.Log($"[AUTHORITY_GAME_PASS] mode={mode}, player={seat}, states={bridge.StatesReceived}, acceptedShots={bridge.FiresReceived}, commonUses={bridge.CommonUsesReceived}, impacts={bridge.ImpactsReceived}, terrain={bridge.TerrainReceived}, combatAuthority={network.IsCombatAuthority}");
            Application.Quit(0);
        }
        catch(Exception e){Debug.LogError("[AUTHORITY_GAME_FAIL] "+e);Application.Quit(2);}
    }
    private static async Task Until(Func<bool> test,string operation)
    {
        var until=DateTime.UtcNow.AddSeconds(35);
        while(!test()){if(DateTime.UtcNow>until)throw new TimeoutException(operation);await Task.Delay(100);}
    }
}

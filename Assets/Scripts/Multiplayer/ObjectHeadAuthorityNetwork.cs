using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

public sealed partial class ObjectHeadNetworkManager
{
    public const string AuthorityRuleset="authority-0916-v2";
    public bool UseDedicatedAuthority => config.UseDedicatedAuthority || HasCommandLineFlag("-objectHeadAuthority") || IsDedicatedWorker;
    public bool IsDedicatedWorker => HasCommandLineFlag("-objectHeadWorker");
    public bool IsCombatAuthority => UseDedicatedAuthority ? IsDedicatedWorker : IsHost;
    [Serializable] public sealed class AuthorityRoom { public string matchId,roomCode,workerTicket,error,mapId; public int players,capacity,mode; }
    [Serializable] public sealed class AuthorityRooms { public AuthorityRoom[] rooms=Array.Empty<AuthorityRoom>(); }
    [Serializable] private sealed class CreateRequest { public ObjectHeadRoomSettings settings; public bool @public=true; }
    [Serializable] private sealed class FindRequest { public string roomCode; }
    [Serializable] private sealed class WorkerRequest { public string key,matchId; }
    [Serializable] private sealed class Reason { public string reason; }
    private async Task<T> AuthorityRpc<T>(string name,object data)
    {
        EnsureConnected();var result=await client.RpcAsync(session,name,JsonUtility.ToJson(data));return JsonUtility.FromJson<T>(result.Payload);
    }
    public Task<AuthorityRooms> FindPublicRoomsAsync()=>AuthorityRpc<AuthorityRooms>("objecthead_authority_find",new ObjectHeadReadyRequest());
    private async Task CreateAuthoritativeRoomAsync(ObjectHeadRoomSettings settings)
    {
        EnsureConnected();await LeaveCurrentMatchAsync();settings=settings.Copy();settings.rulesetVersion=AuthorityRuleset;
        var room=await AuthorityRpc<AuthorityRoom>("objecthead_authority_create",new CreateRequest{settings=settings});
        await JoinAuthorityId(room.matchId);roomCode=room.roomCode;
    }
    private async Task JoinAuthoritativeRoomAsync(string code)
    {
        EnsureConnected();await LeaveCurrentMatchAsync();
        var room=await AuthorityRpc<AuthorityRoom>("objecthead_authority_find",new FindRequest{roomCode=code});
        await JoinAuthorityId(room.matchId);roomCode=room.roomCode;
    }
    public async Task JoinAuthorityId(string id)
    {
        currentMatch=await socket.JoinMatchAsync(id);lobbyState=null;
        await SendAsync(1,new ObjectHeadPlayerHello{username=displayName});
    }
    public async Task<bool> ClaimWorkerAsync(string id,string secret)
    {
        var claim=await AuthorityRpc<AuthorityRoom>("objecthead_authority_worker",new WorkerRequest{key=secret,matchId=id});
        if(string.IsNullOrEmpty(claim.workerTicket))return false;
        currentMatch=await socket.JoinMatchAsync(id,new System.Collections.Generic.Dictionary<string,string>{{"workerTicket",claim.workerTicket}});
        return true;
    }
    public Task<AuthorityRooms> FindWorkerRoomsAsync(string secret)=>AuthorityRpc<AuthorityRooms>("objecthead_authority_worker",new WorkerRequest{key=secret});
    private void HandleAuthorityMatchState(IMatchState state)
    {
        if(currentMatch==null || state.MatchId!=currentMatch.Id)return;
        try
        {
            string sender=state.UserPresence?.UserId??"";
            string json=Encoding.UTF8.GetString(state.State);
            // Nakama omits presence on server-originated messages. Never trust a player's claimed role.
            if(!string.IsNullOrEmpty(sender))
            {
                if(IsDedicatedWorker && state.OpCode==100)GameplayMessageReceived?.Invoke(100,sender,json);
                return;
            }
            switch(state.OpCode)
            {
                case 2:
                    var next=JsonUtility.FromJson<ObjectHeadLobbyState>(json);
                    if(next==null || next.matchId!=MatchId || next.protocolVersion!=ObjectHeadNetworkProtocol.ProtocolVersion)return;
                    if(lobbyState!=null && next.revision<=lobbyState.revision)return;
                    lobbyState=next;roomCode=next.roomCode;NotifyLobbyChanged();break;
                case 5: ApplyGameStart(JsonUtility.FromJson<GameStartData>(json));break;
                case 7: ApplyReturnToLobby();break;
                case 8: SetStatus(JsonUtility.FromJson<Reason>(json).reason);break;
                case 9: SessionInterrupted?.Invoke(JsonUtility.FromJson<Reason>(json).reason);break;
                default: if(state.OpCode>=100)GameplayMessageReceived?.Invoke(state.OpCode,"",json);break;
            }
        }
        catch(Exception exception){Debug.LogWarning("[Authority] Invalid server message: "+exception.Message);}
    }
}

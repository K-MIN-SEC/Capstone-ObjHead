using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ObjectHeadDedicatedWorker:MonoBehaviour
{
    private float heartbeat;
    private float started;
    private bool stopping;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker!=true)return;
        var root=new GameObject("DedicatedWorker");DontDestroyOnLoad(root);root.AddComponent<ObjectHeadDedicatedWorker>();
    }
    private async void Start()
    {
        started=Time.realtimeSinceStartup;
        try
        {
            var network=ObjectHeadNetworkManager.Instance;
            network.SessionInterrupted+=Stop;
            string secret=Environment.GetEnvironmentVariable("OBJECT_HEAD_WORKER_KEY");
            if(string.IsNullOrEmpty(secret)||secret.Length<32)throw new InvalidOperationException("Worker secret missing; refusing startup.");
            string host=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_HOST");
            string port=Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_PORT");
            network.ConfigureServer("http",host,int.TryParse(port,out int n)?n:7350,Environment.GetEnvironmentVariable("OBJECT_HEAD_SERVER_KEY"));
            await network.ConnectAsync("CombatWorker",Guid.NewGuid().ToString("N"));
            while(!stopping && !network.IsInMatch)
            {
                var rooms=await network.FindWorkerRoomsAsync(secret);
                foreach(var room in rooms.rooms??Array.Empty<ObjectHeadNetworkManager.AuthorityRoom>())
                    if(await network.ClaimWorkerAsync(room.matchId,secret)){Debug.Log("[WORKER] Claimed battle");break;}
                if(!network.IsInMatch)await Task.Delay(500);
            }
        }
        catch(Exception exception){Debug.LogError("[WORKER] "+exception.Message);Application.Quit(2);}
    }
    private void Update()
    {
        // A supervised worker serves one room only. A crashed supervisor cannot leave it forever.
        if(Time.realtimeSinceStartup-started>7500){Stop("worker_lifetime");return;}
        if(Time.unscaledTime<heartbeat)return;heartbeat=Time.unscaledTime+2;
        var network=ObjectHeadNetworkManager.Instance;
        if(network.IsInMatch)Beat(network);
    }
    private async void Beat(ObjectHeadNetworkManager network)
    {try{await network.SendGameplayMessageAsync(122,new ObjectHeadGameplayMessage());}catch{Stop("worker_disconnected");}}
    private void Stop(string reason){stopping=true;Debug.Log("[WORKER] Closing: "+reason);Application.Quit(0);}
}

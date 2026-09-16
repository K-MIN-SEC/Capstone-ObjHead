using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadTraining : MonoBehaviour
{
    public static bool Pending;
    private float nextReset;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    private static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(!Pending || GameStartData.Instance==null || !GameStartData.Instance.localMatch || Object.FindAnyObjectByType<TurnManager>()==null)return;
        Pending=false;new GameObject("TrainingMode").AddComponent<ObjectHeadTraining>();
    }
    private void Update()
    {
        // Training is isolated local play. Both sides can be controlled for practice.
        if(Time.time<nextReset)return;nextReset=Time.time+1;
        foreach(var selector in FindObjectsByType<DemoSkillSelector>(FindObjectsSortMode.None))selector.ResetCooldowns();
    }
    private void Start(){FindAnyObjectByType<TurnManager>()?.ConfigureTraining(ObjectHeadBalanceTable.Load()?.GetFloat("training.turn_seconds",3600) ?? 3600);}
    public static void ResetArena(){Pending=true;SceneManager.LoadScene(SceneManager.GetActiveScene().name);}
}

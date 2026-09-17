using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ObjectHeadTraining : MonoBehaviour
{
    public static bool Pending;
    public static ObjectHeadTraining Instance{get;private set;}
    public static bool Enabled => Instance!=null && GameStartData.Instance?.localMatch==true;
    private float nextReset;
    private TurnManager turns;
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
    private void Awake(){Instance=this;}
    private void OnDestroy(){if(Instance==this)Instance=null;}
    private void Start(){turns=FindAnyObjectByType<TurnManager>();turns?.ConfigureTraining(ObjectHeadBalanceTable.Load()?.GetFloat("training.turn_seconds",3600) ?? 3600);turns?.SetTrainingTimerPaused(true);}
    public static void ResetArena(){Pending=true;SceneManager.LoadScene(SceneManager.GetActiveScene().name);}
    public static void ToggleTimer(){if(Instance?.turns!=null)Instance.turns.SetTrainingTimerPaused(!Instance.turns.TrainingTimerPaused);}
    public static void HealAll(){foreach(var combat in FindObjectsByType<CharacterCombat>(FindObjectsSortMode.None))if(!combat.IsDead)combat.Heal(combat.MaxHp);}
    public static void SkipTurn(){Instance?.turns?.EndCurrentTurn();}
}

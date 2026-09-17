using UnityEngine;

/// <summary>Cosmetic-only cues; editable prefabs and timing never control combat rules.</summary>
[CreateAssetMenu(menuName="Object Head/Action Cues")]
public sealed class ObjectHeadActionCues : ScriptableObject
{
    public GameObject revealPrefab,refillPrefab,hoverPrefab,televisionPrefab;
    [Min(.05f)] public float hoverInterval=.18f;
    [Min(.1f)] public float televisionAnticipation=.35f;
    [Min(.05f)] public float windStreakLength=.55f;
    [Min(0)] public float windBend=.07f;
    private static ObjectHeadActionCues cached;
    public static ObjectHeadActionCues Load()=>cached!=null?cached:cached=Resources.Load<ObjectHeadActionCues>("ObjectHeadActionCues");
    public static ObjectHeadAbilityCue Show(GameObject prefab,Vector2 point,Sprite icon=null,Transform follow=null)
    {
        if(prefab==null || ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return null;
        var root=Instantiate(prefab,point,Quaternion.identity);var cue=root.GetComponent<ObjectHeadAbilityCue>();
        if(cue!=null)cue.Configure(icon,follow);return cue;
    }
    public static void Reveal(Transform owner,Sprite icon)=>Show(Load()?.revealPrefab,owner.position,icon,owner);
    public static void Refill(Transform owner)=>Show(Load()?.refillPrefab,owner.position,null,owner);
    public static void Hover(Vector2 point)=>Show(Load()?.hoverPrefab,point);
}

using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadActionCueAuthoring
{
    [MenuItem("Object Head/Install Missing Action Cues")]
    public static void Apply()
    {
        const string path="Assets/Resources/ObjectHeadActionCues.asset";
        var data=AssetDatabase.LoadAssetAtPath<ObjectHeadActionCues>(path);
        if(data==null){data=ScriptableObject.CreateInstance<ObjectHeadActionCues>();AssetDatabase.CreateAsset(data,path);}
        var flat=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Presentation/FlatEffects.png").OfType<Sprite>().ToArray();
        Sprite smoke=flat.First(s=>s.name=="Smoke"),spark=flat.First(s=>s.name=="Spark");
        var catalog=ObjectHeadContent.Load();
        if(data.revealPrefab==null)data.revealPrefab=Make("GourdReveal",smoke,spark,0,catalog);
        if(data.refillPrefab==null)data.refillPrefab=Make("GourdRefill",smoke,spark,1,catalog);
        if(data.hoverPrefab==null)data.hoverPrefab=Make("HoverDownwash",smoke,spark,2,catalog);
        if(data.televisionPrefab==null)data.televisionPrefab=Make("TelevisionTuneIn",smoke,spark,3,catalog);
        var art=ObjectHeadPresentation.Load();var profile=art.Find(112);
        if(profile!=null){profile.launchPrefab=art.Find(11).launchPrefab;profile.flightPrefab=art.Find(11).flightPrefab;}
        EditorUtility.SetDirty(art);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        Debug.Log("[ACTION_CUES] Authored reveal, refill, downwash and television anticipation prefabs.");
    }
    private static SpriteRenderer Sprite(Transform root,string name,Sprite image,Vector2 at,float size,Color color)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(root,false);go.transform.localPosition=at;
        var r=go.GetComponent<SpriteRenderer>();r.sprite=image;r.sortingOrder=52;r.color=color;
        if(image!=null)go.transform.localScale=Vector3.one*size/Mathf.Max(image.bounds.size.x,image.bounds.size.y);return r;
    }
    private static GameObject Make(string name,Sprite smoke,Sprite spark,int kind,ObjectHeadContent catalog)
    {
        string path="Assets/Prefabs/Effects/"+name+".prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        var root=new GameObject(name);var cue=root.AddComponent<ObjectHeadAbilityCue>();
        cue.offset=Vector2.up*(kind==2?-.1f:1.15f);cue.duration=kind==1?1:kind==2?.4f:kind==3?.35f:.9f;
        cue.rise=kind==2?-.5f:.18f;cue.iconSize=kind==3?.8f:.65f;
        if(kind!=2)
        {
            Sprite(root.transform,"Puff",smoke,Vector2.zero,1,new Color(1,1,1,.65f));
            cue.icon=Sprite(root.transform,"ResolvedHead",kind==1?catalog.Character(ObjectHeadCharacterKind.Gourd).portrait:null,Vector2.zero,cue.iconSize,Color.white);
            if(kind==1)cue.orbitIcons=Enumerable.Range(0,3).Select(i=>Sprite(root.transform,"RefillHead"+i,catalog.characters[i].portrait,Vector2.zero,.3f,Color.white)).ToArray();
            else for(int i=0;i<2;i++)Sprite(root.transform,"Accent"+i,spark,new Vector2(i==0?-.5f:.5f,.1f),.18f,Color.white);
        }
        else
        {
            for(int i=0;i<3;i++)Sprite(root.transform,"AirPuff"+i,smoke,new Vector2((i-1)*.17f,-Mathf.Abs(i-1)*.12f),.24f,new Color(.85f,.95f,1,.7f));
        }
        var result=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return result;
    }
}

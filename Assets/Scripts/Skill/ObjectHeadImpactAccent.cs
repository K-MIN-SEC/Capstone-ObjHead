using UnityEngine;

/// <summary>Short outlined contact accents. No actor animation, physics or gameplay RNG.</summary>
public sealed class ObjectHeadImpactAccent : MonoBehaviour
{
    public const int Budget=12;
    private sealed class Accent
    {
        public LineRenderer ink, light;
        public Vector2 point;
        public float started, size, duration;
        public bool active;
    }
    private static ObjectHeadImpactAccent instance;
    private readonly Accent[] pool=new Accent[Budget];
    private Material material;
    private int cursor;
    public static int ActiveCount { get; private set; }

    public static void Show(Vector2 point,float impulse)
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true || impulse<.05f ||
            ObjectHeadMicroFeedback.Load()?.effectsEnabled!=true) return;
        if(instance==null) new GameObject("ImpactAccentPool").AddComponent<ObjectHeadImpactAccent>();
        if(instance.material==null)return;
        var a=instance.pool[instance.cursor];
        instance.cursor=(instance.cursor+1)%Budget;
        if(!a.active)ActiveCount++;
        a.active=true; a.point=point; a.started=Time.unscaledTime;
        float weight=Mathf.Clamp01(impulse/.3f);
        a.duration=Mathf.Lerp(.11f,.2f,weight);
        a.size=Mathf.Lerp(.16f,.65f,weight);
        a.ink.enabled=a.light.enabled=true;
        Draw(a,0);
    }
    private void Awake()
    {
        instance=this; ActiveCount=0;
        var shader=Shader.Find("Sprites/Default");
        if(shader==null)return;
        material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        for(int i=0;i<Budget;i++)
            pool[i]=new Accent{ink=CreateLine("Ink",46),light=CreateLine("Light",47)};
    }
    private LineRenderer CreateLine(string label,int order)
    {
        var line=new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(transform,false); line.sharedMaterial=material;
        line.useWorldSpace=true; line.loop=true; line.positionCount=16;
        line.sortingOrder=order; line.numCornerVertices=0;
        line.enabled=false; return line;
    }
    private void Update()
    {
        if(material==null)return;
        foreach(var a in pool)
        {
            if(!a.active)continue;
            float t=(Time.unscaledTime-a.started)/a.duration;
            if(t>=1 || ObjectHeadMicroFeedback.Load()?.effectsEnabled!=true)
            {a.active=false;a.ink.enabled=a.light.enabled=false;ActiveCount--;continue;}
            Draw(a,t);
        }
    }
    private static void Draw(Accent a,float t)
    {
        var camera=Camera.main;
        float pixel=camera!=null ? camera.orthographicSize*2/Mathf.Max(1,Screen.height) : .01f;
        // Maintain contact readability at overview zoom without a full-screen flash.
        float size=Mathf.Max(a.size,pixel*8)*(1+.35f*t);
        a.ink.widthMultiplier=Mathf.Max(.035f,pixel*2);
        a.light.widthMultiplier=a.ink.widthMultiplier*.45f;
        float opacity=1-t*t;
        a.ink.startColor=a.ink.endColor=new Color(.08f,.07f,.045f,opacity);
        a.light.startColor=a.light.endColor=new Color(1f,.91f,.55f,opacity);
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI/8f+.18f;
            float radius=size*(i%2==0?1f:.53f);
            var p=(Vector3)(a.point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
            a.ink.SetPosition(i,p);a.light.SetPosition(i,p);
        }
    }
    private void OnDestroy()
    {
        if(material!=null)Destroy(material);
        if(instance==this){instance=null;ActiveCount=0;}
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bounded, scene-owned, cosmetic sprite pool. No physics, gameplay RNG or network messages.</summary>
public sealed class ObjectHeadMicroParticles : MonoBehaviour
{
    private struct Particle
    {
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float age,life,scale,angle,spin,gravity,endScale;
        public Color color;
        public bool alive;
    }
    private static ObjectHeadMicroParticles instance;
    private Particle[] particles;
    private ObjectHeadMicroFeedback config;
    private readonly System.Random cosmeticRandom=new System.Random();
    private int cursor,frame=-1,emittedThisFrame;
    private Camera view;
    public static int ActiveCount { get; private set; }
    public static int TotalEmitted { get; private set; }
    public static int Capacity=>instance!=null?instance.particles.Length:0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {instance=null;ActiveCount=0;TotalEmitted=0;SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
    private static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {if(FindAnyObjectByType<TerrainManager>()!=null)EnsurePool();}
    private static bool EnsurePool()
    {
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker==true)return false;
        var settings=ObjectHeadMicroFeedback.Load();
        if(settings==null || !settings.effectsEnabled)return false;
        if(instance==null)new GameObject("MicroFeedbackPool").AddComponent<ObjectHeadMicroParticles>();
        return instance!=null;
    }
    private void Awake()
    {
        if(instance!=null){Destroy(gameObject);return;}
        instance=this;config=ObjectHeadMicroFeedback.Load();view=Camera.main;
        particles=new Particle[Mathf.Clamp(config.particleBudget,16,512)];
        for(int i=0;i<particles.Length;i++)
        {
            var child=new GameObject("Particle",typeof(SpriteRenderer));child.transform.SetParent(transform,false);
            var renderer=child.GetComponent<SpriteRenderer>();renderer.sortingOrder=config.sortingOrder;renderer.enabled=false;
            particles[i].renderer=renderer;
        }
    }
    private void OnDestroy(){if(instance==this){instance=null;ActiveCount=0;}}
    private float Between(Vector2 range)=>Mathf.Lerp(Mathf.Min(range.x,range.y),Mathf.Max(range.x,range.y),(float)cosmeticRandom.NextDouble());
    private float Range(float a,float b)=>Mathf.Lerp(a,b,(float)cosmeticRandom.NextDouble());

    public static int Emit(ObjectHeadMicroCue cue,Vector2 point,Vector2 direction,float strength=1)
    {
        if(!EnsurePool())return 0;
        return instance.Burst(cue,point,direction,Mathf.Clamp(strength,.25f,2));
    }
    public static void TerrainDestroyed(Vector2 point,float radius,bool cloud=false)
    {
        float strength=Mathf.Clamp(radius,.6f,2);
        if(!cloud)Emit(ObjectHeadMicroCue.Debris,point,Vector2.up,strength);
        Emit(ObjectHeadMicroCue.Dust,point,Vector2.up,strength);
    }
    private int Burst(ObjectHeadMicroCue cue,Vector2 point,Vector2 direction,float strength)
    {
        var profile=config.Find(cue);if(profile?.sprite==null)return 0;
        if(view==null)view=Camera.main;
        if(view!=null && view.orthographic)
        {
            Vector2 d=point-(Vector2)view.transform.position;
            if(Mathf.Abs(d.x)>view.orthographicSize*view.aspect+config.visibilityMargin || Mathf.Abs(d.y)>view.orthographicSize+config.visibilityMargin)return 0;
        }
        if(frame!=Time.frameCount){frame=Time.frameCount;emittedThisFrame=0;}
        int count=Mathf.Min(Mathf.CeilToInt(profile.count*strength),Mathf.Max(0,config.emissionsPerFrame-emittedThisFrame));
        int made=0;float heading=Mathf.Atan2(direction.y,direction.x);
        float spriteSize=Mathf.Max(.001f,Mathf.Max(profile.sprite.bounds.size.x,profile.sprite.bounds.size.y));
        for(int attempt=0;attempt<particles.Length && made<count;attempt++)
        {
            int i=cursor;cursor=(cursor+1)%particles.Length;
            if(particles[i].alive)continue;
            ref Particle p=ref particles[i];
            float angle=heading+Range(-profile.spread,profile.spread)*Mathf.Deg2Rad;
            p.velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Between(profile.speed)*Mathf.Sqrt(strength);
            p.age=0;p.life=Mathf.Max(.05f,Between(profile.lifetime));p.scale=Mathf.Max(.01f,Between(profile.size))/spriteSize;
            p.gravity=profile.gravity;p.spin=Range(-profile.spin,profile.spin);p.angle=Range(-180,180);p.endScale=profile.endScale;p.color=profile.tint;p.alive=true;
            var r=p.renderer;r.sprite=profile.sprite;r.color=p.color;r.enabled=true;
            r.transform.position=point+new Vector2(Range(-1,1),Range(-.3f,.3f))*profile.scatter*strength;
            r.transform.localScale=Vector3.one*p.scale;r.transform.rotation=Quaternion.Euler(0,0,p.angle);
            ActiveCount++;made++;
        }
        emittedThisFrame+=made;TotalEmitted+=made;return made;
    }
    private void Update()
    {
        float dt=Time.deltaTime;
        for(int i=0;i<particles.Length;i++)
        {
            ref Particle p=ref particles[i];if(!p.alive)continue;
            p.age+=dt;
            if(!config.effectsEnabled || p.age>=p.life){p.alive=false;p.renderer.enabled=false;ActiveCount--;continue;}
            float t=p.age/p.life;
            p.velocity.y-=p.gravity*dt;p.renderer.transform.position+=(Vector3)(p.velocity*dt);
            p.angle+=p.spin*dt;p.renderer.transform.rotation=Quaternion.Euler(0,0,p.angle);
            p.renderer.transform.localScale=Vector3.one*(p.scale*Mathf.Lerp(1,p.endScale,t));
            Color color=p.color;color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,t));p.renderer.color=color;
        }
    }
}

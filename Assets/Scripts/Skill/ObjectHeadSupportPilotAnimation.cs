using UnityEngine;

/// <summary>Three authored poses per weapon, synchronized with the cosmetic muzzle event.</summary>
public sealed class ObjectHeadSupportPilotAnimation:MonoBehaviour
{
    private SpriteRenderer renderer2d;private Sprite[] frames;private float shotAge=10,frameSeconds=.06f;
    public static int ShotsPresented {get;private set;}
    public void Configure(Sprite[] poses,float seconds)
    {renderer2d=GetComponent<SpriteRenderer>();frames=poses;frameSeconds=Mathf.Max(.01f,seconds);}
    public void Shoot(){shotAge=0;ShotsPresented++;if(renderer2d!=null && frames!=null && frames.Length>=3)renderer2d.sprite=frames[1];}
    private void Update()
    {
        shotAge+=Time.deltaTime;if(renderer2d==null||frames==null||frames.Length<3)return;
        renderer2d.sprite=frames[shotAge<frameSeconds?1:shotAge<frameSeconds*2?2:0];
    }
}

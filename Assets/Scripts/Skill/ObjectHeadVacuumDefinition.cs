using UnityEngine;

/// <summary>Editable airflow/flight tuning, shared by player, AI and authority worker.</summary>
[CreateAssetMenu(menuName="Object Head/Vacuum Tuning")]
public sealed class ObjectHeadVacuumDefinition : ScriptableObject
{
    public Vector2 range = new Vector2(3, 9);
    public Vector2 impulse = new Vector2(5, 17);
    [Min(.1f)] public float beamWidth = 1.6f;
    [Min(0)] public int maximumLoot = 3;
    [Min(.1f)] public float lootPullSeconds = 1.2f;
    [Min(.1f)] public float lootSpeed = 9;
    public Vector2 hoverHeight = new Vector2(.7f, 3.2f);
    [Min(.1f)] public float hoverSeconds = 6;
    [Min(0)] public float extraMovementSeconds = 5;
    [Min(.1f)] public float verticalSpeed = 4;
    [Min(.1f)] public float groundProbe = 12;
    [Min(.1f)] public float effectSeconds = .45f;
    [Range(1,32)] public int windStreaks = 12;
    [Min(.01f)] public float windWidth = .045f;
    public Color windColor = new Color(.8f,1,1,.8f);
    public void ApplySheet()
    {
        var b=ObjectHeadBalanceTable.Load();if(b==null)return;
        range=new Vector2(Mathf.Max(.1f,b.GetFloat("vacuum.range_min",range.x)),Mathf.Max(.1f,b.GetFloat("vacuum.range_max",range.y)));
        impulse=new Vector2(Mathf.Max(0,b.GetFloat("vacuum.force_min",impulse.x)),Mathf.Max(0,b.GetFloat("vacuum.force_max",impulse.y)));
        beamWidth=Mathf.Max(.1f,b.GetFloat("vacuum.beam_width",beamWidth));maximumLoot=Mathf.Max(0,b.GetInt("vacuum.maximum_loot",maximumLoot));
        lootPullSeconds=Mathf.Max(.1f,b.GetFloat("vacuum.loot_seconds",lootPullSeconds));lootSpeed=Mathf.Max(.1f,b.GetFloat("vacuum.loot_speed",lootSpeed));
        hoverHeight=new Vector2(Mathf.Max(.1f,b.GetFloat("vacuum.hover_min",hoverHeight.x)),Mathf.Max(.1f,b.GetFloat("vacuum.hover_max",hoverHeight.y)));
        hoverSeconds=Mathf.Max(.1f,b.GetFloat("vacuum.hover_seconds",hoverSeconds));extraMovementSeconds=Mathf.Max(0,b.GetFloat("vacuum.extra_movement",extraMovementSeconds));
    }
    public float Range(float charge) => Mathf.Lerp(range.x,range.y,Mathf.Clamp01(charge));
    public float Force(float charge) => Mathf.Lerp(impulse.x,impulse.y,Mathf.Clamp01(charge));
}

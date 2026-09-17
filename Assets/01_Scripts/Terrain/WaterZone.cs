using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class WaterZone : MonoBehaviour
{
    [SerializeField] private UnityEvent<GameObject> entered = new UnityEvent<GameObject>();

    public UnityEvent<GameObject> Entered
    {
        get { return entered; }
    }

    public void ConfigureSurface(float surfaceY, float width, float depth, float topInset)
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            return;
        }

        float safeDepth = Mathf.Max(0.1f, depth);
        float safeInset = Mathf.Max(0f, topInset);
        box.isTrigger = true;
        box.size = new Vector2(Mathf.Max(0.1f, width), safeDepth);
        transform.position = new Vector3(
            transform.position.x,
            surfaceY - safeInset - safeDepth * 0.5f,
            transform.position.z);
    }

    private void Reset()
    {
        Collider2D collider = GetComponent<Collider2D>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CharacterCombat enteringCharacter=other.GetComponentInParent<CharacterCombat>();
        if(enteringCharacter!=null && !enteringCharacter.IsDead)
            ObjectHeadMicroParticles.Emit(ObjectHeadMicroCue.Water,new Vector2(enteringCharacter.transform.position.x,GetComponent<Collider2D>().bounds.max.y),Vector2.up,1.4f);
        entered.Invoke(other.gameObject);
        CharacterCombat combat = other.GetComponentInParent<CharacterCombat>();
        if (combat != null)
        {
            combat.Die();
        }

        Debug.Log(other.name + " entered WaterZone.");
    }
}

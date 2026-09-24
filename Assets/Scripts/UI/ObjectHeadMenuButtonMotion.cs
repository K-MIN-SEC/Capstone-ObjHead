using UnityEngine;
using UnityEngine.EventSystems;

// Small, unscaled hover feedback. Designers can tune it on each prefab button.
public sealed class ObjectHeadMenuButtonMotion : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField, Range(1f, 1.1f)] private float hoverScale = 1.025f;
    [SerializeField, Range(.9f, 1f)] private float pressedScale = .975f;
    [SerializeField, Range(1f, 35f)] private float response = 18f;
    private Vector3 restScale;
    private bool hovered;
    private bool pressed;

    private void Awake() => restScale = transform.localScale;

    private void Update()
    {
        float factor = pressed ? pressedScale : hovered ? hoverScale : 1f;
        float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, restScale * factor, blend);
    }

    private void OnDisable()
    {
        hovered = pressed = false;
        if (restScale != Vector3.zero) transform.localScale = restScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => hovered = true;
    public void OnDeselect(BaseEventData eventData) => hovered = false;
}

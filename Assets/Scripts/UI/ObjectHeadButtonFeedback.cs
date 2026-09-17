using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small, unscaled-time feedback. Keeps authored layout and does not intercept clicks.</summary>
[RequireComponent(typeof(Button))]
public sealed class ObjectHeadButtonFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler,ISubmitHandler
{
    [Range(1,1.1f)] public float hoverScale=1.025f;
    [Range(.9f,1)] public float pressedScale=.965f;
    [Min(1)] public float response=22;
    [Min(.01f)] public float submitSeconds=.1f;
    private Button button;
    private Vector3 authoredScale;
    private bool hover,selected,pressed;
    private float submitUntil;
    private void Awake(){button=GetComponent<Button>();authoredScale=transform.localScale;}
    private void Update()
    {
        bool usable=button.IsInteractable();
        float scale=usable?(pressed||Time.unscaledTime<submitUntil?pressedScale:hover||selected?hoverScale:1):1;
        transform.localScale=Vector3.Lerp(transform.localScale,authoredScale*scale,1-Mathf.Exp(-response*Time.unscaledDeltaTime));
    }
    private void OnDisable(){transform.localScale=authoredScale;hover=selected=pressed=false;submitUntil=0;}
    public void OnPointerEnter(PointerEventData e)=>hover=true;
    public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;}
    public void OnPointerUp(PointerEventData e)=>pressed=false;
    public void OnSelect(BaseEventData e)=>selected=true;
    public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
    public void OnSubmit(BaseEventData e)=>submitUntil=Time.unscaledTime+submitSeconds;
    private void OnApplicationFocus(bool focus){if(!focus){hover=selected=pressed=false;submitUntil=0;}}
}

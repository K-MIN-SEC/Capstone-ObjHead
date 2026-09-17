using UnityEngine;

[CreateAssetMenu(menuName="Object Head/Combat Camera")]
public sealed class ObjectHeadCameraTuning : ScriptableObject
{
    public bool automaticFraming=true;
    [Min(1)] public float characterViewSize=5.5f;
    [Min(1)] public float flightViewSize=7.5f;
    [Min(1)] public float maximumFlightViewSize=10f;
    [Min(.1f)] public float zoomResponse=3.5f;
    [Min(0)] public float lookAheadSeconds=.16f;
    [Min(0)] public float maximumLookAhead=2.5f;
    [Min(0)] public float impactHoldSeconds=.7f;
    [Min(0)] public float settlementHoldSeconds=1.6f;
    [Min(0)] public float oceanViewPadding=5f;
    [Range(0,.4f)] public float bottomHudSafeFraction=.18f;
    [Range(0,1)] public float shakeStrength=.65f;
    [Min(.01f)] public float shakeSeconds=.2f;
    private static ObjectHeadCameraTuning loaded;
    public static ObjectHeadCameraTuning Load()=>loaded!=null?loaded:loaded=Resources.Load<ObjectHeadCameraTuning>("ObjectHeadCameraTuning");
}

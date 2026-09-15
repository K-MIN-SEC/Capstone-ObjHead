using System;
using UnityEngine;

/// <summary>
/// Kept only so older scenes do not lose a serialized script reference.
/// Title UI now lives directly in ObjectHeadTitle.unity and is never instantiated here.
/// </summary>
[Obsolete("Title UI is authored directly in ObjectHeadTitle.unity.")]
public sealed class ObjectHeadTitleBootstrap : MonoBehaviour
{
}

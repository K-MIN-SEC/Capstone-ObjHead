using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class ObjectHeadTitleBootstrap : MonoBehaviour
{
    [SerializeField] private string titlePrefabResourcesPath = "UI/ObjectHeadTitleRoot";

    private void Awake()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        if (FindAnyObjectByType<ObjectHeadTitleScreen>() != null)
#else
        if (FindObjectOfType<ObjectHeadTitleScreen>() != null)
#endif
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(titlePrefabResourcesPath);
        if (prefab == null)
        {
            Debug.LogError("[Object Head] Title UI prefab was not found: " + titlePrefabResourcesPath);
            return;
        }

        Instantiate(prefab);
    }
}

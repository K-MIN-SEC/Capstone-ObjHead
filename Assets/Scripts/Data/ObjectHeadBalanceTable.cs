using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadBalanceEntry
{
    public string key;
    public float value;
    public string unit;
    public string description;
}

[CreateAssetMenu(fileName = "ObjectHeadBalance", menuName = "Object Head/Balance Table")]
public sealed class ObjectHeadBalanceTable : ScriptableObject
{
    private const string ResourcesPath = "ObjectHeadBalance";

    [SerializeField] private ObjectHeadBalanceEntry[] entries = Array.Empty<ObjectHeadBalanceEntry>();
    private Dictionary<string, float> valuesByKey;

    public ObjectHeadBalanceEntry[] Entries => entries;

    public float GetFloat(string key, float fallback)
    {
        EnsureCache();
        return !string.IsNullOrWhiteSpace(key) && valuesByKey.TryGetValue(key, out float value)
            ? value
            : fallback;
    }

    public int GetInt(string key, int fallback)
    {
        return Mathf.RoundToInt(GetFloat(key, fallback));
    }

    public static ObjectHeadBalanceTable Load()
    {
        return Resources.Load<ObjectHeadBalanceTable>(ResourcesPath);
    }

    private void OnEnable()
    {
        valuesByKey = null;
    }

    private void EnsureCache()
    {
        if (valuesByKey != null)
        {
            return;
        }

        valuesByKey = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (ObjectHeadBalanceEntry entry in entries)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
            {
                valuesByKey[entry.key] = entry.value;
            }
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(ObjectHeadBalanceEntry[] values)
    {
        entries = values ?? Array.Empty<ObjectHeadBalanceEntry>();
        valuesByKey = null;
    }
#endif
}

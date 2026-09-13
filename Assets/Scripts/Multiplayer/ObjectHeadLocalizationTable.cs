using System;
using System.Linq;
using UnityEngine;

public enum ObjectHeadLanguage
{
    Korean = 0,
    English = 1
}

[Serializable]
public sealed class ObjectHeadLocalizedEntry
{
    public string key;
    [TextArea] public string korean;
    [TextArea] public string english;
}

[CreateAssetMenu(fileName = "ObjectHeadLocalization", menuName = "Object Head/Localization Table")]
public sealed class ObjectHeadLocalizationTable : ScriptableObject
{
    private const string ResourcesPath = "ObjectHeadLocalization";

    [SerializeField] private ObjectHeadLanguage defaultLanguage = ObjectHeadLanguage.Korean;
    [SerializeField] private ObjectHeadLocalizedEntry[] entries = Array.Empty<ObjectHeadLocalizedEntry>();

    public ObjectHeadLanguage DefaultLanguage => defaultLanguage;
    public ObjectHeadLocalizedEntry[] Entries => entries;

    public string Get(string key, ObjectHeadLanguage language)
    {
        ObjectHeadLocalizedEntry entry = entries.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.key, key, StringComparison.Ordinal));
        if (entry == null)
        {
            return key;
        }

        string value = language == ObjectHeadLanguage.Korean ? entry.korean : entry.english;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return !string.IsNullOrWhiteSpace(entry.korean) ? entry.korean : key;
    }

    public static ObjectHeadLocalizationTable Load()
    {
        return Resources.Load<ObjectHeadLocalizationTable>(ResourcesPath);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(ObjectHeadLanguage language, ObjectHeadLocalizedEntry[] values)
    {
        defaultLanguage = language;
        entries = values ?? Array.Empty<ObjectHeadLocalizedEntry>();
    }
#endif
}

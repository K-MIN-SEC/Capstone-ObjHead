using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class ObjectHeadLocalizedLabel : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    public string LocalizationKey
    {
        get => localizationKey;
        set => localizationKey = value;
    }

    public void Apply(ObjectHeadLocalizationTable table, ObjectHeadLanguage language)
    {
        if (table != null && TryGetComponent(out Text target))
        {
            target.text = table.Get(localizationKey, language);
        }
    }
}

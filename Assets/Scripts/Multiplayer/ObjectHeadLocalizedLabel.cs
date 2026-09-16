using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
[ExecuteAlways]
public sealed class ObjectHeadLocalizedLabel : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    public string LocalizationKey
    {
        get => localizationKey;
        set => localizationKey = value;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) Preview();
    }

    public void Preview()
    {
        ObjectHeadContent content = ObjectHeadContent.Load();
        if (TryGetComponent(out Text text) && text.font == null && content != null) text.font = content.uiFont;
        ObjectHeadLocalizationTable table = ObjectHeadLocalizationTable.Load();
        if (table != null) Apply(table, table.DefaultLanguage);
    }

    public void Apply(ObjectHeadLocalizationTable table, ObjectHeadLanguage language)
    {
        if (table != null && TryGetComponent(out Text target))
        {
            target.text = table.Get(localizationKey, language);
        }
    }
}

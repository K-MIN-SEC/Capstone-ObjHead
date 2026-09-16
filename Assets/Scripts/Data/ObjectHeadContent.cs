using System;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadCharacterDefinition
{
    public ObjectHeadCharacterKind kind;
    public string nameKey;
    public string descriptionKey;
    public Sprite portrait;
    public GameObject prefab;
    public Color accent = Color.white;
}

[Serializable]
public sealed class ObjectHeadTeamRule
{
    public int players;
    public int charactersPerPlayer;
}

[Serializable]
public sealed class ObjectHeadMapDefinition
{
    public string id;
    public string nameKey;
    public string descriptionKey;
    public string sceneName;
    public Sprite preview;
}

[CreateAssetMenu(menuName = "Object Head/Content Catalog")]
public sealed class ObjectHeadContent : ScriptableObject
{
    public Font uiFont;
    public Color[] allianceColors;
    public ObjectHeadModeDefinition[] modes = Array.Empty<ObjectHeadModeDefinition>();
    public ObjectHeadModeDefinition Mode(ObjectHeadMatchMode mode) => modes.FirstOrDefault(m => m.mode == mode);
    public ObjectHeadCharacterDefinition[] characters = Array.Empty<ObjectHeadCharacterDefinition>();
    public ObjectHeadTeamRule[] teamRules = Array.Empty<ObjectHeadTeamRule>();
    public ObjectHeadMapDefinition[] maps = Array.Empty<ObjectHeadMapDefinition>();
    public int CharactersPerPlayer(int players) => teamRules.FirstOrDefault(r => r.players == players)?.charactersPerPlayer ?? 0;
    public ObjectHeadCharacterDefinition Character(ObjectHeadCharacterKind kind) => characters.FirstOrDefault(c => c.kind == kind);
    public bool ValidSelection(ObjectHeadCharacterKind[] selection, int players) =>
        selection != null && selection.Length == CharactersPerPlayer(players) && selection.Length > 0 &&
        selection.All(kind => Character(kind) != null);
    public ObjectHeadCharacterKind[] DefaultSelection(int players) =>
        Enumerable.Range(0, CharactersPerPlayer(players)).Select(i => characters[i % characters.Length].kind).ToArray();
    public static ObjectHeadContent Load() => Resources.Load<ObjectHeadContent>("ObjectHeadContent");
}

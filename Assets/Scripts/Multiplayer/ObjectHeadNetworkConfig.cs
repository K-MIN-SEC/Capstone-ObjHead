using System;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class ObjectHeadServerProfile
{
    public string id = "local";
    public string label = "Local Development";
    public string scheme = "http";
    public string host = "127.0.0.1";
    public int port = 7350;
    public string serverKey = "defaultkey";

    public ObjectHeadServerProfile Copy()
    {
        return (ObjectHeadServerProfile)MemberwiseClone();
    }
}

[Serializable]
public sealed class ObjectHeadMapSceneBinding
{
    public string mapId = "object_head_demo_01";
    public string sceneName = "SampleScene";
}

[CreateAssetMenu(fileName = "ObjectHeadNetworkConfig", menuName = "Object Head/Network Config")]
public sealed class ObjectHeadNetworkConfig : ScriptableObject
{
    private const string ResourcesPath = "ObjectHeadNetworkConfig";

    [Header("Server profiles")]
    [SerializeField] private bool useDedicatedAuthority;
    public bool UseDedicatedAuthority => useDedicatedAuthority;
    [SerializeField] private string defaultProfileId = "local";
    [SerializeField] private ObjectHeadServerProfile[] profiles =
    {
        new ObjectHeadServerProfile()
    };

    [Header("Room defaults")]
    [SerializeField] private ObjectHeadRoomSettings defaultRoomSettings = new ObjectHeadRoomSettings();
    [SerializeField] private string fallbackMapId = "object_head_demo_01";

    [Header("Scene routing")]
    [SerializeField] private string titleSceneName = "ObjectHeadTitle";
    [SerializeField] private ObjectHeadMapSceneBinding[] mapScenes =
    {
        new ObjectHeadMapSceneBinding()
    };

    [Header("Temporary demo panel")]
    [SerializeField, Min(0f)] private float panelMargin = 12f;
    [SerializeField, Min(240f)] private float panelWidth = 430f;
    [SerializeField, Min(240f)] private float panelMaxHeight = 720f;

    public string DefaultProfileId => defaultProfileId;
    public ObjectHeadRoomSettings DefaultRoomSettings => defaultRoomSettings != null
        ? defaultRoomSettings.Copy()
        : new ObjectHeadRoomSettings();
    public string FallbackMapId => string.IsNullOrWhiteSpace(fallbackMapId)
        ? "object_head_demo_01"
        : fallbackMapId.Trim();
    public string TitleSceneName => string.IsNullOrWhiteSpace(titleSceneName)
        ? "ObjectHeadTitle"
        : titleSceneName.Trim();
    public float PanelMargin => panelMargin;
    public float PanelWidth => panelWidth;
    public float PanelMaxHeight => panelMaxHeight;

    public ObjectHeadServerProfile GetProfile(string profileId = null)
    {
        ObjectHeadServerProfile[] available = profiles ?? Array.Empty<ObjectHeadServerProfile>();
        string requested = string.IsNullOrWhiteSpace(profileId) ? defaultProfileId : profileId;
        ObjectHeadServerProfile profile = available.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.id, requested, StringComparison.OrdinalIgnoreCase));
        profile ??= available.FirstOrDefault(candidate => candidate != null);
        return profile != null ? profile.Copy() : new ObjectHeadServerProfile();
    }

    public string ResolveSceneName(string mapId)
    {
        string requested = string.IsNullOrWhiteSpace(mapId) ? FallbackMapId : mapId.Trim();
        ObjectHeadMapSceneBinding binding = (mapScenes ?? Array.Empty<ObjectHeadMapSceneBinding>())
            .FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.mapId, requested, StringComparison.OrdinalIgnoreCase));
        if (binding != null && !string.IsNullOrWhiteSpace(binding.sceneName))
        {
            return binding.sceneName.Trim();
        }

        binding = (mapScenes ?? Array.Empty<ObjectHeadMapSceneBinding>())
            .FirstOrDefault(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.sceneName));
        return binding != null ? binding.sceneName.Trim() : "SampleScene";
    }

    public static ObjectHeadNetworkConfig LoadOrCreateRuntimeDefault()
    {
        ObjectHeadNetworkConfig loaded = Resources.Load<ObjectHeadNetworkConfig>(ResourcesPath);
        return loaded != null ? loaded : CreateInstance<ObjectHeadNetworkConfig>();
    }
}

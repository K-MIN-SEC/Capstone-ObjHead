public static class ObjectHeadCommonAuthority
{
    public static bool Online => GameStartData.Instance != null && !GameStartData.Instance.localMatch;
    public static bool CanWrite => !Online || (ObjectHeadNetworkManager.Instance != null && ObjectHeadNetworkManager.Instance.IsCombatAuthority);
    public static bool IsDedicatedMatch => Online && GameStartData.Instance.dedicatedAuthority;
    public static bool CanEditTerrain => !IsDedicatedMatch || CanWrite;
}

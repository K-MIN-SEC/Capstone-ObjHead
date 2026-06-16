using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ObjectHeadCameraController : MonoBehaviour
{
    [SerializeField] private TerrainManager terrain;
    [SerializeField] private TurnManager turnManager;
    [SerializeField, Min(0.1f)] private float followLerp = 8f;
    [SerializeField, Min(0.1f)] private float manualPanSpeed = 9f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 7f;
    [SerializeField, Min(1f)] private float minSize = 4f;
    [SerializeField, Min(1f)] private float defaultPlaySize = 7.5f;
    [SerializeField, Min(1f)] private float maxSize = 18f;
    [SerializeField] private Vector2 paddingWorld = new Vector2(0.4f, 0.4f);

    private Camera targetCamera;
    private Vector3 manualOffset;
    private Vector2 lastActionFocusPosition;
    private bool overviewMode;
    private bool manualControl;
    private bool forceCharacterFocus;
    private bool hasLastActionFocusPosition;
    private int observedTurnSerial = -1;
    private SkillProjectile lastSeenProjectile;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.orthographic = true;
    }

    private void Start()
    {
        if (terrain == null) terrain = FindAny<TerrainManager>();
        if (turnManager == null) turnManager = FindAny<TurnManager>();
        FocusOnCurrentTarget(true);
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        ObserveProjectileStart();
        ObserveTurnChange();
        UpdateActionFocusFromProjectile();
        ReadManualInput();
        if (overviewMode || manualControl)
        {
            transform.position = ClampToTerrain(transform.position);
            return;
        }

        if (TryGetFollowPosition(out Vector2 targetPosition))
        {
            Vector3 desired = new Vector3(targetPosition.x, targetPosition.y, transform.position.z) + manualOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                ClampToTerrain(desired),
                Time.unscaledDeltaTime * followLerp);
        }
        else
        {
            transform.position = ClampToTerrain(transform.position);
        }
    }

    public void Configure(TerrainManager terrainManager, TurnManager manager)
    {
        terrain = terrainManager;
        turnManager = manager;
        FocusOnCurrentTarget(true);
    }

    public bool IsOutsideReachableView(Vector2 worldPosition, float marginWorld = 0f)
    {
        if (!TryGetReachableViewBounds(out float minX, out float maxX, out float minY, out float maxY))
        {
            return false;
        }

        float margin = Mathf.Max(0f, marginWorld);
        return worldPosition.x < minX - margin ||
               worldPosition.x > maxX + margin ||
               worldPosition.y < minY - margin ||
               worldPosition.y > maxY + margin;
    }

    public void FocusOnCurrentCharacterImmediate(bool resetZoom = false)
    {
        hasLastActionFocusPosition = false;
        lastSeenProjectile = null;
        FocusOnCurrentTarget(resetZoom);
    }

    public void FitToTerrainOverview()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (terrain == null || terrain.WidthPx <= 0 || terrain.HeightPx <= 0)
        {
            return;
        }

        float widthWorld = terrain.WidthPx / (float)terrain.PixelsPerUnit;
        float heightWorld = terrain.HeightPx / (float)terrain.PixelsPerUnit;
        Vector2 center = terrain.TerrainOriginWorld + new Vector2(widthWorld * 0.5f, heightWorld * 0.5f);
        float sizeByHeight = heightWorld * 0.5f + paddingWorld.y;
        float sizeByWidth = widthWorld / (2f * Mathf.Max(0.01f, targetCamera.aspect)) + paddingWorld.x;
        targetCamera.orthographicSize = Mathf.Max(minSize, Mathf.Max(sizeByHeight, sizeByWidth));
        manualOffset = Vector3.zero;
        overviewMode = true;
        manualControl = false;
        forceCharacterFocus = false;
        transform.position = ClampToTerrain(new Vector3(center.x, center.y, transform.position.z));
    }

    private void FocusOnCurrentTarget(bool resetZoom)
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (resetZoom)
        {
            targetCamera.orthographicSize = Mathf.Clamp(defaultPlaySize, minSize, maxSize);
        }

        overviewMode = false;
        manualControl = false;
        forceCharacterFocus = true;
        manualOffset = Vector3.zero;
        if (turnManager != null && turnManager.CurrentCharacter != null)
        {
            Vector3 targetPosition = turnManager.CurrentCharacter.transform.position;
            transform.position = ClampToTerrain(new Vector3(targetPosition.x, targetPosition.y, transform.position.z));
        }
        else
        {
            FitToTerrainOverview();
        }
    }

    private void ReadManualInput()
    {
        Vector2 pan = Vector2.zero;
        float zoomDelta = 0f;
        bool focusCharacter = false;
        bool overview = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.oKey.isPressed) pan.y += 1f;
            if (keyboard.kKey.isPressed) pan.x -= 1f;
            if (keyboard.lKey.isPressed) pan.y -= 1f;
            if (keyboard.semicolonKey.isPressed) pan.x += 1f;
            if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) zoomDelta += 1f;
            if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) zoomDelta -= 1f;
            focusCharacter = keyboard.iKey.wasPressedThisFrame;
            overview = keyboard.pKey.wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            zoomDelta -= mouse.scroll.ReadValue().y * 0.035f;
            if (mouse.middleButton.isPressed || mouse.rightButton.isPressed)
            {
                pan -= mouse.delta.ReadValue() * 0.018f;
            }
        }

        pan += ObjectHeadGamepadInput.CameraPan();
#else
        if (Input.GetKey(KeyCode.O)) pan.y += 1f;
        if (Input.GetKey(KeyCode.K)) pan.x -= 1f;
        if (Input.GetKey(KeyCode.L)) pan.y -= 1f;
        if (Input.GetKey(KeyCode.Semicolon)) pan.x += 1f;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) zoomDelta += 1f;
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) zoomDelta -= 1f;
        zoomDelta -= Input.mouseScrollDelta.y * 0.25f;
        if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            pan.x -= Input.GetAxisRaw("Mouse X") * 12f;
            pan.y -= Input.GetAxisRaw("Mouse Y") * 12f;
        }
        focusCharacter = Input.GetKeyDown(KeyCode.I);
        overview = Input.GetKeyDown(KeyCode.P);
#endif

        if (overview)
        {
            ToggleTerrainOverview();
            return;
        }

        if (focusCharacter)
        {
            FocusOnCurrentTarget(false);
            return;
        }

        if (pan.sqrMagnitude > 0f)
        {
            overviewMode = false;
            manualControl = true;
            forceCharacterFocus = false;
            Vector2 panStep = pan.sqrMagnitude > 1f ? pan.normalized : pan;
            transform.position = ClampToTerrain(
                transform.position + (Vector3)(panStep * manualPanSpeed * Time.unscaledDeltaTime));
        }

        if (Mathf.Abs(zoomDelta) > 0f)
        {
            overviewMode = false;
            manualControl = true;
            forceCharacterFocus = false;
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize + zoomDelta * zoomSpeed * Time.unscaledDeltaTime,
                minSize,
                maxSize);
            transform.position = ClampToTerrain(transform.position);
        }
    }

    private void ToggleTerrainOverview()
    {
        if (overviewMode)
        {
            FocusOnCurrentTarget(true);
            return;
        }

        FitToTerrainOverview();
    }

    private bool TryGetFollowPosition(out Vector2 targetPosition)
    {
        SkillProjectile projectile = FindAny<SkillProjectile>();
        if (!forceCharacterFocus && projectile != null && projectile.IsFlying)
        {
            targetPosition = projectile.transform.position;
            return true;
        }

        if (!forceCharacterFocus && ShouldPreferActionFocus())
        {
            if (projectile != null)
            {
                targetPosition = projectile.transform.position;
                return true;
            }

            if (hasLastActionFocusPosition)
            {
                targetPosition = lastActionFocusPosition;
                return true;
            }
        }

        if (turnManager != null && turnManager.CurrentCharacter != null)
        {
            targetPosition = turnManager.CurrentCharacter.transform.position;
            return true;
        }

        targetPosition = default;
        return false;
    }

    private void ObserveProjectileStart()
    {
        SkillProjectile projectile = FindAny<SkillProjectile>();
        if (projectile == null || projectile == lastSeenProjectile)
        {
            return;
        }

        lastSeenProjectile = projectile;
        forceCharacterFocus = false;
        manualControl = false;
        overviewMode = false;
        manualOffset = Vector3.zero;
        RememberActionFocus(projectile.transform.position);
    }

    private void ObserveTurnChange()
    {
        if (turnManager == null)
        {
            turnManager = FindAny<TurnManager>();
        }

        if (turnManager == null || observedTurnSerial == turnManager.TurnSerial)
        {
            return;
        }

        observedTurnSerial = turnManager.TurnSerial;
        hasLastActionFocusPosition = false;
        lastSeenProjectile = null;
    }

    private void UpdateActionFocusFromProjectile()
    {
        SkillProjectile projectile = FindAny<SkillProjectile>();
        if (projectile != null && !forceCharacterFocus)
        {
            RememberActionFocus(projectile.transform.position);
        }
    }

    private void RememberActionFocus(Vector2 position)
    {
        lastActionFocusPosition = position;
        hasLastActionFocusPosition = true;
    }

    private bool ShouldPreferActionFocus()
    {
        return turnManager != null &&
               hasLastActionFocusPosition &&
               (turnManager.IsResidualTimeActive ||
                turnManager.CurrentPhase == TurnPhase.ProjectileFlying ||
                turnManager.CurrentPhase == TurnPhase.PostImpactDelay ||
                turnManager.CurrentPhase == TurnPhase.Resolving);
    }

    private Vector3 ClampToTerrain(Vector3 position)
    {
        if (!TryGetCameraCenterBounds(out float minX, out float maxX, out float minY, out float maxY))
        {
            return position;
        }

        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (minX + maxX) * 0.5f;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (minY + maxY) * 0.5f;
        return position;
    }

    private bool TryGetReachableViewBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        if (!TryGetCameraCenterBounds(out float centerMinX, out float centerMaxX, out float centerMinY, out float centerMaxY))
        {
            minX = maxX = minY = maxY = 0f;
            return false;
        }

        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        minX = Mathf.Min(centerMinX, centerMaxX) - halfWidth;
        maxX = Mathf.Max(centerMinX, centerMaxX) + halfWidth;
        minY = Mathf.Min(centerMinY, centerMaxY) - halfHeight;
        maxY = Mathf.Max(centerMinY, centerMaxY) + halfHeight;
        return true;
    }

    private bool TryGetCameraCenterBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        if (terrain == null || targetCamera == null || terrain.WidthPx <= 0 || terrain.HeightPx <= 0)
        {
            minX = maxX = minY = maxY = 0f;
            return false;
        }

        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        float terrainWidth = terrain.WidthPx / (float)terrain.PixelsPerUnit;
        float terrainHeight = terrain.HeightPx / (float)terrain.PixelsPerUnit;
        minX = terrain.TerrainOriginWorld.x + halfWidth - paddingWorld.x;
        maxX = terrain.TerrainOriginWorld.x + terrainWidth - halfWidth + paddingWorld.x;
        minY = terrain.TerrainOriginWorld.y + halfHeight - paddingWorld.y;
        maxY = terrain.TerrainOriginWorld.y + terrainHeight - halfHeight + paddingWorld.y;

        if (minX > maxX)
        {
            float centerX = terrain.TerrainOriginWorld.x + terrainWidth * 0.5f;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = terrain.TerrainOriginWorld.y + terrainHeight * 0.5f;
            minY = centerY;
            maxY = centerY;
        }

        return true;
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}

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
    [Header("Free camera (no drag required)")]
    [SerializeField] private bool edgePanEnabled = true;
    [SerializeField, Range(.005f,.1f)] private float edgePanScreenFraction = .025f;
    [SerializeField, Min(.1f)] private float edgePanViewHeightsPerSecond = .8f;
    [SerializeField] private bool middleMouseDragEnabled = true;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 7f;
    [Header("Mouse wheel (independent of frame rate)")]
    [Tooltip("Fraction of the current view height changed by one wheel notch. 0.16 = 16%.")]
    [SerializeField, Range(0.01f, 0.5f)] private float wheelZoomFraction = 0.16f;
    [Tooltip("Used ONLY when Input Settings keeps the platform-specific range. Unity's default normalized scroll already reports 1 per notch.")]
    [SerializeField, Min(1f)] private float wheelUnitsPerNotch = 120f;
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
    private SkillProjectile frameProjectile;
    private ObjectHeadBattleScreen battleScreen;
    private ObjectHeadCameraTuning cinematic;
    private Vector3 shakeOffset;
    private float shakeUntil,shakeAmplitude,impactUntil;
    private Vector2 confirmedImpact;
    private float preferredViewSize;
    private float settlementUntil;
    private Bounds settlementBounds;

    private void OnEnable(){ObjectHeadPresentation.ImpactPresented+=OnImpact;CharacterCombat.DamagePresented+=OnDamagePresented;}
    private void OnDisable()
    {
        ObjectHeadPresentation.ImpactPresented-=OnImpact;
        CharacterCombat.DamagePresented-=OnDamagePresented;
        transform.position-=shakeOffset;shakeOffset=Vector3.zero;
    }
    private void OnImpact(Vector2 point,float radius,float impulse)
    {
        if(cinematic==null || manualControl || overviewMode)return;
        confirmedImpact=point;impactUntil=Time.unscaledTime+cinematic.impactHoldSeconds;
        shakeAmplitude=Mathf.Min(.22f,impulse)*cinematic.shakeStrength;
        shakeUntil=Time.unscaledTime+cinematic.shakeSeconds;
    }
    private void OnDamagePresented(Vector2 point,float popupDuration)
    {
        if(cinematic==null)return;
        if(Time.unscaledTime>=settlementUntil)settlementBounds=new Bounds(point,Vector3.zero);
        else settlementBounds.Encapsulate(point);
        settlementUntil=Time.unscaledTime+Mathf.Max(popupDuration,cinematic.settlementHoldSeconds);
        forceCharacterFocus=false;
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.orthographic = true;
    }

    private void Start()
    {
        if (terrain == null) terrain = FindAny<TerrainManager>();
        if (turnManager == null) turnManager = FindAny<TurnManager>();
        battleScreen = FindAny<ObjectHeadBattleScreen>();
        cinematic=ObjectHeadCameraTuning.Load();
        if(cinematic!=null)defaultPlaySize=cinematic.characterViewSize;
        preferredViewSize=defaultPlaySize;
        FocusOnCurrentTarget(true);
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        transform.position-=shakeOffset;shakeOffset=Vector3.zero;
        frameProjectile=FindAny<SkillProjectile>();
        ObserveProjectileStart();
        ObserveTurnChange();
        UpdateActionFocusFromProjectile();
        ReadManualInput();
        if (overviewMode || manualControl)
        {
            transform.position = ClampToTerrain(transform.position);
            return;
        }

        if(cinematic!=null && cinematic.automaticFraming)
        {
            float desiredSize=preferredViewSize;
            if(Time.unscaledTime<settlementUntil)
                desiredSize=Mathf.Max(desiredSize,settlementBounds.extents.y+2,(settlementBounds.extents.x+2)/targetCamera.aspect);
            if(frameProjectile!=null && frameProjectile.IsFlying)
            {
                float speed=frameProjectile.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
                desiredSize=Mathf.Max(preferredViewSize,Mathf.Clamp(cinematic.flightViewSize+speed*cinematic.lookAheadSeconds*.35f,
                    cinematic.flightViewSize,cinematic.maximumFlightViewSize));
            }
            targetCamera.orthographicSize=Mathf.Lerp(targetCamera.orthographicSize,Mathf.Clamp(desiredSize,minSize,maxSize),
                1f-Mathf.Exp(-cinematic.zoomResponse*Time.unscaledDeltaTime));
        }

        if (TryGetFollowPosition(out Vector2 targetPosition))
        {
            float hudOffset=cinematic!=null?targetCamera.orthographicSize*cinematic.bottomHudSafeFraction:0;
            Vector3 desired = new Vector3(targetPosition.x, targetPosition.y-hudOffset, transform.position.z) + manualOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                ClampToTerrain(desired),
                Time.unscaledDeltaTime * followLerp);
        }
        else
        {
            transform.position = ClampToTerrain(transform.position);
        }
        if(cinematic!=null && Time.unscaledTime<shakeUntil && Time.timeScale>0)
        {
            float fade=(shakeUntil-Time.unscaledTime)/Mathf.Max(.01f,cinematic.shakeSeconds);
            shakeOffset=new Vector3(Mathf.Sin(Time.unscaledTime*73),Mathf.Sin(Time.unscaledTime*91),0)*shakeAmplitude*fade;
            transform.position+=shakeOffset;
        }
    }

    public void Configure(TerrainManager terrainManager, TurnManager manager)
    {
        terrain = terrainManager;
        turnManager = manager;
        FocusOnCurrentTarget(true);
    }

    public float MaximumVisibleSkyY()
    {
        if(terrain==null || targetCamera==null)return transform.position.y+maxSize;
        var bounds=terrain.GetTerrainBounds();
        float overview=Mathf.Max(bounds.extents.y+paddingWorld.y,bounds.size.x/(2*Mathf.Max(.1f,targetCamera.aspect))+paddingWorld.x);
        float size=Mathf.Max(maxSize,overview);
        return Mathf.Max(bounds.max.y+paddingWorld.y,bounds.center.y+size);
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
            preferredViewSize=targetCamera.orthographicSize;
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
        if (!Application.isFocused || Time.timeScale <= 0f || battleScreen?.BlocksCameraInput == true) return;
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events?.currentSelectedGameObject?.GetComponent<UnityEngine.UI.InputField>()?.isFocused == true) return;
        Vector2 pan = Vector2.zero;
        Vector2 mousePosition = new Vector2(-1,-1);
        Vector2 dragPixels = Vector2.zero;
        float zoomDelta = 0f;
        float wheelNotches = 0f;
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
            focusCharacter = keyboard.homeKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame;
            overview = keyboard.pKey.wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            wheelNotches = NormalizeWheelNotches(mouse.scroll.ReadValue().y,
                InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms,
                wheelUnitsPerNotch);
            mousePosition = mouse.position.ReadValue();
            if (middleMouseDragEnabled && mouse.middleButton.isPressed)
            {
                dragPixels = -mouse.delta.ReadValue();
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
        wheelNotches = Input.mouseScrollDelta.y;
        mousePosition = Input.mousePosition;
        if (middleMouseDragEnabled && Input.GetMouseButton(2))
        {
            dragPixels = new Vector2(-Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y")) * 12f;
        }
        focusCharacter = Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.I);
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

        bool pointerOverUi = events != null && events.IsPointerOverGameObject();
        Vector2 edge = edgePanEnabled && !pointerOverUi
            ? EdgePanDirection(mousePosition, new Vector2(Screen.width,Screen.height),edgePanScreenFraction) : Vector2.zero;
        Vector2 step = Vector2.ClampMagnitude(pan,1) * manualPanSpeed * Time.unscaledDeltaTime
            + edge * (targetCamera.orthographicSize * 2f * edgePanViewHeightsPerSecond * Time.unscaledDeltaTime);
        if (!pointerOverUi) step += dragPixels * (targetCamera.orthographicSize * 2f / Mathf.Max(1,Screen.height));
        if (step.sqrMagnitude > 0f)
        {
            overviewMode = false;
            manualControl = true;
            forceCharacterFocus = false;
            transform.position = ClampToTerrain(
                transform.position + (Vector3)step);
        }

        // Scroll is an accumulated event, not a held velocity. Multiplying it by deltaTime
        // made high-refresh-rate machines zoom less and made keyboard/mouse disagree.
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) wheelNotches = 0f;
        if (Mathf.Abs(zoomDelta) > 0f || Mathf.Abs(wheelNotches) > 0f)
        {
            overviewMode = false;
            manualControl = true;
            forceCharacterFocus = false;
            targetCamera.orthographicSize = Mathf.Clamp(
                WheelZoomSize(targetCamera.orthographicSize, wheelNotches, wheelZoomFraction) + zoomDelta * zoomSpeed * Time.unscaledDeltaTime,
                minSize,
                maxSize);
            preferredViewSize=targetCamera.orthographicSize;
            transform.position = ClampToTerrain(transform.position);
        }
    }

    public static Vector2 EdgePanDirection(Vector2 cursor,Vector2 screen,float fraction)
    {
        if (screen.x<=0 || screen.y<=0 || cursor.x<0 || cursor.y<0 || cursor.x>screen.x || cursor.y>screen.y) return Vector2.zero;
        Vector2 edge = screen * Mathf.Clamp(fraction,.005f,.1f);
        float x = cursor.x<edge.x ? -(1-cursor.x/edge.x) : cursor.x>screen.x-edge.x ? 1-(screen.x-cursor.x)/edge.x : 0;
        float y = cursor.y<edge.y ? -(1-cursor.y/edge.y) : cursor.y>screen.y-edge.y ? 1-(screen.y-cursor.y)/edge.y : 0;
        return Vector2.ClampMagnitude(new Vector2(x,y),1);
    }

    public static float WheelZoomSize(float size, float notches, float fraction)
    {
        return size * Mathf.Pow(1f - Mathf.Clamp(fraction, .01f, .5f), notches);
    }

    public static float NormalizeWheelNotches(float delta, bool alreadyNormalized, float rawUnitsPerNotch)
    {
        return alreadyNormalized ? delta : delta / Mathf.Max(1f, rawUnitsPerNotch);
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
        if(!forceCharacterFocus && ObjectHeadRainbowSweep.Focus!=null){targetPosition=ObjectHeadRainbowSweep.Focus.position;return true;}
        if(Time.unscaledTime<settlementUntil){targetPosition=settlementBounds.center;return true;}
        if(!forceCharacterFocus && Time.unscaledTime<impactUntil)
        {targetPosition=confirmedImpact;return true;}
        SkillProjectile projectile = frameProjectile;
        if (!forceCharacterFocus && projectile != null && projectile.IsFlying)
        {
            targetPosition = projectile.transform.position;
            if(cinematic!=null)
                targetPosition+=Vector2.ClampMagnitude(projectile.GetComponent<Rigidbody2D>().linearVelocity*cinematic.lookAheadSeconds,cinematic.maximumLookAhead);
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
        SkillProjectile projectile = frameProjectile;
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

        if(Time.unscaledTime<settlementUntil)return;
        observedTurnSerial = turnManager.TurnSerial;
        // Return to the next actor smoothly, without leaving the previous manual pan active.
        manualControl=false;overviewMode=false;forceCharacterFocus=true;
        impactUntil=0;manualOffset=Vector3.zero;
        hasLastActionFocusPosition = false;
        lastSeenProjectile = null;
    }

    private void UpdateActionFocusFromProjectile()
    {
        SkillProjectile projectile = frameProjectile;
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
                turnManager.IsSettlementTimeActive || turnManager.IsTurnEndResolving ||
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
        minY -= cinematic!=null ? cinematic.oceanViewPadding : 0;
        maxY = terrain.TerrainOriginWorld.y + terrainHeight - halfHeight + paddingWorld.y;

        if (minX > maxX)
        {
            float centerX = terrain.TerrainOriginWorld.x + terrainWidth * 0.5f;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = terrain.TerrainOriginWorld.y + terrainHeight * 0.5f - (cinematic!=null ? cinematic.oceanViewPadding*.5f : 0);
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

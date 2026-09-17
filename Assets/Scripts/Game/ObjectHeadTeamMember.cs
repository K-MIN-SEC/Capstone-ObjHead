using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class ObjectHeadTeamMember : MonoBehaviour
{
    [SerializeField, Min(1)] private int playerIndex = 1;
    [SerializeField, Min(1)] private int teamSlotIndex = 1;
    [SerializeField] private string characterLabel = "Character";

    public int PlayerIndex => playerIndex;
    public int AllianceId => ObjectHeadMatchRules.Alliance(playerIndex);
    public int TeamSlotIndex => teamSlotIndex;
    public string CharacterLabel => characterLabel;
    public string DisplayName => $"P{playerIndex}-{teamSlotIndex} {characterLabel}";

    public void Configure(int ownerPlayerIndex, int slotIndex, ObjectHeadCharacterKind kind)
    {
        playerIndex = Mathf.Max(1, ownerPlayerIndex);
        teamSlotIndex = Mathf.Max(1, slotIndex);
        characterLabel = kind.ToString();
        gameObject.name = $"P{playerIndex}_Slot{teamSlotIndex}_{characterLabel}";
    }
}

public static class ObjectHeadTeamColors
{
    public static readonly Color TeamOne = new Color(1f, 0.16f, 0.16f, 1f);
    public static readonly Color TeamTwo = new Color(0.18f, 0.48f, 1f, 1f);

    public static Color GetColor(int playerIndex)
    {
        var palette = ObjectHeadContent.Load()?.allianceColors;
        int alliance = ObjectHeadMatchRules.Alliance(playerIndex);
        if (palette != null && alliance > 0 && alliance <= palette.Length) return palette[alliance - 1];
        if (playerIndex == 1)
        {
            return TeamOne;
        }

        if (playerIndex == 2)
        {
            return TeamTwo;
        }

        return Color.white;
    }
}

public static class ObjectHeadGamepadInput
{
    private const float StickDeadzone = 0.25f;
    private const float DpadThreshold = 0.5f;
    private const float TriggerThreshold = 0.45f;
    private static readonly int[] JumpButtonCandidates = { 0 };
    private static readonly int[] CancelButtonCandidates = { 2, 3 };
    private static readonly int[] AimLockButtonCandidates = { 6 };
    private static readonly int[] AimModeToggleButtonCandidates = { 1 };
    private static readonly int[] PreviousWeaponButtonCandidates = { 4 };
    private static readonly int[] NextWeaponButtonCandidates = { 5 };
    private static int legacyChargeTriggerFrame = -1;
    private static bool legacyChargeTriggerHeld;
    private static bool previousLegacyChargeTriggerHeld;
    private static int chargeTriggerFrame = -1;
    private static bool chargeTriggerHeld;
    private static bool previousChargeTriggerHeld;
    private static int legacyAimLockFrame = -1;
    private static bool legacyAimLockHeld;
    private static bool previousLegacyAimLockHeld;

    public static float MoveX()
    {
        // Horizontal also contains keyboard arrows. The fallback must be joystick-only.
        float legacyValue = ReadLegacyAxis("ObjectHeadLeftStickX");
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        float value = legacyValue;
        if (gamepad != null)
        {
            value = gamepad.leftStick.x.ReadValue();
        }
        else if (TryGetJoystick(out UnityEngine.InputSystem.Joystick joystick))
        {
            value = joystick.stick.x.ReadValue();
        }

        return Mathf.Abs(value) >= StickDeadzone ? value : 0f;
#else
        return Mathf.Abs(legacyValue) >= StickDeadzone ? legacyValue : 0f;
#endif
    }

    public static Vector2 CameraPan()
    {
        Vector2 legacyValue = new Vector2(
            ReadLegacyAxis("ObjectHeadDpadX"),
            ReadLegacyAxis("ObjectHeadDpadY"));
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        if (gamepad != null)
        {
            return NormalizeDpad(gamepad.dpad.ReadValue());
        }

        if (TryReadDpad(out Vector2 value))
        {
            return NormalizeDpad(value);
        }

        return NormalizeDpad(legacyValue);
#else
        return NormalizeDpad(legacyValue);
#endif
    }

    public static Vector2 AimStick()
    {
        Vector2 legacyValue = new Vector2(
            ReadLegacyAxis("ObjectHeadRightStickX"),
            -ReadLegacyAxis("ObjectHeadRightStickY"));
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        Vector2 value = legacyValue;
        if (gamepad != null)
        {
            value = gamepad.rightStick.ReadValue();
        }
        else if (TryReadStickControl(new[] { "rightStick", "stick2", "stick1" }, out Vector2 stickValue))
        {
            value = stickValue;
        }

        return value.sqrMagnitude >= StickDeadzone * StickDeadzone ? value : Vector2.zero;
#else
        return legacyValue.sqrMagnitude >= StickDeadzone * StickDeadzone ? legacyValue : Vector2.zero;
#endif
    }

    public static Vector2 AimDpad()
    {
        return AimStick();
    }

    public static bool WasJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("buttonSouth", "trigger", "button0") ||
               WasLegacyButtonPressed(JumpButtonCandidates);
#else
        return WasLegacyButtonPressed(JumpButtonCandidates);
#endif
    }

    public static bool IsJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed("buttonSouth", "trigger", "button0") ||
               IsLegacyButtonHeld(JumpButtonCandidates);
#else
        return IsLegacyButtonHeld(JumpButtonCandidates);
#endif
    }

    public static bool WasChargePressed()
    {
        UpdateChargeTriggerState();
        return chargeTriggerHeld && !previousChargeTriggerHeld;
    }

    public static bool IsChargeHeld()
    {
        UpdateChargeTriggerState();
        return chargeTriggerHeld;
    }

    public static bool WasChargeReleased()
    {
        UpdateChargeTriggerState();
        return !chargeTriggerHeld && previousChargeTriggerHeld;
    }

    public static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("buttonWest", "buttonNorth", "button2", "button3") ||
               WasLegacyButtonPressed(CancelButtonCandidates);
#else
        return WasLegacyButtonPressed(CancelButtonCandidates);
#endif
    }

    public static bool WasAimLockPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("leftTrigger", "leftTriggerButton", "button6") ||
               WasLegacyAimLockPressed();
#else
        return WasLegacyAimLockPressed();
#endif
    }

    public static bool WasAimModeTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("buttonEast", "button1") ||
               WasLegacyButtonPressed(AimModeToggleButtonCandidates);
#else
        return WasLegacyButtonPressed(AimModeToggleButtonCandidates);
#endif
    }

    public static bool IsAimLockHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed("leftTrigger", "leftTriggerButton", "button6") ||
               IsLegacyAimLockHeld();
#else
        return IsLegacyAimLockHeld();
#endif
    }

    public static string DebugChargeDecisionText()
    {
        return $"Charge held={IsChargeHeld()} / L2 gate={IsAimLockHeld()} / R2 raw={ReadInputSystemRightTriggerValue():0.00} / L2 raw={ReadInputSystemLeftTriggerValue():0.00} / Legacy R2={ReadLegacyRightTriggerValue():0.00} / Legacy L2={ReadLegacyLeftTriggerValue():0.00}";
    }

    public static bool WasPreviousWeaponPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("leftShoulder", "button4") ||
               WasLegacyButtonPressed(PreviousWeaponButtonCandidates);
#else
        return WasLegacyButtonPressed(PreviousWeaponButtonCandidates);
#endif
    }

    public static bool WasNextWeaponPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressed("rightShoulder", "button5") ||
               WasLegacyButtonPressed(NextWeaponButtonCandidates);
#else
        return WasLegacyButtonPressed(NextWeaponButtonCandidates);
#endif
    }

    private static float ReadLegacyAxis(params string[] axisNames)
    {
        for (int i = 0; i < axisNames.Length; i++)
        {
            try
            {
                float value = Input.GetAxisRaw(axisNames[i]);
                if (Mathf.Abs(value) > 0.001f)
                {
                    return value;
                }
            }
            catch (System.ArgumentException)
            {
            }
        }

        return 0f;
    }

    private static bool WasLegacyButtonPressed(int[] buttonIndices)
    {
        for (int i = 0; i < buttonIndices.Length; i++)
        {
            if (Input.GetKeyDown(LegacyJoystickButton(buttonIndices[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyButtonHeld(int[] buttonIndices)
    {
        for (int i = 0; i < buttonIndices.Length; i++)
        {
            if (Input.GetKey(LegacyJoystickButton(buttonIndices[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WasLegacyButtonReleased(int[] buttonIndices)
    {
        for (int i = 0; i < buttonIndices.Length; i++)
        {
            if (Input.GetKeyUp(LegacyJoystickButton(buttonIndices[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static void UpdateChargeTriggerState()
    {
        if (chargeTriggerFrame == Time.frameCount)
        {
            return;
        }

        previousChargeTriggerHeld = chargeTriggerHeld;
        bool rightTriggerHeld = IsRightTriggerPhysicallyHeld();
        chargeTriggerHeld = chargeTriggerHeld
            ? rightTriggerHeld
            : rightTriggerHeld && !IsAimLockHeld();
        chargeTriggerFrame = Time.frameCount;
    }

    private static bool IsRightTriggerPhysicallyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        if (gamepad != null && gamepad.rightTrigger.ReadValue() >= TriggerThreshold)
        {
            return true;
        }
#endif

        return IsLegacyChargeTriggerHeld();
    }

    private static bool WasLegacyChargeTriggerPressed()
    {
        UpdateLegacyChargeTriggerState();
        return legacyChargeTriggerHeld && !previousLegacyChargeTriggerHeld;
    }

    private static bool IsLegacyChargeTriggerHeld()
    {
        UpdateLegacyChargeTriggerState();
        return legacyChargeTriggerHeld;
    }

    private static bool WasLegacyChargeTriggerReleased()
    {
        UpdateLegacyChargeTriggerState();
        return !legacyChargeTriggerHeld && previousLegacyChargeTriggerHeld;
    }

    private static void UpdateLegacyChargeTriggerState()
    {
        if (legacyChargeTriggerFrame == Time.frameCount)
        {
            return;
        }

        previousLegacyChargeTriggerHeld = legacyChargeTriggerHeld;
        bool rightTriggerHeld = ReadLegacyRightTriggerValue() >= TriggerThreshold;
        legacyChargeTriggerHeld = legacyChargeTriggerHeld
            ? rightTriggerHeld
            : rightTriggerHeld && !IsLegacyAimLockHeld();
        legacyChargeTriggerFrame = Time.frameCount;
    }

    private static float ReadLegacyRightTriggerValue()
    {
        return Mathf.Max(
            Mathf.Max(0f, ReadLegacyAxis("ObjectHeadRightTrigger")),
            Mathf.Max(0f, ReadLegacyAxis("ObjectHeadRightTriggerAlt")));
    }

    private static bool WasLegacyAimLockPressed()
    {
        UpdateLegacyAimLockState();
        return legacyAimLockHeld && !previousLegacyAimLockHeld;
    }

    private static bool IsLegacyAimLockHeld()
    {
        UpdateLegacyAimLockState();
        return legacyAimLockHeld;
    }

    private static void UpdateLegacyAimLockState()
    {
        if (legacyAimLockFrame == Time.frameCount)
        {
            return;
        }

        previousLegacyAimLockHeld = legacyAimLockHeld;
        legacyAimLockHeld =
            IsLegacyButtonHeld(AimLockButtonCandidates) ||
            ReadLegacyLeftTriggerValue() >= TriggerThreshold;
        legacyAimLockFrame = Time.frameCount;
    }

    private static float ReadLegacyLeftTriggerValue()
    {
        return Mathf.Max(
            Mathf.Max(0f, ReadLegacyAxis("ObjectHeadLeftTrigger")),
            Mathf.Max(0f, ReadLegacyAxis("ObjectHeadLeftTriggerAlt")));
    }

    private static float ReadInputSystemLeftTriggerValue()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        return gamepad != null ? gamepad.leftTrigger.ReadValue() : 0f;
#else
        return 0f;
#endif
    }

    private static float ReadInputSystemRightTriggerValue()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        return gamepad != null ? gamepad.rightTrigger.ReadValue() : 0f;
#else
        return 0f;
#endif
    }

    private static KeyCode LegacyJoystickButton(int buttonIndex)
    {
        return (KeyCode)((int)KeyCode.JoystickButton0 + Mathf.Clamp(buttonIndex, 0, 19));
    }

    private static Vector2 NormalizeDpad(Vector2 value)
    {
        value.x = Mathf.Abs(value.x) >= DpadThreshold ? Mathf.Sign(value.x) : 0f;
        value.y = Mathf.Abs(value.y) >= DpadThreshold ? Mathf.Sign(value.y) : 0f;
        return value;
    }

#if ENABLE_INPUT_SYSTEM
    private static UnityEngine.InputSystem.Gamepad GetGamepad()
    {
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad != null)
        {
            return gamepad;
        }

        return UnityEngine.InputSystem.Gamepad.all.Count > 0
            ? UnityEngine.InputSystem.Gamepad.all[0]
            : null;
    }

    private static bool TryGetJoystick(out UnityEngine.InputSystem.Joystick joystick)
    {
        joystick = UnityEngine.InputSystem.Joystick.current;
        if (joystick != null)
        {
            return true;
        }

        if (UnityEngine.InputSystem.Joystick.all.Count > 0)
        {
            joystick = UnityEngine.InputSystem.Joystick.all[0];
            return joystick != null;
        }

        return false;
    }

    private static bool WasPressed(params string[] controlNames)
    {
        UnityEngine.InputSystem.Controls.ButtonControl button = FindButton(controlNames);
        return button != null && button.wasPressedThisFrame;
    }

    private static bool IsPressed(params string[] controlNames)
    {
        UnityEngine.InputSystem.Controls.ButtonControl button = FindButton(controlNames);
        return button != null && button.isPressed;
    }

    private static bool WasReleased(params string[] controlNames)
    {
        UnityEngine.InputSystem.Controls.ButtonControl button = FindButton(controlNames);
        return button != null && button.wasReleasedThisFrame;
    }

    private static UnityEngine.InputSystem.Controls.ButtonControl FindButton(params string[] controlNames)
    {
        UnityEngine.InputSystem.Gamepad gamepad = GetGamepad();
        UnityEngine.InputSystem.Controls.ButtonControl button = FindButtonOnDevice(gamepad, controlNames);
        if (button != null)
        {
            return button;
        }

        if (TryGetJoystick(out UnityEngine.InputSystem.Joystick joystick))
        {
            button = FindButtonOnDevice(joystick, controlNames);
            if (button != null)
            {
                return button;
            }
        }

        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            button = FindButtonOnDevice(device, controlNames);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static UnityEngine.InputSystem.Controls.ButtonControl FindButtonOnDevice(
        UnityEngine.InputSystem.InputDevice device,
        string[] controlNames)
    {
        if (device == null)
        {
            return null;
        }

        for (int i = 0; i < controlNames.Length; i++)
        {
            UnityEngine.InputSystem.Controls.ButtonControl button =
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(controlNames[i]);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static bool TryReadStickControl(string[] controlNames, out Vector2 value)
    {
        value = Vector2.zero;
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            for (int i = 0; i < controlNames.Length; i++)
            {
                UnityEngine.InputSystem.Controls.StickControl stick =
                    device.TryGetChildControl<UnityEngine.InputSystem.Controls.StickControl>(controlNames[i]);
                if (stick != null)
                {
                    value = stick.ReadValue();
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadDpad(out Vector2 value)
    {
        value = Vector2.zero;
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            UnityEngine.InputSystem.Controls.DpadControl dpad =
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.DpadControl>("dpad");
            if (dpad != null)
            {
                value = dpad.ReadValue();
                return true;
            }
        }

        return false;
    }
#endif
}

[DisallowMultipleComponent]
public class ObjectHeadGamepadDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    [SerializeField] private Vector2 screenPosition = new Vector2(16f, 120f);
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 320f);

    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private Texture2D backgroundTexture;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            visible = !visible;
        }
#endif
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        EnsureStyles();
        Rect panelRect = new Rect(screenPosition.x, screenPosition.y, panelSize.x, panelSize.y);
        GUI.Box(panelRect, GUIContent.none, boxStyle);

        Rect labelRect = new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, panelRect.height - 20f);
        GUI.Label(labelRect, BuildStatusText(), labelStyle);
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null)
        {
            Destroy(backgroundTexture);
        }
    }

    private void EnsureStyles()
    {
        if (boxStyle != null && labelStyle != null)
        {
            return;
        }

        backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        backgroundTexture.Apply();

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = backgroundTexture;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = Color.white;
        labelStyle.wordWrap = false;
    }

    private string BuildStatusText()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad == null && UnityEngine.InputSystem.Gamepad.all.Count > 0)
        {
            gamepad = UnityEngine.InputSystem.Gamepad.all[0];
        }

        string deviceText = BuildDeviceSummary();
        string pressedText = BuildPressedControlSummary();
        string legacyPressedText = BuildLegacyPressedSummary();
        string legacyAxisText = BuildLegacyAxisSummary();
        if (gamepad == null)
        {
            UnityEngine.InputSystem.Joystick joystick = UnityEngine.InputSystem.Joystick.current;
            if (joystick == null && UnityEngine.InputSystem.Joystick.all.Count > 0)
            {
                joystick = UnityEngine.InputSystem.Joystick.all[0];
            }

            if (joystick != null)
            {
                return $"Joystick: {joystick.displayName}\n" +
                       $"Device: {joystick.name}\n" +
                       $"Stick: {FormatVector(joystick.stick.ReadValue())}\n" +
                       pressedText +
                       legacyPressedText +
                       legacyAxisText +
                       deviceText +
                       "F9: hide/show";
            }

            return "Gamepad/Joystick: not detected\n" +
                   pressedText +
                   legacyPressedText +
                   legacyAxisText +
                   deviceText +
                   "Input Debugger: Window > Analysis > Input Debugger\n" +
                   "F9: hide/show";
        }

        Vector2 leftStick = gamepad.leftStick.ReadValue();
        return $"Gamepad: {gamepad.displayName}\n" +
               $"Device: {gamepad.name}\n" +
               $"Left Stick move: {FormatVector(leftStick)}\n" +
               $"Right Stick aim: {FormatVector(ObjectHeadGamepadInput.AimStick())}\n" +
               $"D-pad camera: {FormatVector(ObjectHeadGamepadInput.CameraPan())}\n" +
               $"B/South jump: {ButtonState(gamepad.buttonSouth)}\n" +
               $"A/East aim mode: {ButtonState(gamepad.buttonEast)} / {AimController.GamepadAimModeLabel}\n" +
               $"L2 aim lock: {ButtonState(gamepad.leftTrigger)}\n" +
               $"R2 charge: {ButtonState(gamepad.rightTrigger)}\n" +
               $"{ObjectHeadGamepadInput.DebugChargeDecisionText()}\n" +
               $"Y/West cancel: {ButtonState(gamepad.buttonWest)}\n" +
               $"X/North cancel fallback: {ButtonState(gamepad.buttonNorth)}\n" +
               $"L/R select: {ButtonState(gamepad.leftShoulder)} / {ButtonState(gamepad.rightShoulder)}\n" +
               pressedText +
               legacyPressedText +
               legacyAxisText +
               "F9: hide/show";
#else
        return "Gamepad debug requires Unity Input System.";
#endif
    }

    private string FormatVector(Vector2 value)
    {
        return $"{value.x:0.00}, {value.y:0.00}";
    }

#if ENABLE_INPUT_SYSTEM
    private string BuildDeviceSummary()
    {
        StringBuilder builder = new StringBuilder();
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.InputDevice> devices =
            UnityEngine.InputSystem.InputSystem.devices;
        builder.Append("Devices: ");
        builder.Append(devices.Count);

        int shown = Mathf.Min(devices.Count, 4);
        for (int i = 0; i < shown; i++)
        {
            builder.Append("\n- ");
            builder.Append(devices[i].displayName);
            builder.Append(" / ");
            builder.Append(devices[i].layout);
        }

        if (devices.Count > shown)
        {
            builder.Append("\n- ...");
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private string BuildPressedControlSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Pressed: ");
        int shown = 0;
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            foreach (UnityEngine.InputSystem.InputControl control in device.allControls)
            {
                UnityEngine.InputSystem.Controls.ButtonControl button =
                    control as UnityEngine.InputSystem.Controls.ButtonControl;
                if (button == null || !button.isPressed)
                {
                    continue;
                }

                if (shown > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(device.name);
                builder.Append('/');
                builder.Append(control.name);
                shown++;

                if (shown >= 6)
                {
                    builder.Append(", ...");
                    break;
                }
            }

            if (shown >= 6)
            {
                break;
            }
        }

        if (shown == 0)
        {
            builder.Append('-');
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private string BuildLegacyPressedSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Legacy pressed: ");
        int shown = 0;
        for (int i = 0; i <= 19; i++)
        {
            if (!Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + i)))
            {
                continue;
            }

            if (shown > 0)
            {
                builder.Append(", ");
            }

            builder.Append(i);
            shown++;
        }

        if (shown == 0)
        {
            builder.Append('-');
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private string BuildLegacyAxisSummary()
    {
        string[] names =
        {
            "Horizontal",
            "Vertical",
            "ObjectHeadRightStickX",
            "ObjectHeadRightStickY",
            "ObjectHeadDpadX",
            "ObjectHeadDpadY",
            "ObjectHeadLeftTrigger",
            "ObjectHeadLeftTriggerAlt",
            "ObjectHeadRightTrigger",
            "ObjectHeadRightTriggerAlt",
            "ObjectHeadTriggerShared"
        };
        StringBuilder builder = new StringBuilder();
        builder.Append("Legacy axes: ");
        for (int i = 0; i < names.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(names[i]);
            builder.Append('=');
            builder.Append(ReadAxisForDebug(names[i]).ToString("0.00"));
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private float ReadAxisForDebug(string axisName)
    {
        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch (System.ArgumentException)
        {
            return 0f;
        }
    }

    private string ButtonState(UnityEngine.InputSystem.Controls.ButtonControl button)
    {
        if (button.wasPressedThisFrame)
        {
            return "down";
        }

        if (button.isPressed)
        {
            return "held";
        }

        if (button.wasReleasedThisFrame)
        {
            return "up";
        }

        return "-";
    }
#endif
}

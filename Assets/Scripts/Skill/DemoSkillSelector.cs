using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum SkillEffectType
{
    DamageExplosion,
    DelayedExplosion,
    ChainExplosion,
    CreateTerrainCircle,
    CreateTerrainBridge,
    CreateHazardZone,
    CreateSlowZone
}

public struct ObjectHeadSkillSettings
{
    public SkillEffectType effectType;
    public Sprite headSprite;
    public Color projectileColor;
    public Color impactColor;
    public int maxDamage;
    public float explosionRadiusWorld;
    public float knockbackForce;
    public int terrainRadiusPx;
    public float bridgeLengthWorld;
    public int bridgeThicknessPx;
    public int chainCount;
    public float chainSpacingWorld;
    public float chainDelaySeconds;
    public int chainMaxTotalDamage;
    public float chainSpreadRadiusWorld;
    public bool useWideClusterPattern;
    public bool useRollingChainPath;
    public float rollingChainMinSpeed;
    public float rollingChainAngularSpeed;
    public float delaySeconds;
    public int zoneDurationTurns;
    public int zoneDamagePerTick;
    public float zoneTickSeconds;
    public int zoneDurationRounds;
    public int zoneDamagePerTurn;
    public float zoneLengthWorld;
    public float zoneThicknessWorld;
    public float slowMultiplier;
    public float projectileVisualDiameter;
    public bool blinkBeforeEffect;
    public float blinkSeconds;
    public float blinkIntervalSeconds;
    public Sprite blinkSpriteA;
    public Sprite blinkSpriteB;
    public int skillId;
    public int commonHeadTypeId;
    public int terrainBurstCount;
    public int terrainBurstStampRadiusPx;
    public int terrainBurstMaxPlacementAttemptsPerStamp;
    public float terrainBurstIntervalSeconds;
    public float terrainBurstSpreadWorld;
    public float terrainBurstVerticalBiasWorld;
    public float finalTerrainRadiusXWorld;
    public float finalTerrainRadiusYWorld;
    public float maxBuildHeightAboveSurfaceWorld;

    public static ObjectHeadSkillSettings CreateDefault(
        Sprite headSprite,
        Color projectileColor,
        Color impactColor,
        int maxDamage,
        float explosionRadiusWorld,
        float knockbackForce)
    {
        return new ObjectHeadSkillSettings
        {
            effectType = SkillEffectType.DamageExplosion,
            headSprite = headSprite,
            projectileColor = projectileColor,
            impactColor = impactColor,
            maxDamage = maxDamage,
            explosionRadiusWorld = explosionRadiusWorld,
            knockbackForce = knockbackForce,
            terrainRadiusPx = 0,
            bridgeLengthWorld = 0f,
            bridgeThicknessPx = 8,
            chainCount = 1,
            chainSpacingWorld = 0.25f,
            chainDelaySeconds = 0.1f,
            chainMaxTotalDamage = maxDamage,
            chainSpreadRadiusWorld = 0f,
            useWideClusterPattern = false,
            useRollingChainPath = false,
            rollingChainMinSpeed = 0f,
            rollingChainAngularSpeed = 0f,
            delaySeconds = 0f,
            zoneDurationTurns = 0,
            zoneDamagePerTick = 0,
            zoneTickSeconds = 1f,
            zoneDurationRounds = 0,
            zoneDamagePerTurn = 0,
            zoneLengthWorld = 0f,
            zoneThicknessWorld = 0.2f,
            slowMultiplier = 1f,
            projectileVisualDiameter = 0.58f,
            blinkBeforeEffect = false,
            blinkSeconds = 0.45f,
            blinkIntervalSeconds = 0.08f,
            blinkSpriteA = null,
            blinkSpriteB = null,
            skillId = 0,
            commonHeadTypeId = 0,
            terrainBurstCount = 0,
            terrainBurstStampRadiusPx = 0,
            terrainBurstMaxPlacementAttemptsPerStamp = 4,
            terrainBurstIntervalSeconds = 0.06f,
            terrainBurstSpreadWorld = 1f,
            terrainBurstVerticalBiasWorld = 0f,
            finalTerrainRadiusXWorld = 1f,
            finalTerrainRadiusYWorld = 0.8f,
            maxBuildHeightAboveSurfaceWorld = 5f
        };
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterVisual))]
public class DemoSkillSelector : MonoBehaviour
{
    private const int BasicSlotCount = 3;
    private const int CommonSlotCount = 3;
    private const int TotalLoadoutSlotCount = BasicSlotCount + CommonSlotCount;

    [SerializeField] private ObjectHeadCharacterKind characterKind = ObjectHeadCharacterKind.Bulb;
    [SerializeField, Range(0, 2)] private int selectedSkillIndex;
    [SerializeField] private bool allowKeyboardSelection = true;

    [Header("Seed Terrain Growth")]
    [SerializeField, Min(1)] private int seedTerrainBurstCount = 12;
    [SerializeField, Min(1)] private int seedTerrainBurstStampRadiusPx = 9;
    [SerializeField, Min(0.01f)] private float seedTerrainBurstIntervalSeconds = 0.055f;
    [SerializeField, Min(1)] private int terrainBurstMaxPlacementAttemptsPerStamp = 4;
    [SerializeField, Min(0.1f)] private float seedTerrainRadiusXWorld = 1.2f;
    [SerializeField, Min(0.5f)] private float seedTerrainRadiusYWorld = 0.95f;
    [SerializeField, Min(0.5f)] private float minimumCreatedTerrainRadiusYWorld = 0.5f;
    [SerializeField, Min(0.5f)] private float maxBuildHeightAboveSurfaceWorld = 5f;

    private readonly int[] remainingCooldowns = new int[3];
    private CharacterVisual characterVisual;
    private TurnCharacterController turnCharacter;
    private CommonHeadUseController commonHeadUseController;
    private ObjectHeadBalanceTable balance;

    public ObjectHeadCharacterKind CharacterKind => characterKind;
    public int SelectedSkillIndex => selectedSkillIndex;

    private void Awake()
    {
        balance = ObjectHeadBalanceTable.Load();
        characterVisual = GetComponent<CharacterVisual>();
        turnCharacter = GetComponent<TurnCharacterController>();
        commonHeadUseController = GetComponent<CommonHeadUseController>();
        ApplySelection();
    }

    private void Update()
    {
        if (!allowKeyboardSelection || turnCharacter == null || !turnCharacter.HasControl)
        {
            return;
        }

        TurnManager manager = FindTurnManager();
        if (manager == null || !manager.CanCharacterFire(turnCharacter))
        {
            return;
        }

        ReadSelectionInput();
    }

    public void SetCharacterKind(ObjectHeadCharacterKind kind)
    {
        CancelCommonHeadSelection();
        characterKind = kind;
        ApplySelection();
    }

    public void SetSkillIndex(int index)
    {
        CancelCommonHeadSelection();
        selectedSkillIndex = Mathf.Clamp(index, 0, 2);
        ApplySelection();
    }

    public bool CanUseSelectedSkill()
    {
        return GetRemainingCooldown(selectedSkillIndex) <= 0;
    }

    public int GetRemainingCooldown(int skillIndex)
    {
        return remainingCooldowns[Mathf.Clamp(skillIndex, 0, 2)];
    }

    public int GetCooldownDuration(int skillIndex)
    {
        skillIndex = Mathf.Clamp(skillIndex, 0, 2);
        int fallback = skillIndex == 0 ? 0 : skillIndex == 1 ? 2 : 3;
        return BalanceInt($"skill.cooldown.{skillIndex + 1}", fallback);
    }

    public void NotifyTurnStarted()
    {
        for (int i = 0; i < remainingCooldowns.Length; i++)
        {
            remainingCooldowns[i] = Mathf.Max(0, remainingCooldowns[i] - 1);
        }

        if (!CanUseSelectedSkill())
        {
            SelectFirstReadySkill();
        }
    }

    public void NotifySkillFired()
    {
        remainingCooldowns[selectedSkillIndex] = GetCooldownDuration(selectedSkillIndex);
    }

    public ObjectHeadSkillSettings GetCurrentSkillSettings()
    {
        Sprite headSprite = characterVisual != null ? characterVisual.CurrentHeadSprite : null;
        ObjectHeadSkillSettings settings = ObjectHeadSkillSettings.CreateDefault(
            headSprite,
            new Color(1f, 0.35f, 0.05f, 1f),
            new Color(1f, 0.25f, 0f, 0.55f),
            20,
            0.8f,
            7f);

        switch (characterKind)
        {
            case ObjectHeadCharacterKind.Bulb:
                ConfigureBulbSkill(ref settings);
                break;
            case ObjectHeadCharacterKind.Seed:
                ConfigureSeedSkill(ref settings);
                break;
            case ObjectHeadCharacterKind.Bomb:
                ConfigureBombSkill(ref settings);
                break;
        }

        settings.skillId = ((int)characterKind + 1) * 10 + selectedSkillIndex + 1;
        settings.headSprite = headSprite;
        return settings;
    }

    private void ConfigureBulbSkill(ref ObjectHeadSkillSettings settings)
    {
        settings.projectileColor = new Color(0.85f, 0.82f, 0.68f, 1f);
        settings.impactColor = new Color(1f, 0.95f, 0.45f, 0.5f);
        settings.projectileVisualDiameter = 0.62f;

        if (selectedSkillIndex == 0)
        {
            settings.effectType = SkillEffectType.CreateHazardZone;
            settings.maxDamage = SkillInt("max_damage", 5);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.7f);
            settings.knockbackForce = 0f;
            settings.zoneDamagePerTurn = SkillInt("zone_damage_per_turn", 8);
            settings.zoneDurationRounds = SkillInt("zone_duration_rounds", 2);
            settings.zoneLengthWorld = SkillFloat("zone_length_world", 4.5f);
            settings.zoneThicknessWorld = SkillFloat("zone_thickness_world", 0.18f);
            settings.slowMultiplier = 1f;
            return;
        }

        if (selectedSkillIndex == 1)
        {
            settings.effectType = SkillEffectType.CreateSlowZone;
            settings.maxDamage = SkillInt("max_damage", 15);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 1.4f);
            settings.knockbackForce = SkillFloat("knockback_force", 2.5f);
            settings.terrainRadiusPx = 0;
            settings.zoneDamagePerTurn = SkillInt("zone_damage_per_turn", 10);
            settings.zoneDurationRounds = SkillInt("zone_duration_rounds", 2);
            settings.zoneLengthWorld = SkillFloat("zone_length_world", 6f);
            settings.zoneThicknessWorld = SkillFloat("zone_thickness_world", 0.22f);
            settings.slowMultiplier = SkillFloat("slow_multiplier", 0.6f);
            settings.impactColor = new Color(1f, 0.9f, 0.15f, 0.48f);
            return;
        }

        settings.effectType = SkillEffectType.ChainExplosion;
        settings.maxDamage = SkillInt("max_damage", 8);
        settings.chainMaxTotalDamage = SkillInt("chain_max_total_damage", 35);
        settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.7f);
        settings.knockbackForce = SkillFloat("knockback_force", 4f);
        settings.terrainRadiusPx = SkillInt("terrain_radius_px", 17);
        settings.chainCount = SkillInt("chain_count", 5);
        settings.chainSpacingWorld = SkillFloat("chain_spacing_world", 0.4f);
        settings.chainDelaySeconds = SkillFloat("chain_delay_seconds", 0.1f);
        settings.blinkBeforeEffect = true;
        settings.blinkSeconds = SkillFloat("blink_seconds", 0.7f);
        settings.blinkIntervalSeconds = SkillFloat("blink_interval_seconds", 0.085f);
        settings.blinkSpriteA = Resources.Load<Sprite>("Sprites/Heads/head_bulb_on");
        settings.blinkSpriteB = Resources.Load<Sprite>("Sprites/Heads/head_bulb_off");
    }

    private void ConfigureSeedSkill(ref ObjectHeadSkillSettings settings)
    {
        settings.projectileColor = new Color(0.36f, 0.62f, 0.28f, 1f);
        settings.impactColor = new Color(0.35f, 0.9f, 0.35f, 0.5f);
        settings.knockbackForce = 2f;
        settings.projectileVisualDiameter = 0.58f;

        if (selectedSkillIndex == 0)
        {
            settings.effectType = SkillEffectType.CreateTerrainCircle;
            settings.maxDamage = SkillInt("max_damage", 5);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.48f);
            settings.terrainRadiusPx = SkillInt("terrain_radius_px", 18);
            settings.terrainBurstCount = SkillInt("terrain_burst_count", seedTerrainBurstCount);
            settings.terrainBurstStampRadiusPx = SkillInt("terrain_burst_stamp_radius_px", seedTerrainBurstStampRadiusPx);
            settings.terrainBurstMaxPlacementAttemptsPerStamp = terrainBurstMaxPlacementAttemptsPerStamp;
            settings.terrainBurstIntervalSeconds = SkillFloat("terrain_burst_interval_seconds", seedTerrainBurstIntervalSeconds);
            settings.terrainBurstSpreadWorld = SkillFloat("terrain_burst_spread_world", seedTerrainRadiusXWorld);
            settings.terrainBurstVerticalBiasWorld = 0f;
            settings.finalTerrainRadiusXWorld = SkillFloat("terrain_burst_spread_world", seedTerrainRadiusXWorld);
            settings.finalTerrainRadiusYWorld = Mathf.Max(
                minimumCreatedTerrainRadiusYWorld,
                SkillFloat("final_radius_y_world", seedTerrainRadiusYWorld));
            settings.maxBuildHeightAboveSurfaceWorld = SkillFloat("max_build_height_world", maxBuildHeightAboveSurfaceWorld);
            return;
        }

        if (selectedSkillIndex == 1)
        {
            settings.effectType = SkillEffectType.CreateTerrainBridge;
            settings.maxDamage = SkillInt("max_damage", 3);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.38f);
            settings.bridgeLengthWorld = SkillFloat("bridge_length_world", 6.5f);
            settings.bridgeThicknessPx = SkillInt("bridge_thickness_px", 9);
            return;
        }

        settings.effectType = SkillEffectType.CreateHazardZone;
        settings.maxDamage = SkillInt("max_damage", 10);
        settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.8f);
        settings.zoneDamagePerTurn = SkillInt("zone_damage_per_turn", 12);
        settings.zoneDurationRounds = SkillInt("zone_duration_rounds", 2);
        settings.zoneLengthWorld = SkillFloat("zone_length_world", 5.5f);
        settings.zoneThicknessWorld = SkillFloat("zone_thickness_world", 0.25f);
        settings.slowMultiplier = 1f;
        settings.impactColor = new Color(0.24f, 0.78f, 0.24f, 0.48f);
    }

    private void ConfigureBombSkill(ref ObjectHeadSkillSettings settings)
    {
        settings.projectileColor = new Color(1f, 0.75f, 0.05f, 1f);
        settings.impactColor = new Color(1f, 0.15f, 0f, 0.58f);
        settings.projectileVisualDiameter = 0.62f;

        if (selectedSkillIndex == 0)
        {
            settings.effectType = SkillEffectType.DamageExplosion;
            settings.maxDamage = SkillInt("max_damage", 15);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 1.1f);
            settings.terrainRadiusPx = SkillInt("terrain_radius_px", 26);
            settings.knockbackForce = SkillFloat("knockback_force", 12f);
            return;
        }

        if (selectedSkillIndex == 1)
        {
            settings.effectType = SkillEffectType.DelayedExplosion;
            settings.maxDamage = SkillInt("max_damage", 30);
            settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 1.4f);
            settings.terrainRadiusPx = SkillInt("terrain_radius_px", 40);
            settings.knockbackForce = SkillFloat("knockback_force", 7f);
            settings.delaySeconds = SkillFloat("delay_seconds", 2f);
            settings.impactColor = new Color(1f, 0.05f, 0.02f, 0.58f);
            return;
        }

        settings.effectType = SkillEffectType.ChainExplosion;
        settings.maxDamage = SkillInt("max_damage", 9);
        settings.chainMaxTotalDamage = SkillInt("chain_max_total_damage", 45);
        settings.explosionRadiusWorld = SkillFloat("explosion_radius_world", 0.9f);
        settings.terrainRadiusPx = SkillInt("terrain_radius_px", 28);
        settings.knockbackForce = SkillFloat("knockback_force", 7f);
        settings.chainCount = SkillInt("chain_count", 6);
        settings.chainSpacingWorld = SkillFloat("chain_spacing_world", 0.5f);
        settings.chainDelaySeconds = SkillFloat("chain_delay_seconds", 0.14f);
        settings.useRollingChainPath = true;
        settings.rollingChainMinSpeed = SkillFloat("rolling_min_speed", 3.2f);
        settings.rollingChainAngularSpeed = SkillFloat("rolling_angular_speed", 720f);
        settings.impactColor = new Color(1f, 0.85f, 0.05f, 0.55f);
    }

    private void ApplySelection()
    {
        selectedSkillIndex = Mathf.Clamp(selectedSkillIndex, 0, 2);

        if (characterVisual == null)
        {
            characterVisual = GetComponent<CharacterVisual>();
        }

        if (characterVisual != null)
        {
            characterVisual.SetCharacterKind(characterKind);
            characterVisual.SetSkillIndex(selectedSkillIndex);
        }
    }

    private float SkillFloat(string stat, float fallback)
    {
        return BalanceFloat($"skill.{characterKind.ToString().ToLowerInvariant()}.{selectedSkillIndex + 1}.{stat}", fallback);
    }

    private int SkillInt(string stat, int fallback)
    {
        return BalanceInt($"skill.{characterKind.ToString().ToLowerInvariant()}.{selectedSkillIndex + 1}.{stat}", fallback);
    }

    private float BalanceFloat(string key, float fallback)
    {
        balance ??= ObjectHeadBalanceTable.Load();
        return balance != null ? balance.GetFloat(key, fallback) : fallback;
    }

    private int BalanceInt(string key, int fallback)
    {
        balance ??= ObjectHeadBalanceTable.Load();
        return balance != null ? balance.GetInt(key, fallback) : fallback;
    }

    private void SelectFirstReadySkill()
    {
        for (int i = 0; i < 3; i++)
        {
            if (remainingCooldowns[i] <= 0)
            {
                SetSkillIndex(i);
                return;
            }
        }
    }

    private void CancelCommonHeadSelection()
    {
        if (commonHeadUseController == null)
        {
            commonHeadUseController = GetComponent<CommonHeadUseController>();
        }

        commonHeadUseController?.CancelSelectionAndRestoreUniqueHead();
    }

    private void ReadSelectionInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SetSkillIndex(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SetSkillIndex(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SetSkillIndex(2);
            if (keyboard.f1Key.wasPressedThisFrame) SetCharacterKind(ObjectHeadCharacterKind.Bulb);
            if (keyboard.f2Key.wasPressedThisFrame) SetCharacterKind(ObjectHeadCharacterKind.Seed);
            if (keyboard.f3Key.wasPressedThisFrame) SetCharacterKind(ObjectHeadCharacterKind.Bomb);
        }

        if (ObjectHeadGamepadInput.WasPreviousWeaponPressed())
        {
            CycleLoadoutSelection(-1);
        }

        if (ObjectHeadGamepadInput.WasNextWeaponPressed())
        {
            CycleLoadoutSelection(1);
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetSkillIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetSkillIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetSkillIndex(2);
        if (Input.GetKeyDown(KeyCode.F1)) SetCharacterKind(ObjectHeadCharacterKind.Bulb);
        if (Input.GetKeyDown(KeyCode.F2)) SetCharacterKind(ObjectHeadCharacterKind.Seed);
        if (Input.GetKeyDown(KeyCode.F3)) SetCharacterKind(ObjectHeadCharacterKind.Bomb);
#endif
    }

    private void CycleLoadoutSelection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        int currentIndex = GetCurrentLoadoutIndex();
        int step = direction > 0 ? 1 : -1;
        for (int attempt = 1; attempt <= TotalLoadoutSlotCount; attempt++)
        {
            int candidate = Mod(currentIndex + step * attempt, TotalLoadoutSlotCount);
            if (TrySelectLoadoutIndex(candidate))
            {
                return;
            }
        }
    }

    private int GetCurrentLoadoutIndex()
    {
        if (commonHeadUseController != null && commonHeadUseController.HasSelectedCommonHead)
        {
            return BasicSlotCount + commonHeadUseController.SelectedSlotIndex;
        }

        return Mathf.Clamp(selectedSkillIndex, 0, BasicSlotCount - 1);
    }

    private bool TrySelectLoadoutIndex(int loadoutIndex)
    {
        if (loadoutIndex < BasicSlotCount)
        {
            SetSkillIndex(loadoutIndex);
            return true;
        }

        if (commonHeadUseController == null)
        {
            commonHeadUseController = GetComponent<CommonHeadUseController>();
        }

        int commonSlotIndex = loadoutIndex - BasicSlotCount;
        return commonHeadUseController != null &&
               commonHeadUseController.HasCommonHeadInSlot(commonSlotIndex) &&
               commonHeadUseController.TrySelectCommonHeadSlot(commonSlotIndex);
    }

    private static int Mod(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }

    private static TurnManager FindTurnManager()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<TurnManager>();
#else
        return Object.FindObjectOfType<TurnManager>();
#endif
    }
}

public static class TerrainGrowthSeedUtility
{
    public static int Build(
        CharacterCombat owner,
        TurnManager turnManager,
        int skillId,
        int commonHeadTypeId,
        Vector2 worldPosition)
    {
        ObjectHeadTeamMember member = owner != null
            ? owner.GetComponent<ObjectHeadTeamMember>()
            : null;
        int playerIndex = member != null ? member.PlayerIndex : 0;
        int slotIndex = member != null ? member.TeamSlotIndex : 0;
        int quantizedX = Mathf.RoundToInt(worldPosition.x * 16f);
        int quantizedY = Mathf.RoundToInt(worldPosition.y * 16f);

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + ObjectHeadMatchBootstrap.CurrentMatchSeed;
            hash = hash * 31 + (turnManager != null ? turnManager.TurnSerial : 0);
            hash = hash * 31 + playerIndex;
            hash = hash * 31 + slotIndex;
            hash = hash * 31 + skillId;
            hash = hash * 31 + commonHeadTypeId;
            hash = hash * 31 + quantizedX;
            hash = hash * 31 + quantizedY;
            return hash;
        }
    }
}

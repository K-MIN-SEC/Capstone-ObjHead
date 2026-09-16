using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class ObjectHeadMatchBootstrap : MonoBehaviour
{
    private const string TerrainResourcePath = "Sprites/Map/terrain_map_object_head_1536x512";
    private const string CollisionMaskResourcePath = "Sprites/Map/terrain_collision_mask_1536x512";
    private const string BackgroundResourcePath = "Sprites/Map/background_ocean_sky";

    [SerializeField, Range(2, 4)] private int playerCount = 2;
    [SerializeField] private Vector2 terrainOriginWorld = new Vector2(-24f, -8f);
    [SerializeField] private int pixelsPerUnit = 32;
    [SerializeField] private int chunkSizePx = 64;
    [SerializeField] private int collisionCellSizePx = 4;
    [SerializeField, Range(1f, 2.5f)] private float mapWidthMultiplier = 1.5f;
    [SerializeField, Range(12f, 20f)] private float upperSkyPaddingWorld = 16f;
    [SerializeField] private bool useFixedTestSpawnSeed;
    [FormerlySerializedAs("deterministicSpawnSeed")]
    [SerializeField] private int fixedTestSpawnSeed = 6974;
    [SerializeField] private int commonHeadSpawnSeed = 6975;
    [SerializeField] private bool characterSpritesFaceRightByDefault = false;
    [SerializeField, Min(0.1f)] private float characterMoveSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float characterJumpForce = 6f;
    [SerializeField] private float waterSurfaceY = -7.5f;
    [SerializeField] private float terrainWaterContactOffset = 0.05f;
    [SerializeField, Range(0f, 1f)] private float backgroundWaterlineNormalized = 0.166f;
    [SerializeField] private bool addTerrainEditBrush = true;
    [SerializeField] private bool cleanupExistingPrototypeScene = true;
    [SerializeField] private bool addGamepadDebugOverlay = true;
    [SerializeField] private ObjectHeadMapAuthoring mapAuthoring;
    [Header("Authored scene references")]
    [SerializeField] private TerrainManager sceneTerrain;
    [SerializeField] private ObjectHeadContent content;
    [SerializeField] private TurnManager sceneTurnManager;
    [SerializeField] private PlayerInventoryManager sceneInventory;
    [SerializeField] private TerrainRandomSpawner sceneSpawner;
    [SerializeField] private CommonHeadItemSpawner sceneItemSpawner;

    private TerrainManager terrain;
    private TurnManager turnManager;
    private Transform mapRoot;
    private ObjectHeadBalanceTable balance;
    private bool built;

    public static int CurrentMatchSeed { get; private set; } = 6974;

    private static void AutoCreateForPlayableScene()
    {
        ObjectHeadNetworkConfig networkConfig = ObjectHeadNetworkConfig.LoadOrCreateRuntimeDefault();
        if (string.Equals(SceneManager.GetActiveScene().name, networkConfig.TitleSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FindAny<TerrainTestBootstrap>() != null || FindAny<ObjectHeadMatchBootstrap>() != null)
        {
            return;
        }

        new GameObject("ObjectHeadMatchBootstrap").AddComponent<ObjectHeadMatchBootstrap>();
    }

    private void Awake()
    {
        BuildMatch();
    }

    public void BuildMatch()
    {
        if (built)
        {
            return;
        }

        built = true;
        balance = ObjectHeadBalanceTable.Load();
        content ??= ObjectHeadContent.Load();
        if (GameStartData.Instance != null) playerCount = GameStartData.Instance.playerCount;

        ResolveSceneAuthoring();

        if (sceneTerrain != null)
        {
            BuildAuthoredMatch();
            return;
        }

        if (cleanupExistingPrototypeScene)
        {
            CleanupExistingPrototypeScene();
        }

        Camera camera = EnsureCamera();
        mapRoot = new GameObject("MapRoot").transform;
        terrain = BuildTerrain(camera);
        AlignTerrainToWaterSurface(terrain);
        BuildBackground(terrain);
        BuildZones(terrain);

        turnManager = new GameObject("TurnManager").AddComponent<TurnManager>();
        PlayerInventoryManager inventoryManager = new GameObject("PlayerInventoryManager").AddComponent<PlayerInventoryManager>();
        inventoryManager.ConfigurePlayers(playerCount);
        TurnCharacterController[] orderedCharacters = SpawnDefaultTeams(terrain);

        TerrainRandomSpawner randomSpawner = new GameObject("TerrainRandomSpawner").AddComponent<TerrainRandomSpawner>();
        int characterSpawnSeed = ResolveCharacterSpawnSeed();
        randomSpawner.Configure(terrain, orderedCharacters, characterSpawnSeed, mapAuthoring);
        randomSpawner.SpawnCharacters();

        BuildCommonHeadSystem(terrain, turnManager);
        turnManager.SetCharacters(orderedCharacters);

        ObjectHeadCameraController cameraController = camera.GetComponent<ObjectHeadCameraController>();
        if (cameraController == null)
        {
            cameraController = camera.gameObject.AddComponent<ObjectHeadCameraController>();
        }
        cameraController.Configure(terrain, turnManager);

        if (FindAny<ObjectHeadHUD>() == null)
        {
            new GameObject("ObjectHeadHUD").AddComponent<ObjectHeadHUD>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (addGamepadDebugOverlay && FindAny<ObjectHeadGamepadDebugOverlay>() == null)
        {
            new GameObject("ObjectHeadGamepadDebugOverlay").AddComponent<ObjectHeadGamepadDebugOverlay>();
        }
#endif
    }

    private void BuildAuthoredMatch()
    {
        terrain = sceneTerrain;
        // The authored Transform is the source of truth. The pixel mask still rebuilds at runtime.
        terrain.SetTerrainOriginWorld(terrain.transform.position);
        if (!terrain.IsInitialized) terrain.InitializeTerrain();
        turnManager = sceneTurnManager;
        sceneInventory.ConfigurePlayers(playerCount);
        TurnCharacterController[] characters = SpawnDefaultTeams(terrain);
        sceneSpawner.Configure(terrain, characters, ResolveCharacterSpawnSeed(), mapAuthoring);
        sceneSpawner.SpawnCharacters();
        sceneItemSpawner.Configure(turnManager, terrain, GameStartData.Instance != null ? GameStartData.Instance.mapSeed : commonHeadSpawnSeed);
        turnManager.SetCharacters(characters, GameStartData.Instance != null ? GameStartData.Instance.startingPlayerIndex : 1);
        Camera.main.GetComponent<ObjectHeadCameraController>().Configure(terrain, turnManager);
    }

    private void ResolveSceneAuthoring()
    {
        if (mapAuthoring == null)
        {
            mapAuthoring = FindAny<ObjectHeadMapAuthoring>();
        }

        if (mapAuthoring == null)
        {
            return;
        }

        if (mapAuthoring.TryGetTerrainOrigin(out Vector2 authoredTerrainOrigin))
        {
            terrainOriginWorld = authoredTerrainOrigin;
        }

        if (mapAuthoring.TryGetWaterSurfaceY(out float authoredWaterSurfaceY))
        {
            waterSurfaceY = authoredWaterSurfaceY;
        }
    }

    private void CleanupExistingPrototypeScene()
    {
        DestroyComponents<TerrainCameraFitter>();
        DestroyComponents<ObjectHeadCameraController>();
        DestroyObjects<TurnManager>();
        DestroyObjects<ObjectHeadHUD>();
        DestroyRootObjects<TerrainManager>();
        DestroyRootObjects<TerrainRandomSpawner>();
        DestroyRootObjects<CommonHeadItemSpawner>();
        DestroyRootObjects<CommonHeadItem>();
        DestroyRootObjects<PlayerInventoryManager>();
        DestroyRootObjects<GroundHazardZone>();
        DestroyNamedRoots("PlayerCube_", "Ground", "MapRoot", "TerrainRoot", "SpawnGroup_", "ItemSpawnRoot", "Background_OceanSky", "BackgroundRenderer", "WaterZone", "DeathZone", "CameraBounds");
    }

    private TerrainManager BuildTerrain(Camera camera)
    {
        Texture2D terrainTexture = Resources.Load<Texture2D>(TerrainResourcePath);
        Texture2D collisionMask = Resources.Load<Texture2D>(CollisionMaskResourcePath);
        if (terrainTexture == null)
        {
            Debug.LogError($"Missing terrain texture at Resources/{TerrainResourcePath}.");
            return null;
        }

        GameObject terrainRoot = new GameObject("TerrainRoot");
        terrainRoot.transform.SetParent(mapRoot, false);
        SpriteRenderer renderer = terrainRoot.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 0;

        GameObject chunkRootObject = new GameObject("TerrainChunkRoot");
        chunkRootObject.transform.SetParent(terrainRoot.transform, false);

        TerrainManager manager = terrainRoot.AddComponent<TerrainManager>();
        manager.Configure(
            terrainTexture,
            collisionMask,
            renderer,
            chunkRootObject.transform,
            terrainOriginWorld,
            pixelsPerUnit,
            chunkSizePx,
            collisionCellSizePx,
            mapWidthMultiplier,
            upperSkyPaddingWorld);

        if (addTerrainEditBrush)
        {
            TerrainEditBrush brush = terrainRoot.AddComponent<TerrainEditBrush>();
            brush.Configure(manager, camera);
        }

        return manager;
    }

    private void AlignTerrainToWaterSurface(TerrainManager manager)
    {
        if (manager == null || !manager.TryGetLowestSolidWorldY(out float lowestSolidWorldY))
        {
            Debug.LogWarning("Terrain water alignment skipped: no solid collision-mask pixel was found.");
            return;
        }

        float targetLowestSolidY = waterSurfaceY + terrainWaterContactOffset;
        float deltaY = targetLowestSolidY - lowestSolidWorldY;
        Vector2 alignedOrigin = manager.TerrainOriginWorld + Vector2.up * deltaY;
        manager.SetTerrainOriginWorld(alignedOrigin);
        terrainOriginWorld = alignedOrigin;

        Debug.Log(
            $"Aligned terrain lowest solid Y {lowestSolidWorldY:0.###} to " +
            $"{targetLowestSolidY:0.###} (water {waterSurfaceY:0.###}, offset {terrainWaterContactOffset:0.###}).");
    }

    private void BuildBackground(TerrainManager manager)
    {
        Sprite background = LoadCenteredSpriteResource(BackgroundResourcePath, pixelsPerUnit);
        if (background == null || manager == null)
        {
            return;
        }

        GameObject backgroundObject = new GameObject("BackgroundRenderer");
        backgroundObject.transform.SetParent(mapRoot, false);
        SpriteRenderer renderer = backgroundObject.AddComponent<SpriteRenderer>();
        renderer.sprite = background;
        renderer.sortingOrder = -20;

        float terrainWidth = manager.WidthPx / (float)manager.PixelsPerUnit;
        float terrainHeight = manager.HeightPx / (float)manager.PixelsPerUnit;
        Vector2 center = manager.TerrainOriginWorld + new Vector2(terrainWidth * 0.5f, terrainHeight * 0.5f);

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float scale = Mathf.Max(terrainWidth / Mathf.Max(0.01f, spriteSize.x), terrainHeight / Mathf.Max(0.01f, spriteSize.y));
        float localWaterlineY = renderer.sprite.bounds.min.y +
            renderer.sprite.bounds.size.y * Mathf.Clamp01(backgroundWaterlineNormalized);
        float alignedBackgroundY = waterSurfaceY - localWaterlineY * scale;
        backgroundObject.transform.position = new Vector3(center.x, alignedBackgroundY, 8f);
        backgroundObject.transform.localScale = Vector3.one * scale;
    }

    private void BuildZones(TerrainManager manager)
    {
        if (manager == null)
        {
            return;
        }

        float width = manager.WidthPx / (float)manager.PixelsPerUnit;
        float centerX = manager.TerrainOriginWorld.x + width * 0.5f;

        GameObject water = new GameObject("WaterZone");
        water.transform.SetParent(mapRoot, false);
        water.transform.position = new Vector2(centerX, waterSurfaceY);
        water.AddComponent<BoxCollider2D>();
        WaterZone waterZone = water.AddComponent<WaterZone>();
        waterZone.ConfigureSurface(waterSurfaceY, width + 6f, 1.5f, 0.03f);

        GameObject death = new GameObject("DeathZone");
        death.transform.SetParent(mapRoot, false);
        death.transform.position = new Vector2(centerX, waterSurfaceY - 4f);
        BoxCollider2D deathCollider = death.AddComponent<BoxCollider2D>();
        deathCollider.isTrigger = true;
        deathCollider.size = new Vector2(width + 12f, 3f);
        death.AddComponent<DeathZone>();

        GameObject cameraBounds = new GameObject("CameraBounds");
        cameraBounds.transform.SetParent(mapRoot, false);
        cameraBounds.transform.position = new Vector3(centerX, manager.TerrainOriginWorld.y + manager.HeightPx / (2f * manager.PixelsPerUnit), 0f);
    }

    private Sprite LoadCenteredSpriteResource(string resourcePath, float ppu)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, ppu));
        }

        return Resources.Load<Sprite>(resourcePath);
    }

    private int ResolveCharacterSpawnSeed()
    {
        string source;
        int seed;
        if (TryGetGameStartDataSeed(out int externalSeed))
        {
            seed = externalSeed;
            source = "GameStartData";
        }
        else if (useFixedTestSpawnSeed)
        {
            seed = fixedTestSpawnSeed != 0 ? fixedTestSpawnSeed : 6974;
            source = "FixedTest";
        }
        else
        {
            int entropy = unchecked(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks);
            seed = new System.Random(entropy).Next(1, int.MaxValue);
            source = "RandomTest";
        }

        CurrentMatchSeed = seed;
        Debug.Log($"Character spawn seed: {seed}\nSource: {source}");
        return seed;
    }

    private static bool TryGetGameStartDataSeed(out int seed)
    {
        seed = 0;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type type = assemblies[assemblyIndex].GetType("GameStartData");
            if (type == null)
            {
                continue;
            }

            object instance = null;
            PropertyInfo instanceProperty = type.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceProperty != null)
            {
                instance = instanceProperty.GetValue(null);
            }

            if (TryReadSeedMember(type, instance, "characterSpawnSeed", out seed) ||
                TryReadSeedMember(type, instance, "CharacterSpawnSeed", out seed))
            {
                return seed != 0;
            }
        }

        return false;
    }

    private static bool TryReadSeedMember(Type type, object instance, string memberName, out int seed)
    {
        seed = 0;
        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;
        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            if (!field.IsStatic && instance == null)
            {
                return false;
            }

            object value = field.GetValue(field.IsStatic ? null : instance);
            return TryConvertSeed(value, out seed);
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            MethodInfo getter = property.GetGetMethod(true);
            if (getter == null || (!getter.IsStatic && instance == null))
            {
                return false;
            }

            object value = property.GetValue(getter != null && getter.IsStatic ? null : instance);
            return TryConvertSeed(value, out seed);
        }

        return false;
    }

    private static bool TryConvertSeed(object value, out int seed)
    {
        seed = 0;
        if (value == null)
        {
            return false;
        }

        try
        {
            seed = Convert.ToInt32(value);
            return seed != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private TurnCharacterController[] SpawnDefaultTeams(TerrainManager manager)
    {
        int charactersPerPlayer = content.CharactersPerPlayer(playerCount);

        List<TurnCharacterController>[] teams = new List<TurnCharacterController>[playerCount];
        for (int player = 0; player < playerCount; player++)
        {
            teams[player] = new List<TurnCharacterController>();

            for (int slot = 0; slot < charactersPerPlayer; slot++)
            {
                ObjectHeadPlayerAssignment assignment = Array.Find(GameStartData.Instance?.players ?? Array.Empty<ObjectHeadPlayerAssignment>(), p => p.playerIndex == player + 1);
                ObjectHeadCharacterKind[] team = assignment != null && content.ValidSelection(assignment.characters, playerCount)
                    ? assignment.characters : content.DefaultSelection(playerCount);
                ObjectHeadCharacterKind kind = team[slot];
                float width = manager != null ? manager.WidthPx / (float)manager.PixelsPerUnit : 10f;
                float height = manager != null ? manager.HeightPx / (float)manager.PixelsPerUnit : 10f;
                Vector2 temporaryPosition = manager != null
                    ? manager.TerrainOriginWorld + new Vector2(width * 0.5f, height + 2f + slot)
                    : new Vector2(player * 2f, 8f + slot);
                teams[player].Add(CreateCharacter(player + 1, slot + 1, kind, temporaryPosition));
            }
        }

        List<TurnCharacterController> ordered = new List<TurnCharacterController>();
        for (int slot = 0; slot < charactersPerPlayer; slot++)
        {
            for (int player = 0; player < playerCount; player++)
            {
                if (slot < teams[player].Count)
                {
                    ordered.Add(teams[player][slot]);
                }
            }
        }

        return ordered.ToArray();
    }

    private void BuildCommonHeadSystem(TerrainManager manager, TurnManager managerForTurns)
    {
        if (manager == null || managerForTurns == null)
        {
            return;
        }

        GameObject spawnerObject = new GameObject("CommonHeadItemSpawner");
        CommonHeadItemSpawner spawner = spawnerObject.AddComponent<CommonHeadItemSpawner>();
        spawner.Configure(managerForTurns, manager, commonHeadSpawnSeed);
    }

    private TurnCharacterController CreateCharacter(int playerIndex, int slotIndex, ObjectHeadCharacterKind kind, Vector2 position)
    {
        ObjectHeadCharacterDefinition definition = content.Character(kind);
        if (definition != null && definition.prefab != null)
        {
            GameObject instance = Instantiate(definition.prefab, position, Quaternion.identity);
            instance.name = $"P{playerIndex}_Slot{slotIndex}_{kind}";
            instance.GetComponent<DemoSkillSelector>().SetCharacterKind(kind);
            instance.GetComponent<ObjectHeadTeamMember>().Configure(playerIndex, slotIndex, kind);
            ApplyStats(kind, instance.GetComponent<CharacterCombat>());
            instance.GetComponent<CharacterVisual>().SetFacingRight(playerIndex % 2 == 1);
            instance.GetComponent<TurnCharacterController>().ConfigureMovement(BalanceFloat("character.move_speed", characterMoveSpeed), BalanceFloat("character.jump_force", characterJumpForce));
            return instance.GetComponent<TurnCharacterController>();
        }
        GameObject character = new GameObject($"P{playerIndex}_Slot{slotIndex}_{kind}");
        character.transform.position = position;

        SpriteRenderer rootRenderer = character.AddComponent<SpriteRenderer>();
        rootRenderer.enabled = false;

        Rigidbody2D body = character.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D collider = character.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.72f, 1.15f);
        collider.offset = new Vector2(0f, 0.02f);

        CharacterVisual visual = character.AddComponent<CharacterVisual>();
        TurnCharacterController controller = character.AddComponent<TurnCharacterController>();
        CharacterCombat combat = character.AddComponent<CharacterCombat>();
        AimController aim = character.AddComponent<AimController>();
        PowerChargeController power = character.AddComponent<PowerChargeController>();
        DemoSkillSelector skill = character.AddComponent<DemoSkillSelector>();
        SkillFireController fire = character.AddComponent<SkillFireController>();
        ObjectHeadTeamMember member = character.AddComponent<ObjectHeadTeamMember>();
        CommonHeadUseController commonHeadUse = character.AddComponent<CommonHeadUseController>();

        skill.SetCharacterKind(kind);
        skill.SetSkillIndex(0);
        member.Configure(playerIndex, slotIndex, kind);
        ApplyStats(kind, combat);

        visual.ConfigureSpriteFacing(characterSpritesFaceRightByDefault);
        visual.SetFacingRight(playerIndex == 1);
        controller.ConfigureMovement(
            BalanceFloat("character.move_speed", characterMoveSpeed),
            BalanceFloat("character.jump_force", characterJumpForce));
        _ = aim;
        _ = power;
        _ = fire;
        _ = commonHeadUse;
        return character.GetComponent<TurnCharacterController>();
    }

    private void ApplyStats(ObjectHeadCharacterKind kind, CharacterCombat combat)
    {
        if (combat == null)
        {
            return;
        }

        var definition=content?.Character(kind);
        if(definition!=null)
        {
            string prefix="character."+kind.ToString().ToLowerInvariant();
            combat.ConfigureStats(BalanceInt(prefix+".max_hp",definition.maxHp),
                BalanceFloat(prefix+".throw_power",definition.throwPower),
                BalanceFloat(prefix+".knockback_resistance",definition.knockbackResistance));
            return;
        }
        switch (kind)
        {
            case ObjectHeadCharacterKind.Bulb:
                combat.ConfigureStats(
                    BalanceInt("character.bulb.max_hp", 85),
                    BalanceFloat("character.bulb.throw_power", 1.15f),
                    BalanceFloat("character.bulb.knockback_resistance", 0.75f));
                break;
            case ObjectHeadCharacterKind.Seed:
                combat.ConfigureStats(
                    BalanceInt("character.seed.max_hp", 120),
                    BalanceFloat("character.seed.throw_power", 0.88f),
                    BalanceFloat("character.seed.knockback_resistance", 1.25f));
                break;
            case ObjectHeadCharacterKind.Bomb:
                combat.ConfigureStats(
                    BalanceInt("character.bomb.max_hp", 100),
                    BalanceFloat("character.bomb.throw_power", 1.1f),
                    BalanceFloat("character.bomb.knockback_resistance", 0.9f));
                break;
        }
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

    private int GetCharactersPerPlayer(int count)
    {
        if (count <= 2) return 3;
        if (count == 3) return 2;
        return 1;
    }

    private Camera EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(75, 190, 235, 255);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private void DestroyNamedRoots(params string[] prefixesOrNames)
    {
        GameObject[] objects = FindAllGameObjects();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate == gameObject || candidate.transform.parent != null)
            {
                continue;
            }

            for (int prefixIndex = 0; prefixIndex < prefixesOrNames.Length; prefixIndex++)
            {
                string prefix = prefixesOrNames[prefixIndex];
                if (candidate.name == prefix || candidate.name.StartsWith(prefix))
                {
                    SafeDestroy(candidate);
                    break;
                }
            }
        }
    }

    private void DestroyObjects<T>() where T : Object
    {
        T[] objects = FindAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            Component component = objects[i] as Component;
            if (component != null && component.gameObject == gameObject)
            {
                continue;
            }

            SafeDestroy(objects[i]);
        }
    }

    private void DestroyRootObjects<T>() where T : Component
    {
        T[] components = FindAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null || components[i].gameObject == gameObject)
            {
                continue;
            }

            Transform root = components[i].transform.root;
            if (root != null && root.gameObject != gameObject)
            {
                SafeDestroy(root.gameObject);
            }
        }
    }

    private void DestroyComponents<T>() where T : Component
    {
        T[] components = FindAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null || components[i].gameObject == gameObject)
            {
                continue;
            }

            SafeDestroy(components[i]);
        }
    }

    private static void SafeDestroy(Object target)
    {
        if (target == null)
        {
            return;
        }

        Object.DestroyImmediate(target);
    }

    private static GameObject[] FindAllGameObjects()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<GameObject>();
#endif
    }

    private static T[] FindAll<T>() where T : Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
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

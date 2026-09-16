using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps build preparation separate from UI authoring.
/// The title hierarchy and RectTransform values live in the scene/prefab so designers can edit them.
/// </summary>
public static class ObjectHeadTitleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ObjectHeadTitle.unity";
    private const string PrefabPath = "Assets/Prefabs/UI/ObjectHeadTitle.prefab";
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Object Head/Prepare Editable Title Scene")]
    public static void Build()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveLegacyRuntimeBootstrap(scene);

        if (FindTitleScreen(scene) == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Title prefab is missing at {PrefabPath}. Restore it before preparing the scene.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = prefab.name;
            Undo.RegisterCreatedObjectUndo(instance, "Add editable title UI");
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        ObjectHeadReleaseAuthoring.Validate();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Object Head] Editable title scene prepared. Existing UI layout was preserved.");
    }

    public static void BuildWindowsDemo()
    {
        // Data can be regenerated from the spreadsheet, but scene/prefab layout is never rebuilt here.
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        ValidateEditableTitleAssets();
        ObjectHeadReleaseAuthoring.Validate();
        AssetDatabase.SaveAssets();

        string outputPath = GetCommandLineValue("-objectHeadBuildPath");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("-objectHeadBuildPath is required.");
        }

        bool incremental = Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, "-objectHeadIncrementalBuild", StringComparison.OrdinalIgnoreCase));
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = incremental
                ? BuildOptions.StrictMode
                : BuildOptions.StrictMode | BuildOptions.CleanBuildCache
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        }

        Debug.Log($"[Object Head] Windows build succeeded: {outputPath} ({report.summary.totalSize} bytes)");
    }

    private static void ValidateEditableTitleAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            throw new InvalidOperationException($"Title scene is missing at {ScenePath}.");
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            throw new InvalidOperationException($"Editable title prefab is missing at {PrefabPath}.");
        }

        Scene titleScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindTitleScreen(titleScene) == null)
        {
            throw new InvalidOperationException(
                "ObjectHeadTitle scene has no serialized ObjectHeadTitleScreen. " +
                "Use Object Head > Prepare Editable Title Scene once before building.");
        }
    }

    private static ObjectHeadTitleScreen FindTitleScreen(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ObjectHeadTitleScreen screen = root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            if (screen != null)
            {
                return screen;
            }
        }

        return null;
    }

    private static void RemoveLegacyRuntimeBootstrap(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "ObjectHeadTitleBootstrap")
            {
                Undo.DestroyObjectImmediate(root);
            }
        }
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameplayScenePath, true)
        }
        .Concat(EditorBuildSettings.scenes.Where(scene =>
            !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene.path, GameplayScenePath, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    }

    private static string GetCommandLineValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }
}

using System;
using System.Linq;
using UnityEditor;

// Run with -batchmode -executeMethod ObjectHeadAIEditorSmokeRunner.Run -objectHeadAISmoke.
public static class ObjectHeadAIEditorSmokeRunner
{
    public static void Run()
    {
        if (!Environment.GetCommandLineArgs().Any(arg=>arg=="-objectHeadAISmoke" || arg=="-objectHeadAIPickupSmoke"))
            throw new ArgumentException("The AI smoke runner requires an AI smoke flag.");
        EditorApplication.isPlaying = true;
    }
}

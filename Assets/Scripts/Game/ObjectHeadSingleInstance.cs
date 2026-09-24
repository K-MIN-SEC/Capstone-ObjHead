using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

internal static class ObjectHeadSingleInstance
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // Keep the handle alive for the player process. Windows releases it even after a crash.
    private static Mutex playerMutex;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string message, string caption, uint type);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void CheckForRunningPlayer()
    {
        // Dedicated/batch workers are not interactive game clients.
        if (Application.isBatchMode)
            return;

        playerMutex = new Mutex(true, @"Local\ObjectHeadBattlePlayer", out bool firstInstance);
        if (firstInstance)
            return;

        playerMutex.Dispose();
        playerMutex = null;
        MessageBox(IntPtr.Zero, "이미 게임이 실행 중입니다.", "오브젝트 헤드 배틀", 0x00000040);
        Environment.Exit(0);
    }
#endif
}

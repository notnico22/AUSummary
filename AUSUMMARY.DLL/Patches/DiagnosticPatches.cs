using HarmonyLib;
using System;
using UnityEngine;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// DIAGNOSTIC PATCH - Tests if Harmony is working
/// </summary>
[HarmonyPatch]
public static class DiagnosticPatches
{
    private static bool _hasLogged = false;

    /// <summary>
    /// This should fire EVERY FRAME if Harmony is working
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    public static void OnPlayerUpdate(PlayerControl __instance)
    {
        if (!_hasLogged && __instance != null && __instance.AmOwner)
        {
            AUSummaryPlugin.Instance.Log.LogWarning("═══════════════════════════════════");
            AUSummaryPlugin.Instance.Log.LogWarning("✅ HARMONY IS WORKING! Patches are being applied!");
            AUSummaryPlugin.Instance.Log.LogWarning("═══════════════════════════════════");
            _hasLogged = true;
        }
    }

    /// <summary>
    /// Test if we can patch MurderPlayer
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    [HarmonyPrefix]
    public static void TestMurderPatch(PlayerControl __instance, PlayerControl target)
    {
        AUSummaryPlugin.Instance.Log.LogWarning($"🔪 DIAGNOSTIC: MURDER PATCH FIRED! {__instance?.Data?.PlayerName} killing {target?.Data?.PlayerName}");
    }

    /// <summary>
    /// Test if we can patch CompleteTask - FIXED SIGNATURE
    /// </summary>
    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    [HarmonyPrefix]
    public static void TestTaskPatch([HarmonyArgument(0)] PlayerControl pc)
    {
        if (pc != null && pc.Data != null)
        {
            AUSummaryPlugin.Instance.Log.LogWarning($"✅ DIAGNOSTIC: TASK PATCH FIRED! {pc.Data.PlayerName} completed a task!");
        }
    }
}

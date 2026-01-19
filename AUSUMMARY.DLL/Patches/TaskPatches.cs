using HarmonyLib;
using System;
using System.Linq;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for task completion
/// </summary>
[HarmonyPatch]
public static class TaskPatches
{
    /// <summary>
    /// Patch for task completion
    /// </summary>
    [HarmonyPatch(typeof(PlayerTask), nameof(PlayerTask.AppendTaskText))]
    [HarmonyPostfix]
    public static void OnTaskCompleted(PlayerTask __instance)
    {
        try
        {
            if (__instance == null || !__instance.IsComplete) return;
            
            var owner = __instance.Owner;
            if (owner == null) return;

            AUSummaryPlugin.Instance.Log.LogInfo($"Task completed by {owner.Data.PlayerName}");
            
            GameTracker.RecordTaskComplete(owner.PlayerId);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnTaskCompleted: {ex.Message}");
        }
    }
}

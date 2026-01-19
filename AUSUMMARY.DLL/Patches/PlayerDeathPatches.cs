using HarmonyLib;
using System;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for player deaths
/// </summary>
[HarmonyPatch]
public static class PlayerDeathPatches
{
    /// <summary>
    /// Patch for player murder - use Prefix to catch it before death
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    [HarmonyPrefix]
    public static void OnPlayerMurdered(PlayerControl __instance, PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null) return;
            if (__instance == null || __instance.Data == null) return;

            var killerName = __instance.Data.PlayerName ?? "Unknown";
            var victimName = target.Data.PlayerName;

            AUSummaryPlugin.Instance.Log.LogInfo($"[MURDER DETECTED] {victimName} killed by {killerName}");
            
            GameTracker.RecordDeath(
                target.PlayerId,
                "Killed",
                killerName
            );
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerMurdered: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch for player exile
    /// </summary>
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    [HarmonyPrefix]
    public static void OnPlayerExiled(ExileController __instance)
    {
        try
        {
            var exiled = __instance.initData?.networkedPlayer;
            if (exiled == null) return;

            var playerName = exiled.PlayerName;

            AUSummaryPlugin.Instance.Log.LogInfo($"[EXILE DETECTED] {playerName} ejected");
            
            GameTracker.RecordDeath(
                exiled.PlayerId,
                "Ejected"
            );
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerExiled: {ex.Message}");
        }
    }
}

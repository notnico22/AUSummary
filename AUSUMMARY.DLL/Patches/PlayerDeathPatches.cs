using HarmonyLib;
using System;
using System.Linq;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for player deaths - MULTIPLE METHODS FOR TOU COMPATIBILITY
/// </summary>
[HarmonyPatch]
public static class PlayerDeathPatches
{
    private static int _killCounter = 0;

    /// <summary>
    /// PRIMARY: Vanilla MurderPlayer patch
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    [HarmonyPrefix]
    public static void OnPlayerMurdered(PlayerControl __instance, PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("Murder detected but target is null!");
                return;
            }
            
            if (__instance == null || __instance.Data == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("Murder detected but killer is null!");
                return;
            }

            var killerName = __instance.Data.PlayerName ?? "Unknown";
            var victimName = target.Data.PlayerName;
            
            var killType = DetermineKillType(__instance, target);

            _killCounter++;
            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [KILL #{_killCounter} - MurderPlayer] {victimName} {killType} by {killerName}");
            
            GameTracker.RecordDeath(
                target.PlayerId,
                "Killed",
                killerName,
                killType
            );
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerMurdered: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// SECONDARY: Checkmate patch (TOU might use this)
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
    [HarmonyPrefix]
    public static void OnCheckMurder(PlayerControl __instance, PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null || __instance == null || __instance.Data == null)
                return;

            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [CheckMurder] {__instance.Data.PlayerName} attempting to kill {target.Data.PlayerName}");
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnCheckMurder: {ex.Message}");
        }
    }

    /// <summary>
    /// TERTIARY: RpcMurderPlayer - network sync
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcMurderPlayer))]
    [HarmonyPrefix]
    public static void OnRpcMurder(PlayerControl __instance, PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null || __instance == null || __instance.Data == null)
                return;

            var killerName = __instance.Data.PlayerName ?? "Unknown";
            var victimName = target.Data.PlayerName;

            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [RpcMurderPlayer] {killerName} RPC killing {victimName}");
            
            // Don't record here if MurderPlayer already fired
            // This is just for logging
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnRpcMurder: {ex.Message}");
        }
    }

    /// <summary>
    /// FOURTH: Die method - when player actually dies
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    [HarmonyPrefix]
    public static void OnPlayerDie(PlayerControl __instance, [HarmonyArgument(0)] DeathReason reason)
    {
        try
        {
            if (__instance == null || __instance.Data == null)
                return;

            AUSummaryPlugin.Instance.Log.LogWarning($"💀 [Die Method] {__instance.Data.PlayerName} died - Reason: {reason}");
            
            // Only record if we haven't already
            // This is a fallback method
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerDie: {ex.Message}");
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
            if (exiled == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("Exile detected but player is null!");
                return;
            }

            var playerName = exiled.PlayerName;

            AUSummaryPlugin.Instance.Log.LogWarning($"[EJECTION] {playerName} was ejected from the ship");
            
            GameTracker.RecordDeath(
                exiled.PlayerId,
                "Ejected",
                null,
                "Ejected"
            );
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerExiled: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Determine the specific kill type based on the killer's role
    /// </summary>
    private static string DetermineKillType(PlayerControl killer, PlayerControl target)
    {
        try
        {
            var role = killer.Data?.Role;
            if (role == null) return "Killed";

            var roleType = role.GetType();
            var roleName = GetRoleName(roleType)?.ToLower() ?? "";

            AUSummaryPlugin.Instance.Log.LogInfo($"Determining kill type for role: {roleName}");

            // Check for specific role kill types
            if (roleName.Contains("soul") && roleName.Contains("collector"))
                return "Reaped";
            
            if (roleName.Contains("bomber"))
                return "Bombed";
            
            if (roleName.Contains("werewolf"))
                return "Mauled";
            
            if (roleName.Contains("arsonist"))
                return "Ignited";
            
            if (roleName.Contains("plaguebearer") || roleName.Contains("pestilence"))
                return "Infected";
            
            if (roleName.Contains("vampire"))
                return "Bitten";
            
            if (roleName.Contains("sheriff") || roleName.Contains("vigilante"))
                return "Shot";
            
            if (roleName.Contains("hunter"))
                return "Hunted";
            
            if (roleName.Contains("glitch"))
                return "Hacked";
            
            if (roleName.Contains("juggernaut"))
                return "Slashed";

            if (roleName.Contains("warlock"))
                return "Cursed";

            if (roleName.Contains("venerer"))
                return "Venerated";

            if (roleName.Contains("puppeteer"))
                return "Controlled";

            if (roleName.Contains("parasite"))
                return "Infected";

            // Check if it's during a meeting (guessed)
            if (MeetingHud.Instance != null)
                return "Guessed";

            // Default kill type
            return "Killed";
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogWarning($"Error determining kill type: {ex.Message}");
            return "Killed";
        }
    }

    private static string? GetRoleName(Type roleType)
    {
        try
        {
            // Try LocaleKey property
            var localeProp = roleType.GetProperty("LocaleKey");
            if (localeProp != null)
            {
                var value = localeProp.GetValue(null);
                if (value != null)
                    return value.ToString();
            }

            // Try RoleName property
            var nameProp = roleType.GetProperty("RoleName");
            if (nameProp != null)
            {
                var value = nameProp.GetValue(null);
                if (value != null)
                    return value.ToString();
            }

            // Fallback to type name
            return roleType.Name;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Patch for Assassin/Guesser kills during meetings
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
    [HarmonyPostfix]
    public static void OnMeetingKill()
    {
        try
        {
            var meeting = MeetingHud.Instance;
            if (meeting == null) return;

            foreach (var playerState in meeting.playerStates)
            {
                if (playerState == null) continue;
                
                var player = playerState.TargetPlayerId;
                var playerData = GameData.Instance?.GetPlayerById(player);
                
                if (playerData != null && playerData.IsDead && !playerData.Disconnected)
                {
                    var playerName = playerData.PlayerName;
                    AUSummaryPlugin.Instance.Log.LogWarning($"[MEETING KILL] {playerName} died during meeting (likely guessed)");
                }
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogWarning($"Error in OnMeetingKill: {ex.Message}");
        }
    }
}

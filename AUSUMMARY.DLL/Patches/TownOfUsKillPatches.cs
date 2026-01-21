using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// COMPREHENSIVE Town of Us kill tracking using reflection to access their RPC system
/// </summary>
[HarmonyPatch]
public static class TownOfUsKillPatches
{
    private static int _killCounter = 0;
    private static readonly Dictionary<byte, KillInfo> _killRegistry = new();
    private static readonly HashSet<byte> _recordedDeaths = new();
    
    private class KillInfo
    {
        public string KillerName { get; set; } = "";
        public string KillType { get; set; } = "Killed";
        public DateTime Time { get; set; }
    }

    public static void ResetTracking()
    {
        _killCounter = 0;
        _killRegistry.Clear();
        _recordedDeaths.Clear();
        AUSummaryPlugin.Instance.Log.LogInfo("Kill tracking reset");
    }

    /// <summary>
    /// Patch TOU's murder methods using reflection
    /// </summary>
    public static void PatchTouMurderMethods(Harmony harmony)
    {
        try
        {
            // Find TownOfUs assembly
            var touAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("TownOfUs"));

            if (touAssembly == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("TownOfUs assembly not found - using fallback tracking only");
                return;
            }

            AUSummaryPlugin.Instance.Log.LogWarning($"✅ Found TOU: {touAssembly.GetName().Name}");

            // Find CustomTouMurderRpcs type
            var murderRpcsType = touAssembly.GetType("TownOfUs.Networking.CustomTouMurderRpcs");
            if (murderRpcsType == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("CustomTouMurderRpcs type not found");
                return;
            }

            // Patch RpcSpecialMurder
            var rpcMethods = murderRpcsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "RpcSpecialMurder")
                .ToList();

            foreach (var method in rpcMethods)
            {
                try
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length >= 3 && 
                        parameters[0].ParameterType.Name == "PlayerControl" &&
                        parameters[1].ParameterType.Name == "PlayerControl" &&
                        parameters.Any(p => p.Name == "causeOfDeath"))
                    {
                        var prefix = typeof(TownOfUsKillPatches).GetMethod(nameof(OnTouSpecialMurderPrefix), 
                            BindingFlags.Public | BindingFlags.Static);
                        
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                        AUSummaryPlugin.Instance.Log.LogWarning("✅ Patched RpcSpecialMurder");
                    }
                }
                catch (Exception ex)
                {
                    AUSummaryPlugin.Instance.Log.LogWarning($"Failed to patch RpcSpecialMurder variant: {ex.Message}");
                }
            }

            // Patch RpcSpecialMultiMurder
            var multiMethods = murderRpcsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "RpcSpecialMultiMurder")
                .ToList();

            foreach (var method in multiMethods)
            {
                try
                {
                    var parameters = method.GetParameters();
                    if (parameters.Any(p => p.ParameterType.Name.Contains("Dictionary")))
                    {
                        var prefix = typeof(TownOfUsKillPatches).GetMethod(nameof(OnTouMultiMurderPrefix),
                            BindingFlags.Public | BindingFlags.Static);
                        
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                        AUSummaryPlugin.Instance.Log.LogWarning("✅ Patched RpcSpecialMultiMurder");
                    }
                }
                catch (Exception ex)
                {
                    AUSummaryPlugin.Instance.Log.LogWarning($"Failed to patch RpcSpecialMultiMurder: {ex.Message}");
                }
            }

            // Patch RpcCustomMurder - used by neutral killers and sheriff!
            try
            {
                MethodInfo? rpcCustomMurder = null;

                // Strategy 1: Search ALL types in TOU assembly for RpcCustomMurder
                var allTouTypes = touAssembly.GetTypes();
                foreach (var type in allTouTypes)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                        .Where(m => m.Name == "RpcCustomMurder" &&
                                   m.GetParameters().Length >= 2 &&
                                   m.GetParameters()[0].ParameterType.Name == "PlayerControl")
                        .ToList();

                    if (methods.Any())
                    {
                        rpcCustomMurder = methods.First();
                        AUSummaryPlugin.Instance.Log.LogWarning($"Found RpcCustomMurder in {type.FullName}");
                        break;
                    }
                }

                // Strategy 2: If not found, search MiraAPI
                if (rpcCustomMurder == null)
                {
                    var miraAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("MiraAPI"));
                    
                    if (miraAssembly != null)
                    {
                        var allMiraTypes = miraAssembly.GetTypes();
                        foreach (var type in allMiraTypes)
                        {
                            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                                .Where(m => m.Name == "RpcCustomMurder" &&
                                           m.GetParameters().Length >= 2 &&
                                           m.GetParameters()[0].ParameterType.Name == "PlayerControl")
                                .ToList();

                            if (methods.Any())
                            {
                                rpcCustomMurder = methods.First();
                                AUSummaryPlugin.Instance.Log.LogWarning($"Found RpcCustomMurder in {type.FullName}");
                                break;
                            }
                        }
                    }
                }

                if (rpcCustomMurder != null)
                {
                    var prefix = typeof(TownOfUsKillPatches).GetMethod(nameof(OnRpcCustomMurderPrefix),
                        BindingFlags.Public | BindingFlags.Static);
                    
                    harmony.Patch(rpcCustomMurder, prefix: new HarmonyMethod(prefix));
                    AUSummaryPlugin.Instance.Log.LogWarning("✅ Patched RpcCustomMurder (neutral killers & sheriff)");
                }
                else
                {
                    AUSummaryPlugin.Instance.Log.LogWarning("RpcCustomMurder method not found in any assembly - will use fallback tracking");
                    
                    // Log all PlayerControl extension methods for debugging
                    AUSummaryPlugin.Instance.Log.LogWarning("Searching for PlayerControl extension methods...");
                    foreach (var type in allTouTypes.Take(20))
                    {
                        var pcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.GetParameters().Length > 0 && 
                                       m.GetParameters()[0].ParameterType.Name == "PlayerControl")
                            .Select(m => $"{type.Name}.{m.Name}")
                            .ToList();
                        
                        if (pcMethods.Any())
                        {
                            AUSummaryPlugin.Instance.Log.LogInfo($"  {type.Name}: {string.Join(", ", pcMethods.Take(3))}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AUSummaryPlugin.Instance.Log.LogWarning($"Failed to patch RpcCustomMurder: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error patching TOU murder methods: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for RpcSpecialMurder - captures killer and causeOfDeath
    /// </summary>
    public static void OnTouSpecialMurderPrefix(
        PlayerControl __0, // source (killer)
        PlayerControl __1, // target (victim)
        object[] __args)    // all arguments
    {
        try
        {
            var source = __0;
            var target = __1;
            
            if (source == null || source.Data == null || target == null || target.Data == null)
                return;

            // Find causeOfDeath in arguments
            string? causeOfDeath = null;
            foreach (var arg in __args)
            {
                if (arg is string str && str != "null")
                {
                    causeOfDeath = str;
                    break;
                }
            }

            // Determine kill type
            string killType = "Killed";
            if (!string.IsNullOrEmpty(causeOfDeath) && causeOfDeath != "null")
            {
                killType = ConvertCauseOfDeathToKillType(causeOfDeath);
            }

            var killInfo = new KillInfo
            {
                KillerName = source.Data.PlayerName,
                KillType = killType,
                Time = DateTime.Now
            };
            
            _killRegistry[target.PlayerId] = killInfo;
            
            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [TOU KILL] {source.Data.PlayerName} killed {target.Data.PlayerName} ({killType})");
            
            // IMPORTANT: Update the death record if it was already recorded  
            GameTracker.UpdateDeathInfo(target.PlayerId, source.Data.PlayerName, killType);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnTouSpecialMurderPrefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for RpcSpecialMultiMurder - captures multi-target kills
    /// </summary>
    public static void OnTouMultiMurderPrefix(
        PlayerControl __0, // source (killer)
        object[] __args)    // all arguments
    {
        try
        {
            var source = __0;
            
            if (source == null || source.Data == null)
                return;

            // Find targets dictionary and causeOfDeath in arguments
            object? targetsDict = null;
            string? causeOfDeath = null;
            
            foreach (var arg in __args)
            {
                if (arg != null)
                {
                    var type = arg.GetType();
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        targetsDict = arg;
                    }
                    else if (arg is string str && str != "null")
                    {
                        causeOfDeath = str;
                    }
                }
            }

            if (targetsDict == null) return;

            // Determine kill type
            string killType = "Killed";
            if (!string.IsNullOrEmpty(causeOfDeath) && causeOfDeath != "null")
            {
                killType = ConvertCauseOfDeathToKillType(causeOfDeath);
            }

            // Extract keys from dictionary using reflection
            var keysProperty = targetsDict.GetType().GetProperty("Keys");
            if (keysProperty != null)
            {
                var keys = keysProperty.GetValue(targetsDict) as System.Collections.IEnumerable;
                if (keys != null)
                {
                    foreach (var key in keys)
                    {
                        if (key is byte targetId)
                        {
                            var killInfo = new KillInfo
                            {
                                KillerName = source.Data.PlayerName,
                                KillType = killType,
                                Time = DateTime.Now
                            };
                            
                            _killRegistry[targetId] = killInfo;
                            
                            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [TOU MULTI-KILL] {source.Data.PlayerName} killed PlayerId {targetId} ({killType})");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnTouMultiMurderPrefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for RpcCustomMurder - used by neutral killers (Soul Collector, etc.)
    /// </summary>
    public static void OnRpcCustomMurderPrefix(PlayerControl __0, PlayerControl __1)
    {
        try
        {
            var source = __0;
            var target = __1;
            
            if (source == null || source.Data == null || target == null || target.Data == null)
                return;

            // Determine kill type from killer's role
            string killType = GetKillTypeFromRole(source);

            var killInfo = new KillInfo
            {
                KillerName = source.Data.PlayerName,
                KillType = killType,
                Time = DateTime.Now
            };
            
            _killRegistry[target.PlayerId] = killInfo;
            
            AUSummaryPlugin.Instance.Log.LogWarning($"🔪 [CUSTOM KILL] {source.Data.PlayerName} killed {target.Data.PlayerName} ({killType})");
            
            // IMPORTANT: Update the death record if it was already recorded
            GameTracker.UpdateDeathInfo(target.PlayerId, source.Data.PlayerName, killType);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnRpcCustomMurderPrefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Fallback: Die method - catches deaths that weren't caught by RPC
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    [HarmonyPrefix]
    public static void OnPlayerDie(PlayerControl __instance, [HarmonyArgument(0)] DeathReason reason)
    {
        try
        {
            if (__instance == null || __instance.Data == null)
                return;

            // Skip if already recorded
            if (_recordedDeaths.Contains(__instance.PlayerId))
                return;
            
            _recordedDeaths.Add(__instance.PlayerId);

            // Try to find kill info from TOU registry first
            string? killerName = null;
            string killType = "Killed";
            
            if (_killRegistry.TryGetValue(__instance.PlayerId, out var killInfo))
            {
                // Found in TOU registry!
                killerName = killInfo.KillerName;
                killType = killInfo.KillType;
            }
            else if (reason == DeathReason.Kill)
            {
                // Fallback: Try to find nearest impostor/neutral killer
                var nearestKiller = FindNearestKiller(__instance);
                if (nearestKiller != null)
                {
                    killerName = nearestKiller.Data.PlayerName;
                    killType = GetKillTypeFromRole(nearestKiller);
                    AUSummaryPlugin.Instance.Log.LogInfo($"Inferred killer from proximity: {killerName}");
                }
            }

            _killCounter++;

            if (killerName != null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning($"💀 [DEATH #{_killCounter}] {__instance.Data.PlayerName} {killType} by {killerName}");
            }
            else
            {
                AUSummaryPlugin.Instance.Log.LogWarning($"💀 [DEATH #{_killCounter}] {__instance.Data.PlayerName} died ({reason})");
            }
            
            // Record the death
            GameTracker.RecordDeath(
                __instance.PlayerId,
                "Killed",
                killerName,
                killType
            );
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnPlayerDie: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert TOU's causeOfDeath string to readable kill type
    /// </summary>
    private static string ConvertCauseOfDeathToKillType(string causeOfDeath)
    {
        var lower = causeOfDeath.ToLower();
        
        // Role-specific ability kills
        if (lower.Contains("warlock")) return "Cursed";
        if (lower.Contains("bomber")) return "Bombed";
        if (lower.Contains("arsonist")) return "Ignited";
        if (lower.Contains("werewolf")) return "Mauled";
        if (lower.Contains("vampire")) return "Bitten";
        if (lower.Contains("sheriff") || lower.Contains("vigilante")) return "Shot";
        if (lower.Contains("hunter")) return "Hunted";
        if (lower.Contains("glitch")) return "Hacked";
        if (lower.Contains("juggernaut")) return "Slashed";
        if (lower.Contains("venerer")) return "Venerated";
        if (lower.Contains("puppeteer")) return "Controlled";
        if (lower.Contains("parasite")) return "Infected";
        if (lower.Contains("soul") && lower.Contains("collector")) return "Reaped";
        if (lower.Contains("pestilence") || lower.Contains("plaguebearer")) return "Infected";
        if (lower.Contains("assassin") || lower.Contains("guess")) return "Guessed";
        if (lower.Contains("prosecute")) return "Prosecuted";
        if (lower.Contains("inquisitor")) return "Vanquished";
        if (lower.Contains("mercenary")) return "Executed";
        if (lower.Contains("chef")) return "Poisoned";
        if (lower.Contains("altruist")) return "Sacrificed";
        if (lower.Contains("oracle")) return "Confessed";
        if (lower.Contains("doomsayer")) return "Observed";
        
        return "Killed";
    }

    /// <summary>
    /// Get kill type from role name - FOR NEUTRAL KILLERS using RpcCustomMurder
    /// </summary>
    private static string GetKillTypeFromRole(PlayerControl player)
    {
        try
        {
            var role = player.Data?.Role;
            if (role == null) return "Killed";
            
            var roleType = role.GetType();
            var roleName = roleType.Name.ToLower();
            
            // Neutral killers using RpcCustomMurder
            if (roleName.Contains("soulcollector")) return "Reaped";
            if (roleName.Contains("werewolf")) return "Mauled";
            if (roleName.Contains("juggernaut")) return "Slashed";
            if (roleName.Contains("glitch")) return "Hacked";
            if (roleName.Contains("vampire")) return "Bitten";
            if (roleName.Contains("arsonist")) return "Ignited";
            if (roleName.Contains("pestilence")) return "Infected";
            if (roleName.Contains("plaguebearer")) return "Infected";
            if (roleName.Contains("inquisitor")) return "Vanquished";
            
            // Crewmate killers
            if (roleName.Contains("sheriff")) return "Shot";
            if (roleName.Contains("vigilante")) return "Shot";
            if (roleName.Contains("hunter")) return "Hunted";
            if (roleName.Contains("veteran")) return "Defended";
            
            return "Killed";
        }
        catch
        {
            return "Killed";
        }
    }

    /// <summary>
    /// Find nearest potential killer (impostor or neutral killer)
    /// </summary>
    private static PlayerControl? FindNearestKiller(PlayerControl victim)
    {
        try
        {
            PlayerControl? nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                if (player.PlayerId == victim.PlayerId) continue;
                if (player.Data.IsDead) continue;
                
                // Check if impostor OR neutral killer
                bool isKiller = player.Data.Role.IsImpostor || IsNeutralKiller(player);
                if (!isKiller) continue;

                var distance = UnityEngine.Vector2.Distance(victim.GetTruePosition(), player.GetTruePosition());
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = player;
                }
            }

            // Only consider if within reasonable kill range (5 units)
            if (nearestDistance < 5f)
            {
                return nearest;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if player is a neutral killer
    /// </summary>
    private static bool IsNeutralKiller(PlayerControl player)
    {
        try
        {
            var role = player.Data?.Role;
            if (role == null) return false;
            
            var roleName = role.GetType().Name.ToLower();
            
            return roleName.Contains("juggernaut") ||
                   roleName.Contains("glitch") ||
                   roleName.Contains("werewolf") ||
                   roleName.Contains("pestilence") ||
                   roleName.Contains("arsonist") ||
                   roleName.Contains("soulcollector") ||
                   roleName.Contains("vampire") ||
                   roleName.Contains("inquisitor");
        }
        catch
        {
            return false;
        }
    }

    public static void PatchTownOfUsKills(Harmony _)
    {
        // This method is kept for compatibility but patching is done in PatchTouMurderMethods
        AUSummaryPlugin.Instance.Log.LogInfo("TOU kill patches will be applied via reflection");
    }
}

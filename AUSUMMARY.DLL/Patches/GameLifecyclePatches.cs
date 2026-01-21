using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Reactor.Utilities;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for game lifecycle (start/end)
/// </summary>
[HarmonyPatch]
public static class GameLifecyclePatches
{
    /// <summary>
    /// Patch for game start - IntroCutscene is more reliable
    /// </summary>
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
    [HarmonyPostfix]
    public static void OnGameStartCrewmate()
    {
        OnGameStart();
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
    [HarmonyPostfix]
    public static void OnGameStartImpostor()
    {
        OnGameStart();
    }

    private static bool _hasStarted = false;

    private static void OnGameStart()
    {
        if (_hasStarted) return;
        _hasStarted = true;

        try
        {
            AUSummaryPlugin.Instance.Log.LogInfo("Game started - beginning tracking");
            
            // Reset tracking systems
            TaskPatches.ResetTracking();
            TownOfUsKillPatches.ResetTracking();
            
            GameTracker.StartGame();

            // Capture player data with delay for roles to be assigned
            Reactor.Utilities.Coroutines.Start(CoDelayedCapture());
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameStart: {ex.Message}");
        }
    }

    private static IEnumerator CoDelayedCapture()
    {
        // Wait for roles to be assigned
        yield return new WaitForSeconds(2f);

        try
        {
            AUSummaryPlugin.Instance.Log.LogInfo("Capturing player roles and modifiers...");
            
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;

                var roleName = GetRoleName(player);
                var team = GetPlayerTeam(player);
                var modifiers = GetPlayerModifiers(player);

                GameTracker.CapturePlayerData(player.Data, roleName, team, modifiers);
            }

            AUSummaryPlugin.Instance.Log.LogInfo($"Captured {PlayerControl.AllPlayerControls.Count} players with roles and modifiers");
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in delayed player capture: {ex.Message}");
        }
    }

    private static string GetRoleName(PlayerControl player)
    {
        try
        {
            var role = player.Data?.Role;
            if (role == null) return "Unknown";
            
            var roleType = role.GetType();
            
            // Try LocaleKey property first
            try
            {
                var prop = roleType.GetProperty("LocaleKey");
                if (prop != null)
                {
                    var value = prop.GetValue(role);
                    if (value != null)
                    {
                        string str = value.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(str) && str.Length > 0 && !char.IsDigit(str[0]))
                        {
                            return str;
                        }
                    }
                }
            }
            catch { }
            
            // Try RoleName property
            try
            {
                var prop = roleType.GetProperty("RoleName");
                if (prop != null)
                {
                    var value = prop.GetValue(role);
                    if (value != null)
                    {
                        string str = value.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(str) && 
                            !str.Contains("Il2Cpp") && 
                            !str.Contains("System"))
                        {
                            return str;
                        }
                    }
                }
            }
            catch { }

            // Fallback to vanilla
            if (role.IsImpostor) return "Impostor";
            return "Crewmate";
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error getting role name: {ex.Message}");
            return "Crewmate";
        }
    }

    private static string GetPlayerTeam(PlayerControl player)
    {
        try
        {
            var role = player.Data?.Role;
            if (role == null) return "Crewmate";
            
            var roleType = role.GetType();
            var roleName = GetRoleName(player).ToLower();

            // Check for neutral roles
            if (roleName.Contains("jester") || roleName.Contains("arsonist") ||
                roleName.Contains("glitch") || roleName.Contains("executioner") ||
                roleName.Contains("plaguebearer") || roleName.Contains("pestilence") ||
                roleName.Contains("werewolf") || roleName.Contains("juggernaut") ||
                roleName.Contains("amnesiac") || roleName.Contains("vampire") ||
                roleName.Contains("doomsayer") || roleName.Contains("survivor") ||
                roleName.Contains("mercenary") || roleName.Contains("inquisitor") ||
                roleName.Contains("fairy") || roleName.Contains("chef") ||
                roleName.Contains("spectre") || roleName.Contains("soul"))
            {
                return "Neutral";
            }

            // Check vanilla impostor
            if (role.IsImpostor) return "Impostor";
            
            return "Crewmate";
        }
        catch
        {
            return "Crewmate";
        }
    }

    private static List<string> GetPlayerModifiers(PlayerControl player)
    {
        var modifiers = new List<string>();

        try
        {
            // Try to get modifier component using MiraAPI
            var modifierComponentType = Type.GetType("MiraAPI.Modifiers.ModifierComponent, MiraAPI");
            if (modifierComponentType == null)
            {
                return modifiers;
            }

            var getComponentMethod = typeof(PlayerControl).GetMethod("GetComponent");
            if (getComponentMethod == null) return modifiers;

            var genericMethod = getComponentMethod.MakeGenericMethod(modifierComponentType);
            var modifierComponent = genericMethod.Invoke(player, null);
            
            if (modifierComponent == null) return modifiers;

            // Get all modifiers from the component
            var modifiersProperty = modifierComponentType.GetProperty("AllModifiers");
            if (modifiersProperty != null)
            {
                var allModifiers = modifiersProperty.GetValue(modifierComponent);
                if (allModifiers != null)
                {
                    var enumerable = allModifiers as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var modifier in enumerable)
                        {
                            if (modifier == null) continue;
                            
                            var modifierType = modifier.GetType();
                            
                            // Try to get modifier name
                            var nameProperty = modifierType.GetProperty("ModifierName");
                            if (nameProperty != null)
                            {
                                var name = nameProperty.GetValue(modifier)?.ToString();
                                if (!string.IsNullOrEmpty(name) && !modifiers.Contains(name))
                                {
                                    modifiers.Add(name);
                                    AUSummaryPlugin.Instance.Log.LogInfo($"Found modifier: {name} on {player.Data.PlayerName}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogWarning($"Error getting modifiers for {player.Data.PlayerName}: {ex.Message}");
        }

        return modifiers;
    }

    /// <summary>
    /// Patch for game end - SMART DEATH INFERENCE + NEUTRAL WIN DETECTION
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPrefix]
    public static void OnGameEndPrefix(EndGameResult endGameResult)
    {
        try
        {
            var reason = endGameResult.GameOverReason;
            
            // PRE-CHECK: Check for neutral winners BEFORE vanilla processing
            var neutralWinner = CheckForNeutralWinner();
            if (neutralWinner != null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning($"🎉 NEUTRAL WIN DETECTED in Prefix!");
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameEndPrefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch for game end - SMART DEATH INFERENCE + NEUTRAL WIN DETECTION
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void OnGameEnd(EndGameResult endGameResult)
    {
        try
        {
            var reason = endGameResult.GameOverReason;
            AUSummaryPlugin.Instance.Log.LogInfo($"Game ended with reason: {reason}");
            
            var winningTeam = GetWinningTeam(reason);
            var winCondition = GetWinCondition(reason);

            // SMART DEATH INFERENCE: If game ended by kills, mark losers as dead
            if (reason == GameOverReason.ImpostorsByKill)
            {
                // Impostors won by killing everyone - all non-impostors should be dead
                AUSummaryPlugin.Instance.Log.LogInfo("Impostor kill win - marking all non-impostors as dead");
                GameTracker.MarkLosingTeamDead("Impostor");
            }
            else if (reason == GameOverReason.CrewmatesByVote)
            {
                // Crewmates won by voting - all impostors should be dead
                AUSummaryPlugin.Instance.Log.LogInfo("Crewmate vote win - marking all impostors as dead");
                GameTracker.MarkLosingTeamDead("Crewmate");
            }
            
            // Also try to read GameData (might have additional info)
            if (GameData.Instance != null)
            {
                foreach (var playerInfo in GameData.Instance.AllPlayers)
                {
                    if (playerInfo == null) continue;
                    
                    var playerId = playerInfo.PlayerId;
                    var isDead = playerInfo.IsDead || playerInfo.Disconnected;
                    
                    if (isDead)
                    {
                        GameTracker.UpdatePlayerAliveStatus(playerId, false);
                        AUSummaryPlugin.Instance.Log.LogInfo($"GameData says {playerInfo.PlayerName} is dead");
                    }
                }
            }

            GameTracker.EndGame(winningTeam, winCondition);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameEnd: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _hasStarted = false;
        }
    }

    private static string GetWinningTeam(GameOverReason reason)
    {
        // Check for neutral winners FIRST (TOU custom win condition)
        try
        {
            var touWinner = CheckForNeutralWinner();
            if (touWinner != null)
            {
                return touWinner;
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogWarning($"Error checking neutral winner: {ex.Message}");
        }

        return reason switch
        {
            GameOverReason.CrewmatesByVote => "Crewmate",
            GameOverReason.CrewmatesByTask => "Crewmate",
            GameOverReason.ImpostorDisconnect => "Crewmate",
            GameOverReason.HideAndSeek_CrewmatesByTimer => "Crewmate",
            GameOverReason.ImpostorsByKill => "Impostor",
            GameOverReason.ImpostorsBySabotage => "Impostor",
            GameOverReason.ImpostorsByVote => "Impostor",
            GameOverReason.CrewmateDisconnect => "Impostor",
            GameOverReason.HideAndSeek_ImpostorsByKills => "Impostor",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Check if a neutral role won - Enhanced Detection
    /// </summary>
    private static string? CheckForNeutralWinner()
    {
        try
        {
            // Method 1: Check TOU WinConditionRegistry
            var registryType = Type.GetType("TownOfUs.Patches.WinConditionRegistry, TownOfUsMira");
            if (registryType != null)
            {
                var conditionsProperty = registryType.GetProperty("RegisteredConditions");
                if (conditionsProperty != null)
                {
                    var conditions = conditionsProperty.GetValue(null) as System.Collections.IEnumerable;
                    if (conditions != null)
                    {
                        foreach (var condition in conditions)
                        {
                            if (condition == null) continue;
                            
                            var conditionType = condition.GetType();
                            
                            // Check if this is NeutralRoleWinCondition
                            if (conditionType.Name.Contains("Neutral"))
                            {
                                AUSummaryPlugin.Instance.Log.LogInfo("Found NeutralRoleWinCondition - checking...");
                                
                                // Try to see if it was met
                                var isMetMethod = conditionType.GetMethod("IsMet");
                                if (isMetMethod != null)
                                {
                                    try
                                    {
                                        var result = isMetMethod.Invoke(condition, new object[] { ShipStatus.Instance });
                                        if (result is bool met && met)
                                        {
                                            AUSummaryPlugin.Instance.Log.LogWarning("Neutral win condition detected via WinConditionRegistry!");
                                            return "Neutral";
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }

            // Method 2: Check individual neutral roles for WinConditionMet()
            var touRoleType = Type.GetType("TownOfUs.Roles.ITownOfUsRole, TownOfUsMira");
            if (touRoleType == null) return null;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Role == null)
                    continue;

                var role = player.Data.Role;
                
                // Check if role is a TOU role
                if (!touRoleType.IsAssignableFrom(role.GetType()))
                    continue;

                // Try to call WinConditionMet()
                var winMethod = role.GetType().GetMethod("WinConditionMet");
                if (winMethod == null) continue;

                try
                {
                    var result = winMethod.Invoke(role, null);
                    if (result is bool won && won)
                    {
                        // This neutral role won!
                        var roleName = GetRoleName(player);
                        AUSummaryPlugin.Instance.Log.LogWarning($"🎉 Neutral winner detected: {player.Data.PlayerName} ({roleName})");
                        return "Neutral";
                    }
                }
                catch (Exception ex)
                {
                    AUSummaryPlugin.Instance.Log.LogWarning($"Error checking win condition for {player.Data.PlayerName}: {ex.Message}");
                }
            }

            // Method 3: Check for specific GameOverReason values TOU might use
            // TOU uses CustomGameOver which might show up in logs
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in CheckForNeutralWinner: {ex.Message}");
        }

        return null;
    }

    private static string GetWinCondition(GameOverReason reason)
    {
        return reason switch
        {
            GameOverReason.CrewmatesByVote => "Voted Out All Impostors",
            GameOverReason.CrewmatesByTask => "Completed All Tasks",
            GameOverReason.ImpostorDisconnect => "Impostor Disconnect",
            GameOverReason.HideAndSeek_CrewmatesByTimer => "Survived Timer",
            GameOverReason.ImpostorsByKill => "Kill All Crewmates",
            GameOverReason.ImpostorsBySabotage => "Sabotage Win",
            GameOverReason.ImpostorsByVote => "Voting Majority",
            GameOverReason.CrewmateDisconnect => "Crewmate Disconnect",
            GameOverReason.HideAndSeek_ImpostorsByKills => "All Crewmates Killed",
            _ => "Unknown"
        };
    }
}

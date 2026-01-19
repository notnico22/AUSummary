using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using System.Collections;
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
            GameTracker.StartGame();

            // Capture player data now
            Reactor.Utilities.Coroutines.Start(CoDelayedCapture());
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameStart: {ex.Message}");
        }
    }

    private static IEnumerator CoDelayedCapture()
    {
        yield return new WaitForSeconds(2f);

        try
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;

                var roleName = GetRoleName(player);
                var team = GetPlayerTeam(player);

                GameTracker.CapturePlayerData(player.Data, roleName, team);
            }

            AUSummaryPlugin.Instance.Log.LogInfo($"Captured {PlayerControl.AllPlayerControls.Count} player roles");
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
            
            try
            {
                var prop = roleType.GetProperty("LocaleKey");
                if (prop != null)
                {
                    var value = prop.GetValue(role);
                    if (value != null)
                    {
                        string str = value.ToString().Trim();
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
                        string str = value.ToString().Trim();
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

    /// <summary>
    /// Patch for game end
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

            GameTracker.EndGame(winningTeam, winCondition);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameEnd: {ex.Message}");
        }
        finally
        {
            _hasStarted = false; // Reset for next game
        }
    }

    private static string GetWinningTeam(GameOverReason reason)
    {
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

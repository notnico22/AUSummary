using HarmonyLib;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for task completion - FIXED for multi-part tasks
/// </summary>
[HarmonyPatch]
public static class TaskPatches
{
    private static int _taskCounter = 0;
    private static readonly HashSet<uint> _completedTasks = new();

    /// <summary>
    /// Reset tracked tasks when game starts
    /// </summary>
    public static void ResetTracking()
    {
        _taskCounter = 0;
        _completedTasks.Clear();
        AUSummaryPlugin.Instance.Log.LogInfo("Task tracking reset");
    }

    /// <summary>
    /// Patch for when a task is marked as complete
    /// CRITICAL: GameData.CompleteTask is only called when a FULL task is complete
    /// This is called by the game AFTER all parts are done, so we don't need to check IsComplete
    /// </summary>
    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    [HarmonyPrefix]
    public static void OnGameDataTaskComplete(GameData __instance, [HarmonyArgument(0)] PlayerControl pc, [HarmonyArgument(1)] uint taskId)
    {
        try
        {
            if (pc == null || pc.Data == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning("Task completed but PlayerControl is null!");
                return;
            }

            // IMPORTANT: Check if player is impostor or dead - they don't do real tasks
            if (pc.Data.Role != null && pc.Data.Role.IsImpostor)
            {
                AUSummaryPlugin.Instance.Log.LogInfo($"[TASK IGNORED] {pc.Data.PlayerName} is impostor (fake task)");
                return;
            }

            if (pc.Data.IsDead)
            {
                AUSummaryPlugin.Instance.Log.LogInfo($"[TASK IGNORED] {pc.Data.PlayerName} is dead");
                return;
            }

            // CRITICAL FIX: Check if we've already counted this task FIRST
            // This prevents double-counting if CompleteTask is called multiple times
            if (_completedTasks.Contains(taskId))
            {
                AUSummaryPlugin.Instance.Log.LogInfo($"[TASK ALREADY COUNTED] Task ID {taskId} for {pc.Data.PlayerName} already recorded");
                return;
            }

            // CRITICAL INSIGHT: GameData.CompleteTask is ONLY called when a task is FULLY complete
            // The game handles multi-part tasks internally and only calls this when ALL parts are done
            // So we can trust this call and don't need to check IsComplete!
            
            // Get task name for logging (optional, might fail)
            string taskName = "Unknown Task";
            try
            {
                if (pc.myTasks != null)
                {
                    foreach (var task in pc.myTasks)
                    {
                        if (task != null && task.Id == taskId)
                        {
                            taskName = task.TaskType.ToString();
                            break;
                        }
                    }
                }
            }
            catch { }

            // Task is complete - count it!
            _completedTasks.Add(taskId);
            _taskCounter++;
            
            AUSummaryPlugin.Instance.Log.LogWarning($"✅ TASK COMPLETE #{_taskCounter}: {pc.Data.PlayerName} finished {taskName} (Task ID: {taskId})");
            GameTracker.RecordTaskComplete(pc.PlayerId);
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnGameDataTaskComplete: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Alternative patch that checks task completion at the source
    /// This catches when PlayerTask.Complete is called directly
    /// </summary>
    [HarmonyPatch(typeof(PlayerTask), nameof(PlayerTask.Complete))]
    [HarmonyPostfix]
    public static void OnTaskCompletePostfix(PlayerTask __instance)
    {
        try
        {
            if (__instance == null) return;
            
            var owner = __instance.Owner;
            if (owner == null || owner.Data == null) return;

            // IMPORTANT: Check if player is impostor or dead
            if (owner.Data.Role != null && owner.Data.Role.IsImpostor)
            {
                return; // Impostor fake task
            }

            if (owner.Data.IsDead)
            {
                return; // Dead players don't count
            }

            // Only count when task becomes complete AND hasn't been counted yet
            if (__instance.IsComplete && !_completedTasks.Contains(__instance.Id))
            {
                _completedTasks.Add(__instance.Id);
                _taskCounter++;
                
                AUSummaryPlugin.Instance.Log.LogWarning($"✅ TASK COMPLETE (PlayerTask patch) #{_taskCounter}: {owner.Data.PlayerName} finished {__instance.TaskType}");
                GameTracker.RecordTaskComplete(owner.PlayerId);
            }
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnTaskCompletePostfix: {ex.Message}");
        }
    }
}

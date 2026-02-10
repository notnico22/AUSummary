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
    /// Patch for when a task is marked as complete - FIXED for multi-part + Better logging
    /// CRITICAL: Must use Postfix so task is actually complete when we check it
    /// </summary>
    [HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
    [HarmonyPostfix]
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

            // Check if this is a multi-part task - manually iterate Il2Cpp list
            PlayerTask? foundTask = null;
            if (pc.myTasks != null)
            {
                foreach (var task in pc.myTasks)
                {
                    if (task != null && task.Id == taskId)
                    {
                        foundTask = task;
                        break;
                    }
                }
            }

            if (foundTask == null)
            {
                AUSummaryPlugin.Instance.Log.LogWarning($"Task {taskId} not found for {pc.Data.PlayerName}");
                return;
            }

            // CRITICAL FIX: Check if we've already counted this task FIRST
            // This prevents double-counting if CompleteTask is called multiple times
            if (_completedTasks.Contains(taskId))
            {
                AUSummaryPlugin.Instance.Log.LogInfo($"[TASK ALREADY COUNTED] {pc.Data.PlayerName}'s {foundTask.TaskType} already recorded");
                return;
            }

            // CRITICAL: Now that we're in Postfix, the task SHOULD be complete
            // But check just to be safe
            if (!foundTask.IsComplete)
            {
                AUSummaryPlugin.Instance.Log.LogInfo($"[TASK PART] {pc.Data.PlayerName} completed part of {foundTask.TaskType} (not fully complete yet)");
                return;
            }

            // Task is complete and not counted yet - count it!
            _completedTasks.Add(taskId);
            _taskCounter++;
            
            AUSummaryPlugin.Instance.Log.LogWarning($"✅ TASK COMPLETE #{_taskCounter}: {pc.Data.PlayerName} finished {foundTask.TaskType} (Task ID: {taskId})");
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

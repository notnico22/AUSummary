using HarmonyLib;
using System;

namespace AUSUMMARY.DLL.Patches;

/// <summary>
/// Patches for meetings
/// </summary>
[HarmonyPatch]
public static class MeetingPatches
{
    private static byte _lastReporter = byte.MaxValue;

    /// <summary>
    /// Capture who called the meeting
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
    [HarmonyPrefix]
    public static void OnReportDeadBody(PlayerControl __instance)
    {
        _lastReporter = __instance.PlayerId;
    }

    /// <summary>
    /// Patch for meeting start
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void OnMeetingStart()
    {
        try
        {
            var callerName = "Unknown";
            var isEmergency = true;

            if (_lastReporter != byte.MaxValue)
            {
                var reporter = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p.PlayerId == _lastReporter);
                
                if (reporter != null)
                {
                    callerName = reporter.Data.PlayerName;

                    isEmergency = false; 
                }
            }

            AUSummaryPlugin.Instance.Log.LogInfo($"Meeting started - Caller: {callerName}");
            
            GameTracker.RecordMeeting(isEmergency, callerName);
            
            _lastReporter = byte.MaxValue; // Reset
        }
        catch (Exception ex)
        {
            AUSummaryPlugin.Instance.Log.LogError($"Error in OnMeetingStart: {ex.Message}");
        }
    }
}

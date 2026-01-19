using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI;
using MiraAPI.PluginLoading;

namespace AUSUMMARY.DLL;

/// <summary>
/// Main plugin class for AUSUMMARY game logging system
/// </summary>
[BepInAutoPlugin("ausummary.mod", "AUSUMMARY - Game Logger")]
[BepInProcess("Among Us.exe")]
[BepInDependency(MiraApiPlugin.Id)]
public partial class AUSummaryPlugin : BasePlugin, IMiraPlugin
{
    public static AUSummaryPlugin Instance { get; private set; } = null!;
    
    public Harmony Harmony { get; } = new(Id);

    public string OptionsTitleText => "AUSUMMARY";

    public ConfigFile GetConfigFile() => Config;

    /// <summary>
    /// Called when the plugin is loaded
    /// </summary>
    public override void Load()
    {
        Instance = this;
        
        Log.LogInfo("AUSUMMARY Game Logger initializing...");
        
        // Initialize the game tracker
        GameTracker.Initialize(Log);
        
        // Apply Harmony patches
        Harmony.PatchAll();
        
        Log.LogInfo($"AUSUMMARY v{Shared.AUSummaryConstants.Version} loaded successfully!");
        Log.LogInfo($"Game summaries will be saved to: {Shared.AUSummaryConstants.GetSummariesPath()}");
    }
}

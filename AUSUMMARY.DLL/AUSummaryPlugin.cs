using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.PluginLoading;
using AUSUMMARY.Shared;

namespace AUSUMMARY.DLL;

/// <summary>
/// Main plugin class for AUSUMMARY game logging system
/// </summary>
[BepInAutoPlugin("ausummary.mod", "AUSUMMARY - Game Logger")]
[BepInProcess("Among Us.exe")]
[BepInDependency(MiraAPI.MiraApiPlugin.Id)]
public partial class AUSummaryPlugin : BasePlugin, IMiraPlugin
{
    public static AUSummaryPlugin Instance { get; private set; } = null!;
    
    /// <summary>
    /// Enable or disable debug logging. Set to false to reduce log spam.
    /// </summary>
    public static bool EnableDebugLogging { get; set; } = false;
    
    public Harmony Harmony { get; } = new(Id);

    public string OptionsTitleText => "AUSUMMARY";

    public ConfigFile GetConfigFile() => Config;
    
    // Configuration options
    private ConfigEntry<bool> _checkForUpdates = null!;
    private ConfigEntry<bool> _sendStatsToVercel = null!;

    /// <summary>
    /// Called when the plugin is loaded
    /// </summary>
    public override void Load()
    {
        Instance = this;
        
        Log.LogInfo("AUSUMMARY Game Logger initializing...");
        
        // Setup configuration
        _checkForUpdates = Config.Bind("General", "CheckForUpdates", true, 
            "Check for mod updates on startup");
        _sendStatsToVercel = Config.Bind("General", "SendAnonymousStats", false, 
            "Send anonymous game statistics to global dashboard (helps improve the mod!)");
        
        // Initialize the game tracker
        GameTracker.Initialize(Log);
        
        // Apply Harmony patches for vanilla methods
        Harmony.PatchAll();
        
        Log.LogInfo($"AUSUMMARY v{Shared.AUSummaryConstants.Version} loaded successfully!");
        Log.LogInfo($"Game summaries will be saved to: {Shared.AUSummaryConstants.GetSummariesPath()}");
        
        // Patch TOU methods using reflection (after a delay to ensure TOU is loaded)
        try
        {
            Log.LogWarning("Attempting to patch Town of Us methods...");
            Patches.TownOfUsKillPatches.PatchTouMurderMethods(Harmony);
        }
        catch (Exception ex)
        {
            Log.LogError($"Error patching TOU methods: {ex.Message}");
        }
        
        // Check for updates asynchronously
        if (_checkForUpdates.Value)
        {
            CheckForUpdates();
        }
    }
    
    private async void CheckForUpdates()
    {
        try
        {
            Log.LogInfo("Checking for updates...");
            var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
            
            if (updateInfo.UpdateAvailable)
            {
                Log.LogWarning("╔═══════════════════════════════════════════════════════╗");
                Log.LogWarning("║           NEW VERSION AVAILABLE!                     ║");
                Log.LogWarning($"║  Current: v{updateInfo.CurrentVersion} New: v{updateInfo.LatestVersion} ║");
                Log.LogWarning($"║  Download: {updateInfo.DownloadUrl}");
                Log.LogWarning("║                                                       ║");
                Log.LogWarning("║  Please update to get the latest features & fixes!  ║");
                Log.LogWarning("╚═══════════════════════════════════════════════════════╝");
                
                // Show in-game notification
                ShowUpdateNotification(updateInfo);
            }
            else
            {
                Log.LogInfo($"You're on the latest version (v{updateInfo.CurrentVersion})!");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Could not check for updates: {ex.Message}");
        }
    }
    
    private void ShowUpdateNotification(UpdateChecker.UpdateInfo updateInfo)
    {
        try
        {
            // This will show when the player joins a lobby
            AddDelayedAction(() =>
            {
                if (DestroyableSingleton<HudManager>.Instance != null && DestroyableSingleton<HudManager>.Instance.Notifier != null)
                {
                    // Use the correct API for adding notifications
                    var notification = $"AUSUMMARY v{updateInfo.LatestVersion} is available! Please update.";
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(notification);
                }
            }, 5f);
        }
        catch
        {
            // Silently fail if we can't show the notification
        }
    }
    
    private async void AddDelayedAction(Action action, float delay)
    {
        await Task.Delay((int)(delay * 1000));
        action?.Invoke();
    }
}

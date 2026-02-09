using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.PluginLoading;
using AUSUMMARY.Shared;
using UnityEngine;

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
        _sendStatsToVercel = Config.Bind("General", "SendAnonymousStats", true, 
            "Send anonymous game statistics to global dashboard (helps improve the mod!)");
        
        // Initialize the game tracker
        GameTracker.Initialize(Log);
        
        // Apply Harmony patches for vanilla methods
        Harmony.PatchAll();
        
        Log.LogInfo($"AUSUMMARY v{Shared.AUSummaryConstants.Version} loaded successfully!");
        Log.LogInfo($"Game summaries will be saved to: {Shared.AUSummaryConstants.GetSummariesPath()}");
        
        // Get or create user ID
        if (_sendStatsToVercel.Value)
        {
            var userId = VercelStatsSender.GetOrCreateUserId();
            Log.LogInfo($"User ID: {userId}");
        }
        
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

        // Initialize MainThreadDispatcher FIRST (critical for IL2CPP)
        AddComponent<MainThreadDispatcher>();
        
        // Start background tasks using a MonoBehaviour component
        AddComponent<AUSummaryBehaviour>();
    }

    /// <summary>
    /// MonoBehaviour component to run background tasks
    /// </summary>
    private class AUSummaryBehaviour : MonoBehaviour
    {
        private float _startTime;
        private bool _updateCheckDone;
        private bool _uploadDone;
        
        private void Start()
        {
            _startTime = Time.time;
        }
        
        private void Update()
        {
            // Check for updates after 5 seconds
            if (!_updateCheckDone && Time.time - _startTime > 5f)
            {
                _updateCheckDone = true;
                if (Instance._checkForUpdates.Value)
                {
                    // Run on background thread to avoid blocking Unity
                    Task.Run(() => CheckForUpdates());
                }
            }
            
            // Upload past games after 10 seconds
            if (!_uploadDone && Time.time - _startTime > 10f)
            {
                _uploadDone = true;
                if (Instance._sendStatsToVercel.Value)
                {
                    // Run on background thread to avoid blocking Unity
                    Task.Run(() => UploadPastGames());
                }
            }
        }
        
        /// <summary>
        /// Checks for mod updates (runs on background thread)
        /// </summary>
        private void CheckForUpdates()
        {
            try
            {
                Instance.Log.LogInfo("Checking for updates...");
                
                var task = UpdateChecker.CheckForUpdatesAsync(CancellationToken.None);
                task.Wait(TimeSpan.FromSeconds(15)); // Wait with timeout
                
                if (task.IsCompleted && !task.IsFaulted)
                {
                    var updateInfo = task.Result;
                    
                    if (updateInfo.UpdateAvailable)
                    {
                        Instance.Log.LogWarning("╔═══════════════════════════════════════════════════════╗");
                        Instance.Log.LogWarning("║           NEW VERSION AVAILABLE!                     ║");
                        Instance.Log.LogWarning($"║  Current: v{updateInfo.CurrentVersion} New: v{updateInfo.LatestVersion}");
                        Instance.Log.LogWarning($"║  Download: {updateInfo.DownloadUrl}");
                        Instance.Log.LogWarning("║                                                       ║");
                        Instance.Log.LogWarning("║  Please update to get the latest features & fixes!  ║");
                        Instance.Log.LogWarning("╚═══════════════════════════════════════════════════════╝");
                    }
                    else
                    {
                        Instance.Log.LogInfo($"You're on the latest version (v{updateInfo.CurrentVersion})!");
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Log.LogWarning($"Could not check for updates: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Uploads past games (runs on background thread)
        /// </summary>
        private void UploadPastGames()
        {
            try
            {
                Instance.Log.LogInfo("Checking for past games to upload...");
                
                var task = VercelStatsSender.UploadPastGamesAsync(CancellationToken.None);
                task.Wait(TimeSpan.FromMinutes(5)); // Wait with longer timeout for uploads
                
                if (task.IsCompleted && !task.IsFaulted)
                {
                    var uploadedCount = task.Result;
                    
                    if (uploadedCount > 0)
                    {
                        Instance.Log.LogInfo($"Successfully uploaded {uploadedCount} past games to the dashboard!");
                    }
                    else
                    {
                        Instance.Log.LogInfo("No past games to upload.");
                    }
                }
            }
            catch (Exception ex)
            {
                Instance.Log.LogWarning($"Could not upload past games: {ex.Message}");
                Instance.Log.LogWarning($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}

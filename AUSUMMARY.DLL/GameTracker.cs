using System;
using System.IO;
using System.Linq;
using AUSUMMARY.Shared;
using AUSUMMARY.Shared.Models;
using BepInEx.Logging;

namespace AUSUMMARY.DLL;

/// <summary>
/// Tracks game progress and generates match summaries
/// </summary>
public static class GameTracker
{
    private static ManualLogSource? _log;
    private static GameSummary _currentGame = new();
    private static DateTime _gameStartTime;
    private static bool _isGameActive;

    public static void Initialize(ManualLogSource log)
    {
        _log = log;
        DebugLog("GameTracker initialized");
    }

    #region Game Lifecycle

    public static void OnGameStart()
    {
        try
        {
            DebugLog("=== GAME STARTED ===");
            _currentGame = new GameSummary
            {
                MatchId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now
            };
            _gameStartTime = DateTime.Now;
            _isGameActive = true;

            // Capture metadata
            CaptureGameMetadata();
            
            // Capture all players
            CapturePlayers();
            
            AddGameEvent("GameStart", "Match began");
            DebugLog($"Tracking {_currentGame.Players.Count} players");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error starting game tracking: {ex.Message}");
        }
    }

    public static void OnGameEnd(GameOverReason endReason)
    {
        try
        {
            if (!_isGameActive)
                return;

            DebugLog("=== GAME ENDED ===");
            _isGameActive = false;

            // Calculate game duration
            _currentGame.Metadata.GameDuration = DateTime.Now - _gameStartTime;

            // Update metadata
            UpdateGameMetadata();

            // Determine winner
            DetermineWinner(endReason);

            // Calculate final statistics
            CalculateFinalStatistics();

            AddGameEvent("GameEnd", $"Match ended: {endReason}");

            // Save summary to file
            SaveGameSummary();
            
            // Send to Vercel for global stats (async, non-blocking)
            _ = VercelStatsSender.SendGameStatsAsync(_currentGame);
            
            DebugLog("Game summary saved successfully");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error ending game tracking: {ex.Message}\n{ex.StackTrace}");
        }
    }

    #endregion

    #region Data Capture

    private static void CaptureGameMetadata()
    {
        try
        {
            var shipStatus = ShipStatus.Instance;
            if (shipStatus == null) return;

            _currentGame.Metadata.MapName = GetMapName(shipStatus.Type);
            _currentGame.Metadata.GameMode = GameOptionsManager.Instance?.CurrentGameOptions?.GameMode switch
            {
                GameModes.Normal => "Classic",
                GameModes.HideNSeek => "Hide and Seek",
                _ => "Unknown"
            };
            _currentGame.Metadata.ModVersion = AUSummaryConstants.Version;

            DebugLog($"Map: {_currentGame.Metadata.MapName}, Mode: {_currentGame.Metadata.GameMode}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error capturing metadata: {ex.Message}");
        }
    }

    private static void CapturePlayers()
    {
        try
        {
            var allPlayers = PlayerControl.AllPlayerControls.ToArray();
            _currentGame.Metadata.PlayerCount = allPlayers.Length;

            foreach (var player in allPlayers)
            {
                if (player == null) continue;

                var snapshot = new PlayerSnapshot
                {
                    PlayerName = player.Data?.PlayerName ?? "Unknown",
                    PlayerId = player.PlayerId,
                    ColorName = GetColorName(player.Data?.DefaultOutfit?.ColorId ?? 0),
                    ColorId = player.Data?.DefaultOutfit?.ColorId ?? 0,
                    HatId = player.Data?.DefaultOutfit?.HatId ?? "",
                    PetId = player.Data?.DefaultOutfit?.PetId ?? "",
                    SkinId = player.Data?.DefaultOutfit?.SkinId ?? "",
                    VisorId = player.Data?.DefaultOutfit?.VisorId ?? "",
                    NameplateId = player.Data?.DefaultOutfit?.NamePlateId ?? "",
                    IsAlive = !player.Data?.IsDead ?? true,
                    TotalTasks = player.Data?.Tasks?.Count ?? 0
                };

                // Get role information (will be updated during intro)
                UpdatePlayerRole(snapshot, player);

                _currentGame.Players.Add(snapshot);
                DebugLog($"Captured player: {snapshot.PlayerName} ({snapshot.ColorName}) - Hat:{snapshot.HatId}");
            }
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error capturing players: {ex.Message}");
        }
    }

    private static void UpdatePlayerRole(PlayerSnapshot snapshot, PlayerControl player)
    {
        try
        {
            if (player?.Data?.Role == null) return;

            snapshot.Role = player.Data.Role.NiceName ?? player.Data.Role.Role.ToString();
            snapshot.Team = player.Data.Role.TeamType switch
            {
                RoleTeamTypes.Crewmate => "Crewmate",
                RoleTeamTypes.Impostor => "Impostor",
                _ => "Unknown"
            };

            DebugLog($"Updated {snapshot.PlayerName} role: {snapshot.Role} ({snapshot.Team})");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error updating player role: {ex.Message}");
        }
    }

    private static void UpdateGameMetadata()
    {
        try
        {
            // Count meetings
            _currentGame.Metadata.TotalMeetings = _currentGame.Events
                .Count(e => e.EventType == "MeetingCalled" || e.EventType == "BodyReported");

            // Count tasks
            int totalTasks = 0;
            int completedTasks = 0;

            foreach (var player in _currentGame.Players)
            {
                totalTasks += player.TotalTasks;
                completedTasks += player.TasksCompleted;
            }

            _currentGame.Metadata.TotalTasks = totalTasks;
            _currentGame.Metadata.CompletedTasks = completedTasks;
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error updating game metadata: {ex.Message}");
        }
    }

    #endregion

    #region Event Tracking

    public static void OnPlayerDeath(PlayerControl player, DeathReason reason)
    {
        try
        {
            var snapshot = _currentGame.Players.FirstOrDefault(p => p.PlayerId == player.PlayerId);
            if (snapshot == null) return;

            snapshot.IsAlive = false;
            snapshot.TimeOfDeath = GetGameTime();
            snapshot.DeathCause = reason.ToString();

            AddGameEvent("PlayerKilled", $"{snapshot.PlayerName} died: {reason}", new[] { snapshot.PlayerName });
            DebugLog($"Player death: {snapshot.PlayerName} - {reason}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error tracking player death: {ex.Message}");
        }
    }

    public static void OnMeetingCalled(PlayerControl caller)
    {
        try
        {
            var callerName = caller?.Data?.PlayerName ?? "Unknown";
            AddGameEvent("MeetingCalled", $"{callerName} called an emergency meeting", new[] { callerName });
            DebugLog($"Meeting called by {callerName}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error tracking meeting: {ex.Message}");
        }
    }

    public static void OnBodyReported(PlayerControl reporter, GameData.PlayerInfo deadBody)
    {
        try
        {
            var reporterName = reporter?.Data?.PlayerName ?? "Unknown";
            var deadName = deadBody?.PlayerName ?? "Unknown";
            AddGameEvent("BodyReported", $"{reporterName} reported {deadName}'s body", new[] { reporterName, deadName });
            DebugLog($"Body reported: {deadName} by {reporterName}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error tracking body report: {ex.Message}");
        }
    }

    public static void OnTaskCompleted(PlayerControl player)
    {
        try
        {
            var snapshot = _currentGame.Players.FirstOrDefault(p => p.PlayerId == player.PlayerId);
            if (snapshot != null)
            {
                snapshot.TasksCompleted++;
                DebugLog($"{snapshot.PlayerName} completed task: {snapshot.TasksCompleted}/{snapshot.TotalTasks}");
            }
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error tracking task completion: {ex.Message}");
        }
    }

    public static void OnPlayerEjected(GameData.PlayerInfo ejected, bool wasTie)
    {
        try
        {
            if (ejected == null) return;

            var snapshot = _currentGame.Players.FirstOrDefault(p => p.PlayerId == ejected.PlayerId);
            if (snapshot != null)
            {
                snapshot.IsAlive = false;
                snapshot.WasEjected = true;
                snapshot.TimeOfDeath = GetGameTime();
                snapshot.DeathCause = "Ejected";
            }

            var message = wasTie ? $"Tie vote - {ejected.PlayerName} was ejected" : $"{ejected.PlayerName} was ejected";
            AddGameEvent("PlayerEjected", message, new[] { ejected.PlayerName });
            DebugLog($"Player ejected: {ejected.PlayerName}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error tracking ejection: {ex.Message}");
        }
    }

    #endregion

    #region Statistics and Winners

    private static void DetermineWinner(GameOverReason reason)
    {
        try
        {
            _currentGame.Winner.WinCondition = reason.ToString();

            switch (reason)
            {
                case GameOverReason.HumansByTask:
                    _currentGame.Winner.WinningTeam = "Crewmate";
                    _currentGame.Winner.Winners = _currentGame.Players
                        .Where(p => p.Team == "Crewmate")
                        .Select(p => p.PlayerName)
                        .ToList();
                    break;

                case GameOverReason.HumansByVote:
                    _currentGame.Winner.WinningTeam = "Crewmate";
                    _currentGame.Winner.Winners = _currentGame.Players
                        .Where(p => p.Team == "Crewmate")
                        .Select(p => p.PlayerName)
                        .ToList();
                    break;

                case GameOverReason.ImpostorByKill:
                case GameOverReason.ImpostorBySabotage:
                case GameOverReason.ImpostorByVote:
                    _currentGame.Winner.WinningTeam = "Impostor";
                    _currentGame.Winner.Winners = _currentGame.Players
                        .Where(p => p.Team == "Impostor")
                        .Select(p => p.PlayerName)
                        .ToList();
                    break;

                default:
                    _currentGame.Winner.WinningTeam = "Unknown";
                    break;
            }

            DebugLog($"Winner determined: {_currentGame.Winner.WinningTeam} by {reason}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error determining winner: {ex.Message}");
        }
    }

    private static void CalculateFinalStatistics()
    {
        try
        {
            _currentGame.Statistics.TotalKills = _currentGame.Players.Sum(p => p.KillCount);
            _currentGame.Statistics.TotalDeaths = _currentGame.Players.Count(p => !p.IsAlive);
            _currentGame.Statistics.TotalEjections = _currentGame.Players.Count(p => p.WasEjected);

            var totalTasks = _currentGame.Players.Sum(p => p.TotalTasks);
            var completedTasks = _currentGame.Players.Sum(p => p.TasksCompleted);
            _currentGame.Statistics.TaskCompletionRate = totalTasks > 0 ? (float)completedTasks / totalTasks : 0f;

            DebugLog($"Final stats - Kills: {_currentGame.Statistics.TotalKills}, Deaths: {_currentGame.Statistics.TotalDeaths}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error calculating statistics: {ex.Message}");
        }
    }

    #endregion

    #region Helpers

    private static void AddGameEvent(string eventType, string description, string[]? involvedPlayers = null)
    {
        var gameEvent = new GameEvent
        {
            EventType = eventType,
            Timestamp = GetGameTime(),
            Description = description,
            InvolvedPlayers = involvedPlayers?.ToList() ?? new List<string>()
        };

        _currentGame.Events.Add(gameEvent);
    }

    private static float GetGameTime()
    {
        return (float)(DateTime.Now - _gameStartTime).TotalSeconds;
    }

    private static string GetMapName(ShipStatus.MapType mapType)
    {
        return mapType switch
        {
            ShipStatus.MapType.Ship => "The Skeld",
            ShipStatus.MapType.Hq => "MIRA HQ",
            ShipStatus.MapType.Pb => "Polus",
            ShipStatus.MapType.Airship => "The Airship",
            ShipStatus.MapType.Fungle => "The Fungle",
            _ => "Unknown"
        };
    }

    private static string GetColorName(int colorId)
    {
        // Basic color names - can be expanded
        var colors = new[] { "Red", "Blue", "Green", "Pink", "Orange", "Yellow", "Black", "White",
            "Purple", "Brown", "Cyan", "Lime", "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral" };
        return colorId >= 0 && colorId < colors.Length ? colors[colorId] : "Unknown";
    }

    private static void SaveGameSummary()
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            Directory.CreateDirectory(summariesPath);

            var fileName = $"game_{_currentGame.Timestamp:yyyyMMdd_HHmmss}_{_currentGame.MatchId.Substring(0, 8)}.json";
            var filePath = Path.Combine(summariesPath, fileName);

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_currentGame, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json);

            _log?.LogInfo($"Game summary saved: {fileName}");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Error saving game summary: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void DebugLog(string message)
    {
        if (AUSummaryPlugin.EnableDebugLogging)
            _log?.LogInfo($"[GameTracker] {message}");
    }

    #endregion
}

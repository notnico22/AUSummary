using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AUSUMMARY.Shared;
using AUSUMMARY.Shared.Models;
using BepInEx.Logging;
using UnityEngine;

namespace AUSUMMARY.DLL;

/// <summary>
/// Core class that tracks game state and generates summaries
/// </summary>
public static class GameTracker
{
    private static GameSummary? _currentGame;
    private static DateTime _gameStartTime;
    private static bool _isTracking;
    private static ManualLogSource? _logger;

    /// <summary>
    /// Initialize the game tracker
    /// </summary>
    public static void Initialize(ManualLogSource logger)
    {
        _logger = logger;
        _logger.LogInfo("GameTracker initialized");
        
        // Ensure output directory exists
        var path = AUSummaryConstants.GetSummariesPath();
        _logger.LogInfo($"Summaries directory: {path}");
    }

    /// <summary>
    /// Start tracking a new game
    /// </summary>
    public static void StartGame()
    {
        try
        {
            _logger?.LogInfo("Starting new game tracking...");
            
            _currentGame = new GameSummary
            {
                MatchId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now
            };

            _gameStartTime = DateTime.Now;
            _isTracking = true;

            // Capture initial metadata
            CaptureGameMetadata();

            // Add game start event
            AddEvent(new GameEvent
            {
                EventType = "GameStart",
                Timestamp = GetGameTime(),
                Description = "Game started"
            });

            _logger?.LogInfo($"Game tracking started: {_currentGame.MatchId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error starting game tracking: {ex.Message}");
        }
    }

    /// <summary>
    /// End game tracking and save summary
    /// </summary>
    public static void EndGame(string winningTeam, string winCondition)
    {
        if (!_isTracking || _currentGame == null)
        {
            _logger?.LogWarning("Attempted to end game tracking but no active game");
            return;
        }

        try
        {
            _logger?.LogInfo("Ending game tracking...");

            // Set game duration
            _currentGame.Metadata.GameDuration = DateTime.Now - _gameStartTime;

            // Set winner information
            _currentGame.Winner.WinningTeam = winningTeam;
            _currentGame.Winner.WinCondition = winCondition;
            _currentGame.Winner.Winners = _currentGame.Players
                .Where(p => p.Team == winningTeam)
                .Select(p => p.PlayerName)
                .ToList();

            // Calculate statistics
            CalculateStatistics();

            // Add game end event
            AddEvent(new GameEvent
            {
                EventType = "GameEnd",
                Timestamp = GetGameTime(),
                Description = $"{winningTeam} wins by {winCondition}",
                Data = new Dictionary<string, object>
                {
                    ["winningTeam"] = winningTeam,
                    ["winners"] = _currentGame.Winner.Winners
                }
            });

            // Save to file
            SaveGameSummary();

            _logger?.LogInfo($"Game tracking ended: {_currentGame.MatchId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error ending game tracking: {ex.Message}");
        }
        finally
        {
            _isTracking = false;
            _currentGame = null;
        }
    }

    /// <summary>
    /// Capture player information
    /// </summary>
    public static void CapturePlayerData(NetworkedPlayerInfo playerInfo, string role, string team)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            var existingPlayer = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerInfo.PlayerId);
            
            if (existingPlayer != null)
            {
                // Update existing player
                existingPlayer.Role = role;
                existingPlayer.Team = team;
                existingPlayer.IsAlive = !playerInfo.IsDead;
                
                _logger?.LogInfo($"Updated player: {existingPlayer.PlayerName} - {role} ({team})");
            }
            else
            {
                // Get actual color name
                var colorName = GetColorName(playerInfo.DefaultOutfit.ColorId);

                // Add new player
                var newPlayer = new PlayerSnapshot
                {
                    PlayerName = playerInfo.PlayerName,
                    PlayerId = playerInfo.PlayerId,
                    ColorName = colorName,
                    Role = role,
                    Team = team,
                    IsAlive = !playerInfo.IsDead,
                    TotalTasks = playerInfo.Tasks?.Count ?? 0
                };
                
                _currentGame.Players.Add(newPlayer);
                
                _logger?.LogInfo($"Added player: {newPlayer.PlayerName} ({colorName}) - {role} ({team})");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error capturing player data: {ex.Message}");
        }
    }

    /// <summary>
    /// Record a player death
    /// </summary>
    public static void RecordDeath(byte playerId, string cause, string? killerName = null)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            var player = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.IsAlive = false;
                player.DeathCause = cause;
                player.TimeOfDeath = GetGameTime();
                player.KilledBy = killerName;
                player.WasEjected = cause == "Ejected";

                _logger?.LogInfo($"Recorded death: {player.PlayerName} - {cause}" + (killerName != null ? $" by {killerName}" : ""));

                AddEvent(new GameEvent
                {
                    EventType = "PlayerKilled",
                    Timestamp = GetGameTime(),
                    Description = $"{player.PlayerName} {cause}" + (killerName != null ? $" by {killerName}" : ""),
                    InvolvedPlayers = killerName != null ? new List<string> { player.PlayerName, killerName } : new List<string> { player.PlayerName }
                });
            }
            else
            {
                _logger?.LogWarning($"Could not find player with ID {playerId} to record death");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error recording death: {ex.Message}");
        }
    }

    /// <summary>
    /// Record a meeting being called
    /// </summary>
    public static void RecordMeeting(bool isEmergency, string? reporterName = null)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            _currentGame.Metadata.TotalMeetings++;

            AddEvent(new GameEvent
            {
                EventType = isEmergency ? "EmergencyMeeting" : "BodyReported",
                Timestamp = GetGameTime(),
                Description = isEmergency ? $"{reporterName} called emergency meeting" : $"{reporterName} reported a body",
                InvolvedPlayers = reporterName != null ? new List<string> { reporterName } : new List<string>()
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error recording meeting: {ex.Message}");
        }
    }

    /// <summary>
    /// Record a task completion
    /// </summary>
    public static void RecordTaskComplete(byte playerId)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            var player = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.TasksCompleted++;
                _currentGame.Metadata.CompletedTasks++;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error recording task completion: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private static void CaptureGameMetadata()
    {
        if (_currentGame == null) return;

        try
        {
            _currentGame.Metadata.MapName = GetMapName();
            _currentGame.Metadata.GameMode = "Town of Us";
            _currentGame.Metadata.PlayerCount = GameData.Instance?.PlayerCount ?? 0;
            _currentGame.Metadata.ModVersion = AUSummaryConstants.Version;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error capturing game metadata: {ex.Message}");
        }
    }

    private static string GetMapName()
    {
        try
        {
            var shipStatus = ShipStatus.Instance;
            if (shipStatus == null) return "Unknown";

            return shipStatus.Type switch
            {
                ShipStatus.MapType.Ship => "The Skeld",
                ShipStatus.MapType.Hq => "MIRA HQ",
                ShipStatus.MapType.Pb => "Polus",
                _ => "Unknown"
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetColorName(int colorId)
    {
        // Actual color names matching Among Us
        var colorNames = new[]
        {
            "Red", "Blue", "Green", "Pink", "Orange", "Yellow",
            "Black", "White", "Purple", "Brown", "Cyan", "Lime",
            "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral"
        };

        if (colorId >= 0 && colorId < colorNames.Length)
            return colorNames[colorId];

        return $"Color{colorId}";
    }

    private static float GetGameTime()
    {
        return (float)(DateTime.Now - _gameStartTime).TotalSeconds;
    }

    private static void AddEvent(GameEvent gameEvent)
    {
        if (_currentGame == null) return;
        _currentGame.Events.Add(gameEvent);
    }

    private static void CalculateStatistics()
    {
        if (_currentGame == null) return;

        try
        {
            // Count kills from Events, not from DeathCause
            _currentGame.Statistics.TotalKills = _currentGame.Events.Count(e => e.EventType == "PlayerKilled" && e.Description.Contains("Killed"));
            _currentGame.Statistics.TotalEjections = _currentGame.Events.Count(e => e.EventType == "PlayerKilled" && e.Description.Contains("Ejected"));
            
            var totalTasks = _currentGame.Players.Sum(p => p.TotalTasks);
            var completedTasks = _currentGame.Metadata.CompletedTasks;
            _currentGame.Statistics.TaskCompletionRate = totalTasks > 0 ? (float)completedTasks / totalTasks : 0;

            _currentGame.Metadata.TotalTasks = totalTasks;
            
            _logger?.LogInfo($"Statistics calculated: {_currentGame.Statistics.TotalKills} kills, {_currentGame.Statistics.TotalEjections} ejections");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error calculating statistics: {ex.Message}");
        }
    }

    private static void SaveGameSummary()
    {
        if (_currentGame == null) return;

        try
        {
            var filePath = AUSummaryConstants.GetSummaryFilePath(_currentGame.MatchId);
            
            // Use System.Text.Json instead to avoid conflicts
            var json = System.Text.Json.JsonSerializer.Serialize(_currentGame, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(filePath, json);
            
            _logger?.LogInfo($"Game summary saved: {filePath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error saving game summary: {ex.Message}");
            _logger?.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    #endregion
}

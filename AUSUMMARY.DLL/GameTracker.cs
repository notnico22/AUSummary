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

            // Capture modifiers at end of game
            CaptureAllModifiers();

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
    /// Capture player information including modifiers
    /// </summary>
    public static void CapturePlayerData(NetworkedPlayerInfo playerInfo, string role, string team, List<string> modifiers)
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
                existingPlayer.Modifiers = modifiers;
                existingPlayer.IsAlive = !playerInfo.IsDead;
                
                _logger?.LogInfo($"Updated player: {existingPlayer.PlayerName} - {role} ({team}) Modifiers: {string.Join(", ", modifiers)}");
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
                    Modifiers = modifiers,
                    IsAlive = !playerInfo.IsDead,
                    TotalTasks = playerInfo.Tasks?.Count ?? 0
                };
                
                _currentGame.Players.Add(newPlayer);
                
                _logger?.LogInfo($"Added player: {newPlayer.PlayerName} ({colorName}) - {role} ({team}) Modifiers: {string.Join(", ", modifiers)}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error capturing player data: {ex.Message}");
        }
    }

    /// <summary>
    /// Capture all players' modifiers at game end
    /// </summary>
    public static void CaptureAllModifiers()
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            _logger?.LogInfo("Capturing modifiers at game end...");
            
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;

                var modifiers = GetPlayerModifiers(player);
                var existingPlayer = _currentGame.Players.FirstOrDefault(p => p.PlayerId == player.PlayerId);
                
                if (existingPlayer != null && modifiers.Any())
                {
                    existingPlayer.Modifiers = modifiers;
                    _logger?.LogInfo($"Updated {existingPlayer.PlayerName} modifiers: {string.Join(", ", modifiers)}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error capturing all modifiers: {ex.Message}");
        }
    }

    /// <summary>
    /// Update a player's alive status (called at game end)
    /// </summary>
    public static void UpdatePlayerAliveStatus(byte playerId, bool isAlive)
    {
        try
        {
            if (_currentGame == null) return;

            var player = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.IsAlive = isAlive;
                
                // If dead and we don't have death info, mark as dead with unknown cause
                if (!isAlive && string.IsNullOrEmpty(player.DeathCause))
                {
                    player.DeathCause = "Unknown";
                    if (string.IsNullOrEmpty(player.KillType))
                        player.KillType = "Killed";
                }
                
                _logger?.LogInfo($"Updated alive status: {player.PlayerName} = {isAlive}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error updating player alive status: {ex.Message}");
        }
    }

    /// <summary>
    /// Mark all players on the losing team as dead (used when game ends by kill)
    /// </summary>
    public static void MarkLosingTeamDead(string winningTeam)
    {
        try
        {
            if (_currentGame == null) return;

            _logger?.LogInfo($"Marking losing team as dead. Winning team: {winningTeam}");

            foreach (var player in _currentGame.Players)
            {
                // If this player's team is NOT the winning team, mark them as dead
                if (player.Team != winningTeam)
                {
                    player.IsAlive = false;
                    
                    // If we don't have death details, add generic ones
                    if (string.IsNullOrEmpty(player.DeathCause))
                    {
                        player.DeathCause = "Killed";
                    }
                    if (string.IsNullOrEmpty(player.KillType))
                    {
                        player.KillType = "Killed";
                    }
                    
                    _logger?.LogInfo($"Marked {player.PlayerName} ({player.Team}) as dead (losing team)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error marking losing team dead: {ex.Message}");
        }
    }

    /// <summary>
    /// Update death information for a player (used when RPC arrives after Die method)
    /// </summary>
    public static void UpdateDeathInfo(byte playerId, string killerName, string killType)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            var player = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                // Update killer info
                player.KilledBy = killerName;
                player.KillType = killType;

                // Increment killer's kill count if not already done
                var killer = _currentGame.Players.FirstOrDefault(p => p.PlayerName == killerName);
                if (killer != null)
                {
                    // Check if we already counted this kill
                    var currentKillCount = killer.KillCount;
                    var expectedKillCount = _currentGame.Players.Count(p => p.KilledBy == killerName && !p.IsAlive);
                    
                    if (currentKillCount < expectedKillCount)
                    {
                        killer.KillCount = expectedKillCount;
                        _logger?.LogInfo($"Updated {killerName} kill count to {killer.KillCount}");
                    }
                }

                _logger?.LogInfo($"Updated death info: {player.PlayerName} was {killType} by {killerName}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error updating death info: {ex.Message}");
        }
    }

    /// <summary>
    /// Record a player death with kill type
    /// </summary>
    public static void RecordDeath(byte playerId, string cause, string? killerName = null, string? killType = null)
    {
        if (!_isTracking || _currentGame == null) return;

        try
        {
            var player = _currentGame.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.IsAlive = false;
                player.DeathCause = cause;
                player.KillType = killType ?? "Killed";
                player.TimeOfDeath = GetGameTime();
                player.KilledBy = killerName;
                player.WasEjected = cause == "Ejected";

                // Increment killer's kill count
                if (killerName != null)
                {
                    var killer = _currentGame.Players.FirstOrDefault(p => p.PlayerName == killerName);
                    if (killer != null)
                    {
                        killer.KillCount++;
                    }
                }

                var statusText = player.IsAlive ? "Alive" : $"Dead ({player.KillType})";
                _logger?.LogInfo($"Recorded death: {player.PlayerName} - {statusText}" + (killerName != null ? $" by {killerName}" : ""));

                AddEvent(new GameEvent
                {
                    EventType = "PlayerKilled",
                    Timestamp = GetGameTime(),
                    Description = $"{player.PlayerName} {statusText}" + (killerName != null ? $" by {killerName}" : ""),
                    InvolvedPlayers = killerName != null ? new List<string> { player.PlayerName, killerName } : new List<string> { player.PlayerName },
                    Data = new Dictionary<string, object>
                    {
                        ["killType"] = killType ?? "Killed",
                        ["cause"] = cause
                    }
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
                
                _logger?.LogInfo($"{player.PlayerName} completed task ({player.TasksCompleted}/{player.TotalTasks})");
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
                _ => shipStatus.Type.ToString()
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

    private static List<string> GetPlayerModifiers(PlayerControl player)
    {
        var modifiers = new List<string>();

        try
        {
            // Try to get modifier component using MiraAPI
            var modifierComponentType = Type.GetType("MiraAPI.Modifiers.ModifierComponent, MiraAPI");
            if (modifierComponentType == null) return modifiers;

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
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error getting modifiers for {player.Data.PlayerName}: {ex.Message}");
        }

        return modifiers;
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
            // Count total deaths (all dead players)
            _currentGame.Statistics.TotalDeaths = _currentGame.Players.Count(p => !p.IsAlive);
            
            // Count kills from player kill counts
            _currentGame.Statistics.TotalKills = _currentGame.Players.Sum(p => p.KillCount);
            
            // Count ejections
            _currentGame.Statistics.TotalEjections = _currentGame.Players.Count(p => p.WasEjected);
            
            var totalTasks = _currentGame.Players.Sum(p => p.TotalTasks);
            var completedTasks = _currentGame.Metadata.CompletedTasks;
            _currentGame.Statistics.TaskCompletionRate = totalTasks > 0 ? (float)completedTasks / totalTasks : 0;

            _currentGame.Metadata.TotalTasks = totalTasks;
            
            _logger?.LogInfo($"Statistics calculated: {_currentGame.Statistics.TotalKills} kills, {_currentGame.Statistics.TotalDeaths} deaths, {_currentGame.Statistics.TotalEjections} ejections");
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

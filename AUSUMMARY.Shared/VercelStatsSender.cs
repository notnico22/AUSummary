using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AUSUMMARY.Shared.Models;
using Newtonsoft.Json;

namespace AUSUMMARY.Shared;

/// <summary>
/// Sends game statistics to a Vercel-hosted dashboard for global analytics
/// </summary>
public static class VercelStatsSender
{
    private const string VercelEndpoint = "https://ausummary.vercel.app/api/stats";
    private const string UserIdFileName = "user_id.txt";
    
    private static readonly HttpClient _httpClient = new();
    private static string? _userId;

    /// <summary>
    /// Gets or generates a unique user ID for this player
    /// </summary>
    public static string GetOrCreateUserId()
    {
        if (!string.IsNullOrEmpty(_userId))
            return _userId;

        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            var userIdPath = Path.Combine(summariesPath, UserIdFileName);

            // Check if user ID file exists
            if (File.Exists(userIdPath))
            {
                _userId = File.ReadAllText(userIdPath).Trim();
                if (!string.IsNullOrEmpty(_userId))
                {
                    Console.WriteLine($"Loaded existing user ID: {_userId}");
                    return _userId;
                }
            }

            // Generate new user ID
            _userId = Guid.NewGuid().ToString();
            File.WriteAllText(userIdPath, _userId);
            Console.WriteLine($"Generated new user ID: {_userId}");
            
            return _userId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error managing user ID: {ex.Message}");
            // Fallback to session-based ID
            _userId = Guid.NewGuid().ToString();
            return _userId;
        }
    }

    /// <summary>
    /// Uploads all past games that haven't been uploaded yet (on first launch)
    /// </summary>
    public static async Task<int> UploadPastGamesAsync()
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            if (!Directory.Exists(summariesPath))
                return 0;

            var jsonFiles = Directory.GetFiles(summariesPath, "*.json")
                .Where(f => !f.EndsWith("-up.json")) // Only non-uploaded files
                .ToList();

            if (jsonFiles.Count == 0)
            {
                Console.WriteLine("No past games to upload");
                return 0;
            }

            Console.WriteLine($"Found {jsonFiles.Count} past games to upload");
            int successCount = 0;

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var summary = JsonConvert.DeserializeObject<GameSummary>(json);
                    
                    if (summary != null && await SendGameStatsAsync(summary, file))
                    {
                        successCount++;
                        // Small delay to avoid overwhelming the API
                        await Task.Delay(200);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"Successfully uploaded {successCount}/{jsonFiles.Count} past games");
            return successCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading past games: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Sends a complete game summary to the Vercel analytics endpoint
    /// </summary>
    public static async Task<bool> SendGameStatsAsync(GameSummary summary, string? filePath = null)
    {
        try
        {
            var userId = GetOrCreateUserId();
            
            // Send the complete game summary with user ID
            var stats = new
            {
                userId = userId,
                matchId = summary.MatchId,
                timestamp = summary.Timestamp,
                metadata = new
                {
                    mapName = summary.Metadata.MapName,
                    gameMode = summary.Metadata.GameMode,
                    playerCount = summary.Metadata.PlayerCount,
                    gameDuration = summary.Metadata.GameDuration.TotalSeconds,
                    totalMeetings = summary.Metadata.TotalMeetings,
                    totalTasks = summary.Metadata.TotalTasks,
                    completedTasks = summary.Metadata.CompletedTasks,
                    modVersion = summary.Metadata.ModVersion
                },
                players = summary.Players.Select(p => new
                {
                    playerName = p.PlayerName,
                    playerId = p.PlayerId,
                    colorName = p.ColorName,
                    role = p.Role,
                    team = p.Team,
                    modifiers = p.Modifiers,
                    isAlive = p.IsAlive,
                    deathCause = p.DeathCause,
                    killType = p.KillType,
                    timeOfDeath = p.TimeOfDeath,
                    killedBy = p.KilledBy,
                    killCount = p.KillCount,
                    tasksCompleted = p.TasksCompleted,
                    totalTasks = p.TotalTasks,
                    wasEjected = p.WasEjected,
                    survivedRounds = p.SurvivedRounds
                }).ToList(),
                events = summary.Events.Select(e => new
                {
                    eventType = e.EventType,
                    timestamp = e.Timestamp,
                    description = e.Description,
                    involvedPlayers = e.InvolvedPlayers,
                    data = e.Data
                }).ToList(),
                winner = new
                {
                    winningTeam = summary.Winner.WinningTeam,
                    winCondition = summary.Winner.WinCondition,
                    winners = summary.Winner.Winners,
                    mvp = summary.Winner.Mvp
                },
                statistics = new
                {
                    totalKills = summary.Statistics.TotalKills,
                    totalEjections = summary.Statistics.TotalEjections,
                    totalDeaths = summary.Statistics.TotalDeaths,
                    taskCompletionRate = summary.Statistics.TaskCompletionRate,
                    averageMeetingTime = summary.Statistics.AverageMeetingTime,
                    impostorWinRate = summary.Statistics.ImpostorWinRate,
                    crewmateWinRate = summary.Statistics.CrewmateWinRate
                }
            };

            var json = JsonConvert.SerializeObject(stats);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = await _httpClient.PostAsync(VercelEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Successfully uploaded game {summary.MatchId.Substring(0, 8)}");
                
                // Mark file as uploaded by renaming it
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && !filePath.EndsWith("-up.json"))
                {
                    try
                    {
                        var newPath = filePath.Replace(".json", "-up.json");
                        File.Move(filePath, newPath);
                        Console.WriteLine($"Renamed to: {Path.GetFileName(newPath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not rename file: {ex.Message}");
                    }
                }
                
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to upload game: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send stats to Vercel: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Batch sends multiple game summaries
    /// </summary>
    public static async Task<int> SendBatchStatsAsync(List<GameSummary> summaries)
    {
        int successCount = 0;
        
        foreach (var summary in summaries)
        {
            if (await SendGameStatsAsync(summary))
            {
                successCount++;
                await Task.Delay(200);
            }
        }

        return successCount;
    }
}

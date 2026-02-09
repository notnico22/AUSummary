using System;
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
    // TODO: Replace with your actual Vercel endpoint URL
    private const string VercelEndpoint = "https://ausummary.vercel.app/api/stats";
    
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Sends a game summary to the Vercel analytics endpoint
    /// </summary>
    public static async Task<bool> SendGameStatsAsync(GameSummary summary)
    {
        try
        {
            // Create anonymized stats payload
            var stats = new
            {
                matchId = summary.MatchId,
                timestamp = summary.Timestamp,
                mapName = summary.Metadata.MapName,
                gameMode = summary.Metadata.GameMode,
                playerCount = summary.Metadata.PlayerCount,
                gameDuration = summary.Metadata.GameDuration.TotalSeconds,
                winningTeam = summary.Winner.WinningTeam,
                winCondition = summary.Winner.WinCondition,
                totalKills = summary.Statistics.TotalKills,
                totalEjections = summary.Statistics.TotalEjections,
                taskCompletionRate = summary.Statistics.TaskCompletionRate,
                modVersion = summary.Metadata.ModVersion,
                // Anonymized player data
                roles = summary.Players.Select(p => new
                {
                    role = p.Role,
                    team = p.Team,
                    survived = p.IsAlive,
                    killCount = p.KillCount,
                    tasksCompleted = p.TasksCompleted,
                    totalTasks = p.TotalTasks
                }).ToList()
            };

            var json = JsonConvert.SerializeObject(stats);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            var response = await _httpClient.PostAsync(VercelEndpoint, content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // Log error but don't fail the game summary save
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
                // Small delay to avoid rate limiting
                await Task.Delay(100);
            }
        }

        return successCount;
    }
}

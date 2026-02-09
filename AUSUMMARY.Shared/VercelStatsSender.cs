using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AUSUMMARY.Shared.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AUSUMMARY.Shared;

/// <summary>
/// Sends game statistics to a Vercel-hosted dashboard for global analytics
/// </summary>
public static class VercelStatsSender
{
    private const string VercelEndpoint = "https://ausummary.vercel.app/api/stats";
    private const string UserIdFileName = "user_id.txt";
    
    private static string? _userId;
    
    // Rate limiting and concurrency control
    private static readonly SemaphoreSlim _uploadSemaphore = new SemaphoreSlim(1, 1);
    private const int UploadDelayMs = 1000; // 1 second delay between uploads
    private const int RetryDelayMs = 3000; // 3 seconds delay before retry
    private const int MaxRetries = 3;
    private const int MaxConcurrentUploads = 5; // Limit batch uploads
    private const int RequestTimeoutSeconds = 15;

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
    /// Uses raw JSON to avoid IL2CPP deserialization issues
    /// </summary>
    public static async Task<int> UploadPastGamesAsync(CancellationToken cancellationToken = default)
    {
        // IMPORTANT: Run on a background thread to avoid IL2CPP GC issues
        return await Task.Run(async () => await UploadPastGamesAsyncInternal(cancellationToken), cancellationToken);
    }
    
    private static async Task<int> UploadPastGamesAsyncInternal(CancellationToken cancellationToken)
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            if (!Directory.Exists(summariesPath))
                return 0;

            var jsonFiles = Directory.GetFiles(summariesPath, "*.json")
                .Where(f => !f.EndsWith("-up.json") && !f.EndsWith(UserIdFileName))
                .OrderBy(f => File.GetCreationTime(f))
                .ToList();

            if (jsonFiles.Count == 0)
            {
                Console.WriteLine("No past games to upload");
                return 0;
            }

            Console.WriteLine($"Found {jsonFiles.Count} past games to upload");
            
            // Limit the number of games to upload in one session to avoid overwhelming the API
            const int maxGamesToUpload = 50;
            if (jsonFiles.Count > maxGamesToUpload)
            {
                Console.WriteLine($"Limiting upload to {maxGamesToUpload} games to avoid overwhelming the server");
                Console.WriteLine($"Remaining {jsonFiles.Count - maxGamesToUpload} will be uploaded next time");
                jsonFiles = jsonFiles.Take(maxGamesToUpload).ToList();
            }
            
            int successCount = 0;
            var userId = GetOrCreateUserId();

            // Process in batches to avoid overwhelming the API
            for (int i = 0; i < jsonFiles.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Upload cancelled by user");
                    break;
                }
                
                var file = jsonFiles[i];
                
                try
                {
                    Console.WriteLine($"Uploading game {i + 1}/{jsonFiles.Count}...");
                    
                    // Read the raw JSON and inject userId without full deserialization
                    var jsonText = await File.ReadAllTextAsync(file, cancellationToken);
                    
                    // Use JObject to add userId without deserializing the whole object
                    var jObject = JObject.Parse(jsonText);
                    jObject["userId"] = userId;
                    
                    var modifiedJson = jObject.ToString(Formatting.None);
                    
                    if (await SendRawJsonWithRetryAsync(modifiedJson, file, cancellationToken))
                    {
                        successCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Upload cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"Successfully uploaded {successCount}/{jsonFiles.Count} past games");
            return successCount;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Past game upload cancelled");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading past games: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Public method to send raw JSON (called from GameTracker)
    /// </summary>
    public static async Task<bool> SendRawJsonAsync(string json, string? filePath = null, CancellationToken cancellationToken = default)
    {
        return await SendRawJsonWithRetryAsync(json, filePath, cancellationToken);
    }
    
    /// <summary>
    /// Sends raw JSON directly to the API with retry logic
    /// </summary>
    private static async Task<bool> SendRawJsonWithRetryAsync(string json, string? filePath = null, CancellationToken cancellationToken = default)
    {
        // Wait for semaphore to ensure sequential uploads
        await _uploadSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                
                try
                {
                    // Create a fresh HttpClient for each request to avoid "already started" errors
                    using var httpClient = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
                    };
                    
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(VercelEndpoint, content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        // Extract matchId from JSON for logging
                        var matchId = "unknown";
                        try
                        {
                            var jObject = JObject.Parse(json);
                            matchId = jObject["MatchId"]?.ToString()?.Substring(0, 8) ?? "unknown";
                        }
                        catch { }
                        
                        Console.WriteLine($"✓ Successfully uploaded game {matchId}");
                        
                        // Mark file as uploaded by renaming it
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && !filePath.EndsWith("-up.json"))
                        {
                            try
                            {
                                var newPath = filePath.Replace(".json", "-up.json");
                                File.Move(filePath, newPath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Could not rename file: {ex.Message}");
                            }
                        }
                        
                        // Delay before next upload to avoid overwhelming the API
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(UploadDelayMs, cancellationToken);
                        }
                        
                        return true;
                    }
                    else
                    {
                        var responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Failed to upload game (attempt {attempt}/{MaxRetries}): {response.StatusCode}");
                        
                        if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine($"  Retrying in {RetryDelayMs}ms...");
                            await Task.Delay(RetryDelayMs, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error sending stats (attempt {attempt}/{MaxRetries}): {ex.Message}");
                    
                    if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"  Retrying in {RetryDelayMs}ms...");
                        await Task.Delay(RetryDelayMs, cancellationToken);
                    }
                }
            }
            
            Console.WriteLine($"✗ Failed to upload after {MaxRetries} attempts");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends a complete game summary to the Vercel analytics endpoint
    /// </summary>
    public static async Task<bool> SendGameStatsAsync(GameSummary summary, string? filePath = null, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: Run on a background thread to avoid IL2CPP GC issues
        return await Task.Run(async () => await SendGameStatsAsyncInternal(summary, filePath, cancellationToken), cancellationToken);
    }
    
    private static async Task<bool> SendGameStatsAsyncInternal(GameSummary summary, string? filePath, CancellationToken cancellationToken)
    {
        // Wait for semaphore to ensure sequential uploads
        await _uploadSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            var userId = GetOrCreateUserId();
            
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                
                try
                {
                    // Send the complete game summary with user ID
                    var stats = new
                    {
                        userId = userId,
                        MatchId = summary.MatchId,
                        Timestamp = summary.Timestamp,
                        Metadata = new
                        {
                            MapName = summary.Metadata.MapName,
                            GameMode = summary.Metadata.GameMode,
                            PlayerCount = summary.Metadata.PlayerCount,
                            GameDuration = summary.Metadata.GameDuration.TotalSeconds,
                            TotalMeetings = summary.Metadata.TotalMeetings,
                            TotalTasks = summary.Metadata.TotalTasks,
                            CompletedTasks = summary.Metadata.CompletedTasks,
                            ModVersion = summary.Metadata.ModVersion
                        },
                        Players = summary.Players.Select(p => new
                        {
                            PlayerName = p.PlayerName,
                            PlayerId = p.PlayerId,
                            ColorName = p.ColorName,
                            Role = p.Role,
                            Team = p.Team,
                            Modifiers = p.Modifiers,
                            IsAlive = p.IsAlive,
                            DeathCause = p.DeathCause,
                            KillType = p.KillType,
                            TimeOfDeath = p.TimeOfDeath,
                            KilledBy = p.KilledBy,
                            KillCount = p.KillCount,
                            TasksCompleted = p.TasksCompleted,
                            TotalTasks = p.TotalTasks,
                            WasEjected = p.WasEjected,
                            SurvivedRounds = p.SurvivedRounds
                        }).ToList(),
                        Events = summary.Events.Select(e => new
                        {
                            EventType = e.EventType,
                            Timestamp = e.Timestamp,
                            Description = e.Description,
                            InvolvedPlayers = e.InvolvedPlayers,
                            Data = e.Data
                        }).ToList(),
                        Winner = new
                        {
                            WinningTeam = summary.Winner.WinningTeam,
                            WinCondition = summary.Winner.WinCondition,
                            Winners = summary.Winner.Winners,
                            Mvp = summary.Winner.Mvp
                        },
                        Statistics = new
                        {
                            TotalKills = summary.Statistics.TotalKills,
                            TotalEjections = summary.Statistics.TotalEjections,
                            TotalDeaths = summary.Statistics.TotalDeaths,
                            TaskCompletionRate = summary.Statistics.TaskCompletionRate,
                            AverageMeetingTime = summary.Statistics.AverageMeetingTime,
                            ImpostorWinRate = summary.Statistics.ImpostorWinRate,
                            CrewmateWinRate = summary.Statistics.CrewmateWinRate
                        }
                    };

                    var json = JsonConvert.SerializeObject(stats);
                    
                    // Create a fresh HttpClient for each request to avoid "already started" errors
                    using var httpClient = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
                    };
                    
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(VercelEndpoint, content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✓ Successfully uploaded game {summary.MatchId.Substring(0, 8)}");
                        
                        // Mark file as uploaded by renaming it
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && !filePath.EndsWith("-up.json"))
                        {
                            try
                            {
                                var newPath = filePath.Replace(".json", "-up.json");
                                File.Move(filePath, newPath);
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
                        var responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Failed to upload game (attempt {attempt}/{MaxRetries}): {response.StatusCode}");
                        
                        if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine($"  Retrying in {RetryDelayMs}ms...");
                            await Task.Delay(RetryDelayMs, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error sending stats (attempt {attempt}/{MaxRetries}): {ex.Message}");
                    
                    if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"  Retrying in {RetryDelayMs}ms...");
                        await Task.Delay(RetryDelayMs, cancellationToken);
                    }
                }
            }
            
            Console.WriteLine($"✗ Failed to upload after {MaxRetries} attempts");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _uploadSemaphore.Release();
        }
    }

    /// <summary>
    /// Batch sends multiple game summaries
    /// </summary>
    public static async Task<int> SendBatchStatsAsync(List<GameSummary> summaries, CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        
        foreach (var summary in summaries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            if (await SendGameStatsAsync(summary, null, cancellationToken))
            {
                successCount++;
            }
        }

        return successCount;
    }
}

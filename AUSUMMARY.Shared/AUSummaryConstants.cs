using System;
using System.IO;

namespace AUSUMMARY.Shared;

/// <summary>
/// Shared constants and configuration
/// </summary>
public static class AUSummaryConstants
{
    /// <summary>
    /// Version of the AUSUMMARY system
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Folder name for storing summaries
    /// </summary>
    public const string SummaryFolderName = "AmongUsGameSummaries";

    /// <summary>
    /// File extension for summary files
    /// </summary>
    public const string SummaryFileExtension = ".json";

    /// <summary>
    /// Get the full path to the summaries folder
    /// </summary>
    public static string GetSummariesPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var summariesPath = Path.Combine(documentsPath, SummaryFolderName);
        
        // Ensure directory exists
        if (!Directory.Exists(summariesPath))
        {
            Directory.CreateDirectory(summariesPath);
        }
        
        return summariesPath;
    }

    /// <summary>
    /// Get the filename for a game summary
    /// </summary>
    public static string GetSummaryFileName(string matchId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"GameSummary_{timestamp}_{matchId.Substring(0, 8)}{SummaryFileExtension}";
    }

    /// <summary>
    /// Get the full path for a game summary file
    /// </summary>
    public static string GetSummaryFilePath(string matchId)
    {
        return Path.Combine(GetSummariesPath(), GetSummaryFileName(matchId));
    }
}

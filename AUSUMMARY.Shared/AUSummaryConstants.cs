using System;
using System.IO;

namespace AUSUMMARY.Shared;


public static class AUSummaryConstants
{

    public const string Version = "1.0.0";


    public const string SummaryFolderName = "AmongUsGameSummaries";


    public const string SummaryFileExtension = ".json";


    public static string GetSummariesPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var summariesPath = Path.Combine(documentsPath, SummaryFolderName);
        
    
        if (!Directory.Exists(summariesPath))
        {
            Directory.CreateDirectory(summariesPath);
        }
        
        return summariesPath;
    }


    public static string GetSummaryFileName(string matchId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"GameSummary_{timestamp}_{matchId.Substring(0, 8)}{SummaryFileExtension}";
    }


    public static string GetSummaryFilePath(string matchId)
    {
        return Path.Combine(GetSummariesPath(), GetSummaryFileName(matchId));
    }
}

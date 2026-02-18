using System;
using System.IO;

namespace AUSUMMARY.Shared;


public static class AUSummaryConstants
{

    public const string Version = "1.1.0";


    public const string SummaryFolderName = "AUSUMMARYNewSummarys";


    public const string SummaryFileExtension = ".json";


    public static string GetSummariesPath()
    {
        try
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var summariesPath = Path.Combine(documentsPath, SummaryFolderName);
            
            // Try to create directory if it doesn't exist
            if (!Directory.Exists(summariesPath))
            {
                Directory.CreateDirectory(summariesPath);
            }
            
            return summariesPath;
        }
        catch (Exception ex)
        {
            // Fallback to temp directory if Documents fails
            var tempPath = Path.Combine(Path.GetTempPath(), SummaryFolderName);
            
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            
            return tempPath;
        }
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

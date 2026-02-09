using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AUSUMMARY.Shared;

/// <summary>
/// Checks for updates from the GitHub repository
/// </summary>
public static class UpdateChecker
{
    private const string GithubApiUrl = "https://api.github.com/repos/notnico22/AUSummary/releases/latest";
    private const string GithubReleasesUrl = "https://github.com/notnico22/AUSummary/releases/latest";

    public class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    /// <summary>
    /// Checks if a newer version is available on GitHub
    /// </summary>
    public static async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = AUSummaryConstants.Version,
            DownloadUrl = GithubReleasesUrl
        };

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AUSummary-UpdateChecker");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetStringAsync(GithubApiUrl, cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                return info;
            
            var release = JObject.Parse(response);

            var tagName = release["tag_name"]?.ToString() ?? "";
            var latestVersion = tagName.TrimStart('v');
            
            info.LatestVersion = latestVersion;
            info.ReleaseNotes = release["body"]?.ToString() ?? "";
            info.DownloadUrl = release["html_url"]?.ToString() ?? GithubReleasesUrl;

            // Compare versions
            info.UpdateAvailable = CompareVersions(info.CurrentVersion, latestVersion) < 0;

            return info;
        }
        catch (OperationCanceledException)
        {
            // Update check was cancelled
            return info;
        }
        catch (Exception ex)
        {
            // If update check fails, continue without update
            Console.WriteLine($"Update check failed: {ex.Message}");
            return info;
        }
    }

    /// <summary>
    /// Compares two semantic versions (e.g., "1.0.0" vs "1.1.0")
    /// Returns: -1 if v1 < v2, 0 if equal, 1 if v1 > v2
    /// </summary>
    private static int CompareVersions(string v1, string v2)
    {
        try
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                int num1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
                int num2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;

                if (num1 < num2) return -1;
                if (num1 > num2) return 1;
            }

            return 0;
        }
        catch
        {
            return 0; // If comparison fails, assume versions are equal
        }
    }
}

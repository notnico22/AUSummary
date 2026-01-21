using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AUSUMMARY.Shared;
using AUSUMMARY.Shared.Models;
using Newtonsoft.Json;

namespace AUSUMMARY.Viewer;

/// <summary>
/// Player role statistics for displaying in search
/// </summary>
public class RoleStats
{
    public string RoleName { get; set; } = "";
    public int GamesPlayed { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Enhanced player statistics
/// </summary>
public class EnhancedPlayerStats
{
    public string PlayerName { get; set; } = "";
    public int TotalGames { get; set; }
    public int Wins { get; set; }
    public double WinRate { get; set; }
    public int TotalKills { get; set; }
    public int TotalDeaths { get; set; }
    public List<RoleStats> RoleBreakdown { get; set; } = new();
}

public partial class MainWindow : Window
{
    private ObservableCollection<GameSummary> _gameSummaries = [];
    private ObservableCollection<GameSummary> _filteredGames = [];
    private FileSystemWatcher? _watcher;
    private GameSummary? _currentGame;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            
            if (AllGamesView != null) AllGamesView.Visibility = Visibility.Visible;
            if (GameDetailsView != null) GameDetailsView.Visibility = Visibility.Collapsed;
            if (PlayerSearchView != null) PlayerSearchView.Visibility = Visibility.Collapsed;
            
            LoadGameSummaries();
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region Window Controls

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Navigation

    private void ShowAllGames_Click(object sender, RoutedEventArgs e)
    {
        if (AllGamesView != null) AllGamesView.Visibility = Visibility.Visible;
        if (GameDetailsView != null) GameDetailsView.Visibility = Visibility.Collapsed;
        if (PlayerSearchView != null) PlayerSearchView.Visibility = Visibility.Collapsed;
        if (StatusText != null) StatusText.Text = "Browsing all games";
    }

    private void ShowMostRecent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_gameSummaries == null || _gameSummaries.Count == 0)
            {
                if (StatusText != null) StatusText.Text = "No games available";
                MessageBox.Show("No games found! Play some Among Us first.", "No Games", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var mostRecent = _gameSummaries.OrderByDescending(g => g.Timestamp).FirstOrDefault();
            if (mostRecent != null)
            {
                ShowGameDetails(mostRecent);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing most recent game: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowPlayerSearch_Click(object sender, RoutedEventArgs e)
    {
        if (AllGamesView != null) AllGamesView.Visibility = Visibility.Collapsed;
        if (GameDetailsView != null) GameDetailsView.Visibility = Visibility.Collapsed;
        if (PlayerSearchView != null) PlayerSearchView.Visibility = Visibility.Visible;
        if (StatusText != null) StatusText.Text = "Search for a player";
    }

    private void BackToGames_Click(object sender, RoutedEventArgs e)
    {
        ShowAllGames_Click(sender, e);
    }

    #endregion

    #region Data Loading

    private void LoadGameSummaries()
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            
            if (!Directory.Exists(summariesPath))
            {
                Directory.CreateDirectory(summariesPath);
                if (StatusText != null)
                    StatusText.Text = "Created summaries folder";
                return;
            }

            var files = Directory.GetFiles(summariesPath, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            _gameSummaries.Clear();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var summary = JsonConvert.DeserializeObject<GameSummary>(json);
                    if (summary != null)
                    {
                        foreach (var player in summary.Players)
                        {
                            player.Modifiers ??= [];
                            player.KillType ??= "";
                            player.KilledBy ??= "";
                            player.DeathCause ??= "";
                        }
                        
                        _gameSummaries.Add(summary);
                    }
                }
                catch { }
            }

            ApplyFilters();
            UpdateStatistics();
            
            if (StatusText != null)
                StatusText.Text = $"Loaded {_gameSummaries.Count} games";
        }
        catch (Exception ex)
        {
            if (StatusText != null)
                StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void SetupFileWatcher()
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            
            if (!Directory.Exists(summariesPath))
                return;

            _watcher = new FileSystemWatcher(summariesPath)
            {
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Created += OnNewSummaryCreated;
            _watcher.EnableRaisingEvents = true;
        }
        catch { }
    }

    private void OnNewSummaryCreated(object sender, FileSystemEventArgs e)
    {
        System.Threading.Thread.Sleep(500);

        Dispatcher.Invoke(() =>
        {
            try
            {
                var json = File.ReadAllText(e.FullPath);
                var summary = JsonConvert.DeserializeObject<GameSummary>(json);
                if (summary != null)
                {
                    foreach (var player in summary.Players)
                    {
                        player.Modifiers ??= [];
                        player.KillType ??= "";
                        player.KilledBy ??= "";
                        player.DeathCause ??= "";
                    }
                    
                    _gameSummaries.Insert(0, summary);
                    ApplyFilters();
                    UpdateStatistics();
                    if (StatusText != null)
                        StatusText.Text = "🎮 New game added!";
                }
            }
            catch { }
        });
    }

    #endregion

    #region Filtering and Statistics

    private void ApplyFilters()
    {
        try
        {
            var filtered = _gameSummaries.AsEnumerable();

            if (CbMapFilter != null && CbMapFilter.SelectedIndex > 0)
            {
                var selectedMap = (CbMapFilter.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (selectedMap != null)
                    filtered = filtered.Where(g => g.Metadata.MapName == selectedMap);
            }

            if (CbWinnerFilter != null && CbWinnerFilter.SelectedIndex > 0)
            {
                filtered = CbWinnerFilter.SelectedIndex switch
                {
                    1 => filtered.Where(g => g.Winner.WinningTeam == "Crewmate"),
                    2 => filtered.Where(g => g.Winner.WinningTeam == "Impostor"),
                    _ => filtered
                };
            }

            filtered = filtered.OrderByDescending(g => g.Timestamp);

            _filteredGames = [.. filtered];
            
            if (GamesGrid != null)
                GamesGrid.ItemsSource = _filteredGames;

            if (NoGamesPanel != null)
                NoGamesPanel.Visibility = _filteredGames.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Filter error: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStatistics()
    {
        try
        {
            if (_gameSummaries.Count == 0)
            {
                if (TxtTotalGames != null) TxtTotalGames.Text = "0";
                if (TxtCrewWinRate != null) TxtCrewWinRate.Text = "0%";
                if (TxtImpWinRate != null) TxtImpWinRate.Text = "0%";
                return;
            }

            var totalGames = _gameSummaries.Count;
            var crewWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Crewmate");
            var impWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Impostor");

            if (TxtTotalGames != null) TxtTotalGames.Text = totalGames.ToString();
            if (TxtCrewWinRate != null) TxtCrewWinRate.Text = $"{(crewWins * 100.0 / totalGames):F0}%";
            if (TxtImpWinRate != null) TxtImpWinRate.Text = $"{(impWins * 100.0 / totalGames):F0}%";
        }
        catch { }
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (CbMapFilter != null) CbMapFilter.SelectedIndex = 0;
        if (CbWinnerFilter != null) CbWinnerFilter.SelectedIndex = 0;
        if (StatusText != null) StatusText.Text = "Filters cleared";
    }

    #endregion

    #region Game Details

    private void GameCard_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is Border border && border.DataContext is GameSummary summary)
            {
                ShowGameDetails(summary);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowGameDetails(GameSummary summary)
    {
        try
        {
            if (summary == null) return;
            
            _currentGame = summary;
            
            if (AllGamesView != null) AllGamesView.Visibility = Visibility.Collapsed;
            if (GameDetailsView != null) GameDetailsView.Visibility = Visibility.Visible;
            if (PlayerSearchView != null) PlayerSearchView.Visibility = Visibility.Collapsed;

            if (TxtDetailMapName != null) TxtDetailMapName.Text = summary.Metadata.MapName;
            if (TxtDetailTimestamp != null) TxtDetailTimestamp.Text = summary.Timestamp.ToString("MMM dd, yyyy - HH:mm");
            if (TxtDetailWinner != null) TxtDetailWinner.Text = summary.Winner.WinningTeam;
            if (TxtDetailCondition != null) TxtDetailCondition.Text = summary.Winner.WinCondition;

            if (TxtDetailDuration != null) TxtDetailDuration.Text = summary.Metadata.GameDuration.ToString(@"mm\:ss");
            if (TxtDetailPlayers != null) TxtDetailPlayers.Text = summary.Metadata.PlayerCount.ToString();
            if (TxtDetailKills != null) TxtDetailKills.Text = summary.Statistics.TotalKills.ToString();
            if (TxtDetailTasks != null) TxtDetailTasks.Text = $"{(summary.Statistics.TaskCompletionRate * 100):F0}%";

            if (PlayersListDetail != null) PlayersListDetail.ItemsSource = summary.Players;

            if (StatusText != null) StatusText.Text = $"Viewing game from {summary.Timestamp:MMM dd, HH:mm}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing details: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// NEW: Show player details popup when clicking a player in game view
    /// </summary>
    private void PlayerCard_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is Border border && border.DataContext is PlayerSnapshot player)
            {
                ShowPlayerDetailsPopup(player);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing player details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowPlayerDetailsPopup(PlayerSnapshot player)
    {
        var statusText = player.IsAlive ? "✅ Alive" : player.WasEjected ? "🚀 Ejected" : "💀 Dead";
        
        var details = $"Player: {player.PlayerName}\n" +
                     $"Role: {player.Role} ({player.Team})\n" +
                     $"Status: {statusText}\n\n";

        if (!player.IsAlive)
        {
            details += $"Death Details:\n";
            if (player.TimeOfDeath.HasValue)
                details += $"  ⏰ Time: {player.TimeOfDeath.Value:F1}s\n";
            if (!string.IsNullOrEmpty(player.KilledBy))
                details += $"  🔪 Killed By: {player.KilledBy}\n";
            if (!string.IsNullOrEmpty(player.KillType))
                details += $"  💀 Kill Type: {player.KillType}\n";
            if (player.WasEjected)
                details += $"  🚀 Ejected during meeting\n";
        }

        details += $"\nGame Stats:\n";
        details += $"  🔪 Kills: {player.KillCount}\n";
        details += $"  ✅ Tasks: {player.TasksCompleted}/{player.TotalTasks}\n";

        if (player.Modifiers.Any())
        {
            details += $"\n🎭 Modifiers: {string.Join(", ", player.Modifiers)}\n";
        }

        MessageBox.Show(details, $"{player.PlayerName} Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Player Search

    private void SearchPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (TxtPlayerSearch == null) return;
        
        var playerName = TxtPlayerSearch.Text.Trim();
        
        if (string.IsNullOrEmpty(playerName))
        {
            if (StatusText != null) StatusText.Text = "Please enter a player name";
            return;
        }

        ShowPlayerStats(playerName);
    }

    private void ShowPlayerStats(string playerName)
    {
        try
        {
            var playerGames = _gameSummaries
                .Where(g => g.Players.Any(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (playerGames.Count == 0)
            {
                if (PlayerStatsPanel != null) PlayerStatsPanel.Visibility = Visibility.Collapsed;
                if (StatusText != null) StatusText.Text = $"No results for '{playerName}'";
                MessageBox.Show($"No games found for player: {playerName}", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (PlayerStatsPanel != null) PlayerStatsPanel.Visibility = Visibility.Visible;
            if (TxtSearchPlayerName != null) TxtSearchPlayerName.Text = playerName;

            // Calculate enhanced statistics
            var stats = CalculateEnhancedPlayerStats(playerName, playerGames);

            // Display basic stats
            if (TxtSearchGamesPlayed != null) TxtSearchGamesPlayed.Text = stats.TotalGames.ToString();
            if (TxtSearchWinRate != null) TxtSearchWinRate.Text = $"{stats.WinRate:F0}%";
            if (TxtSearchKills != null) TxtSearchKills.Text = stats.TotalKills.ToString();
            if (TxtSearchDeaths != null) TxtSearchDeaths.Text = stats.TotalDeaths.ToString();

            // Display role breakdown
            if (RoleBreakdownList != null) RoleBreakdownList.ItemsSource = stats.RoleBreakdown;

            if (StatusText != null) StatusText.Text = $"Found {stats.TotalGames} games for '{playerName}'";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing player stats: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private EnhancedPlayerStats CalculateEnhancedPlayerStats(string playerName, List<GameSummary> playerGames)
    {
        var stats = new EnhancedPlayerStats
        {
            PlayerName = playerName,
            TotalGames = playerGames.Count
        };

        var roleCounts = new Dictionary<string, int>();

        foreach (var game in playerGames)
        {
            var player = game.Players.First(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            
            // Count wins
            if (game.Winner.Winners.Contains(player.PlayerName, StringComparer.OrdinalIgnoreCase))
                stats.Wins++;

            // Count deaths
            if (!player.IsAlive)
                stats.TotalDeaths++;

            // Sum kills
            stats.TotalKills += player.KillCount;

            // Count roles
            if (!string.IsNullOrEmpty(player.Role))
            {
                if (!roleCounts.ContainsKey(player.Role))
                    roleCounts[player.Role] = 0;
                roleCounts[player.Role]++;
            }
        }

        stats.WinRate = (stats.Wins * 100.0 / stats.TotalGames);

        // Convert role counts to RoleStats with percentages
        stats.RoleBreakdown = roleCounts
            .OrderByDescending(r => r.Value)
            .Select(r => new RoleStats
            {
                RoleName = r.Key,
                GamesPlayed = r.Value,
                Percentage = (r.Value * 100.0 / stats.TotalGames)
            })
            .ToList();

        return stats;
    }

    #endregion

    #region Tools and Actions

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = AUSummaryConstants.GetSummariesPath();
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                if (StatusText != null) StatusText.Text = "Opened summaries folder";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshData_Click(object sender, RoutedEventArgs e)
    {
        LoadGameSummaries();
        if (StatusText != null) StatusText.Text = "Data refreshed";
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _watcher?.Dispose();
        base.OnClosed(e);
    }
}

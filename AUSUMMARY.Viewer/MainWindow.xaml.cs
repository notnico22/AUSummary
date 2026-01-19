using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AUSUMMARY.Shared;
using AUSUMMARY.Shared.Models;
using Newtonsoft.Json;

namespace AUSUMMARY.Viewer;

public partial class MainWindow : Window
{
    private ObservableCollection<GameSummary> _gameSummaries = new();
    private ObservableCollection<GameSummary> _filteredGames = new();
    private FileSystemWatcher? _watcher;
    private GameSummary? _currentGame;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            LoadGameSummaries();
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = AUSummaryConstants.GetSummariesPath();
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
                StatusText.Text = "Opened summaries folder";
            }
            else
            {
                MessageBox.Show("Summaries folder not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearData_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to delete ALL game data? This cannot be undone!", 
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var path = AUSummaryConstants.GetSummariesPath();
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*.json"))
                    {
                        File.Delete(file);
                    }
                    _gameSummaries.Clear();
                    ApplyFilters();
                    UpdateStatistics();
                    StatusText.Text = "All data cleared";
                    MessageBox.Show("All game data has been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportStats_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("AUSUMMARY - Game Statistics Export");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Total Games: {_gameSummaries.Count}");
            
            if (_gameSummaries.Count > 0)
            {
                var crewWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Crewmate");
                var impWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Impostor");
                sb.AppendLine($"Crewmate Wins: {crewWins} ({crewWins * 100.0 / _gameSummaries.Count:F1}%)");
                sb.AppendLine($"Impostor Wins: {impWins} ({impWins * 100.0 / _gameSummaries.Count:F1}%)");
                sb.AppendLine();
                
                sb.AppendLine("Games:");
                foreach (var game in _gameSummaries)
                {
                    sb.AppendLine($"  {game.Timestamp:yyyy-MM-dd HH:mm} | {game.Metadata.MapName} | Winner: {game.Winner.WinningTeam}");
                }
            }

            var exportPath = Path.Combine(AUSummaryConstants.GetSummariesPath(), $"export_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(exportPath, sb.ToString());
            StatusText.Text = $"Stats exported to {Path.GetFileName(exportPath)}";
            MessageBox.Show($"Statistics exported successfully!\n\n{exportPath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyMatchID_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGame != null)
        {
            Clipboard.SetText(_currentGame.MatchId);
            StatusText.Text = "Match ID copied to clipboard";
        }
    }

    private void DeleteGame_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGame == null) return;

        var result = MessageBox.Show($"Delete this game?\n\nMatch ID: {_currentGame.MatchId[..8]}\nTime: {_currentGame.Timestamp:yyyy-MM-dd HH:mm}", 
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var files = Directory.GetFiles(AUSummaryConstants.GetSummariesPath(), "*.json");
                foreach (var file in files)
                {
                    var json = File.ReadAllText(file);
                    var game = JsonConvert.DeserializeObject<GameSummary>(json);
                    if (game?.MatchId == _currentGame.MatchId)
                    {
                        File.Delete(file);
                        _gameSummaries.Remove(_currentGame);
                        ApplyFilters();
                        UpdateStatistics();
                        ViewAllGames_Click(null!, null!);
                        StatusText.Text = "Game deleted";
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ViewLeaderboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GamesGridView.Visibility = Visibility.Collapsed;
            DetailsView.Visibility = Visibility.Collapsed;
            PlayerStatsView.Visibility = Visibility.Collapsed;
            LeaderboardView.Visibility = Visibility.Visible;

            // Collect player statistics
            var playerStats = new Dictionary<string, (int games, int wins, int kills, int deaths, int tasks)>();

            foreach (var game in _gameSummaries)
            {
                foreach (var player in game.Players)
                {
                    if (!playerStats.ContainsKey(player.PlayerName))
                    {
                        playerStats[player.PlayerName] = (0, 0, 0, 0, 0);
                    }

                    var stats = playerStats[player.PlayerName];
                    stats.games++;
                    
                    if (game.Winner.Winners.Contains(player.PlayerName, StringComparer.OrdinalIgnoreCase))
                        stats.wins++;
                    
                    if (!player.IsAlive)
                        stats.deaths++;
                    
                    stats.tasks += player.TasksCompleted;

                    var kills = game.Events.Count(e => 
                        e.EventType == "PlayerKilled" && 
                        e.Description.Contains($"by {player.PlayerName}"));
                    stats.kills += kills;

                    playerStats[player.PlayerName] = stats;
                }
            }

            var leaderboard = playerStats
                .Select(kvp => new 
                { 
                    Player = kvp.Key, 
                    Games = kvp.Value.games,
                    Wins = kvp.Value.wins,
                    WinRate = kvp.Value.games > 0 ? kvp.Value.wins * 100.0 / kvp.Value.games : 0,
                    Kills = kvp.Value.kills,
                    Deaths = kvp.Value.deaths,
                    Tasks = kvp.Value.tasks
                })
                .OrderByDescending(p => p.WinRate)
                .ThenByDescending(p => p.Games)
                .Take(20)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("TOP 20 PLAYERS BY WIN RATE");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            int rank = 1;
            foreach (var player in leaderboard)
            {
                var medal = rank <= 3 ? (rank == 1 ? "🥇" : rank == 2 ? "🥈" : "🥉") : $"#{rank}";
                sb.AppendLine($"{medal} {player.Player}");
                sb.AppendLine($"    Games: {player.Games} | Wins: {player.Wins} ({player.WinRate:F1}%)");
                sb.AppendLine($"    Kills: {player.Kills} | Deaths: {player.Deaths} | Tasks: {player.Tasks}");
                sb.AppendLine();
                rank++;
            }

            LeaderboardText.Text = sb.ToString();
            StatusText.Text = $"Showing top {leaderboard.Count} players";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating leaderboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadGameSummaries()
    {
        try
        {
            var summariesPath = AUSummaryConstants.GetSummariesPath();
            
            if (!Directory.Exists(summariesPath))
            {
                if (StatusText != null)
                    StatusText.Text = "No summaries folder found";
                return;
            }

            var files = Directory.GetFiles(summariesPath, $"*{AUSummaryConstants.SummaryFileExtension}")
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
                Filter = $"*{AUSummaryConstants.SummaryFileExtension}",
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
                    _gameSummaries.Insert(0, summary);
                    ApplyFilters();
                    UpdateStatistics();
                    if (StatusText != null)
                        StatusText.Text = "New game added!";
                }
            }
            catch { }
        });
    }

    private void ApplyFilters()
    {
        try
        {
            var filtered = _gameSummaries.AsEnumerable();

            // Map filter
            if (MapFilter != null && MapFilter.SelectedIndex > 0)
            {
                var selectedMap = (MapFilter.SelectedItem as ComboBoxItem)?.Content.ToString();
                filtered = filtered.Where(g => g.Metadata.MapName == selectedMap);
            }

            // Winner filter
            if (WinnerFilter != null && WinnerFilter.SelectedIndex > 0)
            {
                var selectedWinner = WinnerFilter.SelectedIndex == 1 ? "Crewmate" : "Impostor";
                filtered = filtered.Where(g => g.Winner.WinningTeam == selectedWinner);
            }

            // Player count filter
            if (PlayerCountFilter != null && PlayerCountFilter.SelectedIndex > 0)
            {
                filtered = PlayerCountFilter.SelectedIndex switch
                {
                    1 => filtered.Where(g => g.Metadata.PlayerCount <= 4),
                    2 => filtered.Where(g => g.Metadata.PlayerCount >= 5 && g.Metadata.PlayerCount <= 8),
                    3 => filtered.Where(g => g.Metadata.PlayerCount >= 9),
                    _ => filtered
                };
            }

            // Sort
            if (SortFilter != null)
            {
                filtered = SortFilter.SelectedIndex switch
                {
                    0 => filtered.OrderByDescending(g => g.Timestamp),
                    1 => filtered.OrderBy(g => g.Timestamp),
                    2 => filtered.OrderByDescending(g => g.Metadata.GameDuration),
                    3 => filtered.OrderBy(g => g.Metadata.GameDuration),
                    _ => filtered
                };
            }

            _filteredGames = new ObservableCollection<GameSummary>(filtered);
            
            if (GamesGrid != null)
                GamesGrid.ItemsSource = _filteredGames;
            
            if (GameCountText != null)
                GameCountText.Text = $"{_filteredGames.Count} game{(_filteredGames.Count != 1 ? "s" : "")}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Filter error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStatistics()
    {
        try
        {
            if (_gameSummaries.Count == 0)
            {
                if (TotalGamesText != null) TotalGamesText.Text = "Total Games: 0";
                if (CrewWinRateText != null) CrewWinRateText.Text = "Crew Win Rate: 0%";
                if (ImpWinRateText != null) ImpWinRateText.Text = "Imp Win Rate: 0%";
                if (AvgDurationText != null) AvgDurationText.Text = "Avg Duration: 0:00";
                return;
            }

            var totalGames = _gameSummaries.Count;
            var crewWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Crewmate");
            var impWins = _gameSummaries.Count(g => g.Winner.WinningTeam == "Impostor");
            var avgDuration = TimeSpan.FromSeconds(_gameSummaries.Average(g => g.Metadata.GameDuration.TotalSeconds));

            if (TotalGamesText != null) TotalGamesText.Text = $"Total Games: {totalGames}";
            if (CrewWinRateText != null) CrewWinRateText.Text = $"Crew Win Rate: {(crewWins * 100.0 / totalGames):F1}%";
            if (ImpWinRateText != null) ImpWinRateText.Text = $"Imp Win Rate: {(impWins * 100.0 / totalGames):F1}%";
            if (AvgDurationText != null) AvgDurationText.Text = $"Avg Duration: {avgDuration:mm\\:ss}";
        }
        catch { }
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (MapFilter != null) MapFilter.SelectedIndex = 0;
        if (WinnerFilter != null) WinnerFilter.SelectedIndex = 0;
        if (PlayerCountFilter != null) PlayerCountFilter.SelectedIndex = 0;
        if (SortFilter != null) SortFilter.SelectedIndex = 0;
        if (StatusText != null) StatusText.Text = "Filters cleared";
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox != null && SearchBox.Text == "Enter player name...")
        {
            SearchBox.Text = "";
        }
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox != null && string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            SearchBox.Text = "Enter player name...";
        }
    }

    private void ViewMostRecent_Click(object sender, RoutedEventArgs e)
    {
        if (_gameSummaries.Count == 0)
        {
            if (StatusText != null) StatusText.Text = "No games available";
            return;
        }

        ShowGameDetails(_gameSummaries[0]);
    }

    private void ViewAllGames_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGridView != null) GamesGridView.Visibility = Visibility.Visible;
        if (DetailsView != null) DetailsView.Visibility = Visibility.Collapsed;
        if (PlayerStatsView != null) PlayerStatsView.Visibility = Visibility.Collapsed;
        if (LeaderboardView != null) LeaderboardView.Visibility = Visibility.Collapsed;
        if (StatusText != null) StatusText.Text = "Browsing all games";
    }

    private void GameCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is GameSummary summary)
        {
            ShowGameDetails(summary);
        }
    }

    private void GameCard_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
        }
    }

    private void GameCard_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"));
        }
    }

    private void SearchPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (SearchBox == null) return;
        
        var playerName = SearchBox.Text.Trim();
        
        if (string.IsNullOrEmpty(playerName) || playerName == "Enter player name...")
        {
            if (StatusText != null) StatusText.Text = "Please enter a player name";
            return;
        }

        ShowPlayerStats(playerName);
    }

    private void ShowGameDetails(GameSummary summary)
    {
        try
        {
            _currentGame = summary;
            
            if (GamesGridView != null) GamesGridView.Visibility = Visibility.Collapsed;
            if (DetailsView != null) DetailsView.Visibility = Visibility.Visible;
            if (PlayerStatsView != null) PlayerStatsView.Visibility = Visibility.Collapsed;
            if (LeaderboardView != null) LeaderboardView.Visibility = Visibility.Collapsed;

            var playersDetails = string.Join("\n\n", summary.Players.Select(p =>
            {
                var status = p.IsAlive ? "✅ ALIVE" : "💀 DEAD";
                var tasks = p.Team == "Impostor" ? "" : $"\n    Tasks: {p.TasksCompleted}/{p.TotalTasks}";
                var death = !p.IsAlive && !string.IsNullOrEmpty(p.KilledBy) 
                    ? $"\n    Killed by: {p.KilledBy}" 
                    : "";
                
                return $"  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"  {p.PlayerName} ({p.ColorName})\n" +
                       $"  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"    Status: {status}\n" +
                       $"    Role: {p.Role}\n" +
                       $"    Team: {p.Team}{tasks}{death}";
            }));

            var details = $"🎮 MATCH SUMMARY\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                         $"📋 Match ID: {summary.MatchId[..8]}\n" +
                         $"⏰ Time: {summary.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                         $"🗺️ Map: {summary.Metadata.MapName}\n" +
                         $"⏱️ Duration: {summary.Metadata.GameDuration:mm\\:ss}\n\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                         $"📊 GAME STATS\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                         $"👥 Players: {summary.Metadata.PlayerCount}\n" +
                         $"📋 Meetings: {summary.Metadata.TotalMeetings}\n" +
                         $"✅ Tasks: {summary.Metadata.CompletedTasks}/{summary.Metadata.TotalTasks} ({summary.Statistics.TaskCompletionRate:P0})\n" +
                         $"💀 Deaths: {summary.Statistics.TotalKills} kills, {summary.Statistics.TotalEjections} ejections\n\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                         $"🏆 WINNER\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                         $"Team: {summary.Winner.WinningTeam}\n" +
                         $"Condition: {summary.Winner.WinCondition}\n\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                         $"👥 PLAYERS\n" +
                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                         $"{playersDetails}";

            if (DetailsText != null) DetailsText.Text = details;
            if (StatusText != null) StatusText.Text = $"Viewing game from {summary.Timestamp:MMM dd, HH:mm}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowPlayerStats(string playerName)
    {
        try
        {
            if (GamesGridView != null) GamesGridView.Visibility = Visibility.Collapsed;
            if (DetailsView != null) DetailsView.Visibility = Visibility.Collapsed;
            if (PlayerStatsView != null) PlayerStatsView.Visibility = Visibility.Visible;
            if (LeaderboardView != null) LeaderboardView.Visibility = Visibility.Collapsed;

            var playerGames = _gameSummaries
                .Where(g => g.Players.Any(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (playerGames.Count == 0)
            {
                if (PlayerStatsText != null) PlayerStatsText.Text = $"No games found for player: {playerName}";
                if (StatusText != null) StatusText.Text = $"No results for '{playerName}'";
                return;
            }

            int totalGames = playerGames.Count;
            int gamesAsCrewmate = 0;
            int gamesAsImpostor = 0;
            int gamesAsNeutral = 0;
            int wins = 0;
            int losses = 0;
            int deaths = 0;
            int survived = 0;
            int totalKills = 0;
            int totalTasksCompleted = 0;
            int totalTasks = 0;
            var rolesPlayed = new Dictionary<string, int>();

            foreach (var game in playerGames)
            {
                var player = game.Players.First(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                
                if (player.Team == "Crewmate") gamesAsCrewmate++;
                else if (player.Team == "Impostor") gamesAsImpostor++;
                else gamesAsNeutral++;

                if (game.Winner.Winners.Contains(player.PlayerName, StringComparer.OrdinalIgnoreCase))
                    wins++;
                else
                    losses++;

                if (!player.IsAlive) deaths++;
                else survived++;

                totalTasksCompleted += player.TasksCompleted;
                totalTasks += player.TotalTasks;

                if (!rolesPlayed.ContainsKey(player.Role))
                    rolesPlayed[player.Role] = 0;
                rolesPlayed[player.Role]++;

                totalKills += game.Events.Count(e => 
                    e.EventType == "PlayerKilled" && 
                    e.Description.Contains($"by {player.PlayerName}"));
            }

            var winRate = (wins * 100.0 / totalGames);
            var deathRate = (deaths * 100.0 / totalGames);
            var taskCompletion = totalTasks > 0 ? (totalTasksCompleted * 100.0 / totalTasks) : 0;

            var rolesPlayedText = string.Join("\n", rolesPlayed.OrderByDescending(r => r.Value)
                .Select(r => $"  {r.Key}: {r.Value} game{(r.Value > 1 ? "s" : "")}"));

            var stats = $"👤 PLAYER: {playerName}\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"📊 OVERALL STATISTICS\n\n" +
                       $"Total Games Played: {totalGames}\n" +
                       $"Wins: {wins} ({winRate:F1}%)\n" +
                       $"Losses: {losses}\n\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"🎭 TEAM BREAKDOWN\n\n" +
                       $"Crewmate: {gamesAsCrewmate} game{(gamesAsCrewmate != 1 ? "s" : "")}\n" +
                       $"Impostor: {gamesAsImpostor} game{(gamesAsImpostor != 1 ? "s" : "")}\n" +
                       $"Neutral: {gamesAsNeutral} game{(gamesAsNeutral != 1 ? "s" : "")}\n\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"💀 SURVIVAL STATS\n\n" +
                       $"Deaths: {deaths} ({deathRate:F1}%)\n" +
                       $"Survived: {survived}\n" +
                       $"Kills: {totalKills}\n\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"✅ TASK STATISTICS\n\n" +
                       $"Tasks Completed: {totalTasksCompleted}/{totalTasks}\n" +
                       $"Completion Rate: {taskCompletion:F1}%\n\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"🎭 ROLES PLAYED\n\n" +
                       $"{rolesPlayedText}\n\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                       $"🎮 RECENT GAMES\n\n" +
                       string.Join("\n", playerGames.Take(5).Select(g =>
                       {
                           var p = g.Players.First(pl => pl.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                           var won = g.Winner.Winners.Contains(p.PlayerName, StringComparer.OrdinalIgnoreCase);
                           return $"  {g.Timestamp:MMM dd HH:mm} - {p.Role} - {(won ? "✅ WIN" : "❌ LOSS")}";
                       }));

            if (PlayerStatsText != null) PlayerStatsText.Text = stats;
            if (StatusText != null) StatusText.Text = $"Viewing stats for '{playerName}' - {totalGames} games found";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error showing player stats: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadGameSummaries();
    }

    protected override void OnClosed(EventArgs e)
    {
        _watcher?.Dispose();
        base.OnClosed(e);
    }
}

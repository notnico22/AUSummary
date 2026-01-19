# AUSUMMARY 📊

# CURRENTLY IN BETA

**Advanced Statistics Logger and Viewer for Among Us (Town of Us Mod)**

AUSUMMARY is a comprehensive BepInEx mod and statistics viewer that automatically tracks, records, and analyzes your Among Us games. Built specifically for Town of Us modded lobbies, it captures detailed game data including roles, kills, tasks, and much more, then presents it through a beautiful, feature-rich desktop viewer application.

**Video of AUSummary**

https://github.com/user-attachments/assets/ff876d20-7d23-466b-9d2c-b117b85408e6


---

## 🎮 Features Overview

### Core Functionality
- **Automatic Game Tracking**: Captures game data automatically without any manual input required
- **Real-Time Data Collection**: Records events as they happen during gameplay
- **Persistent Storage**: Saves all games as JSON files for permanent record-keeping
- **Live Viewer Updates**: Statistics viewer updates automatically when new games are played

---

## 📦 Components

### 1. AUSUMMARY.DLL (BepInEx Mod)
The game mod that runs inside Among Us and captures all game data.

**What It Tracks:**
- ✅ Player names and colors
- ✅ Roles (Sheriff, Jester, Vigilante, Grenadier, etc.)
- ✅ Teams (Crewmate, Impostor, Neutral)
- ✅ Game start and end times
- ✅ Game duration
- ✅ Map name
- ✅ Kill events (who killed who and when)
- ✅ Ejection/voting events
- ✅ Death causes and times
- ✅ Task completion statistics
- ✅ Meeting counts
- ✅ Win conditions and winners
- ✅ Player survival status

**Technical Details:**
- Uses Harmony patches to hook into game events
- Captures role data from Town of Us mod
- Records events with precise timestamps
- Automatically saves JSON summaries on game end
- Supports neutral roles (Jester, Arsonist, Glitch, etc.)

### 2. AUSUMMARY.Viewer (Desktop Application)
A modern WPF desktop application for viewing and analyzing your game statistics.

---

## 🎯 Viewer Features

### 📋 Game Management

#### Browse All Games
- **Grid View**: Visual card-based layout showing all games at a glance
- **Game Cards Display**:
  - Date and time of game
  - Map name (The Skeld, MIRA HQ, Polus, etc.)
  - Game duration
  - Player count
  - Number of kills
  - Number of meetings
  - Winning team
- **Click to View**: Click any game card to see full details

#### Most Recent Game
- **Quick Access**: One-click button to view your latest game
- **Instant Details**: See complete breakdown of your most recent match

#### Game Details View
- **Match Information**:
  - Full Match ID (with copy to clipboard)
  - Complete timestamp
  - Map and game mode
  - Total duration
- **Statistics**:
  - Player count
  - Total meetings held
  - Tasks completed vs total tasks
  - Task completion percentage
  - Kill count and ejection count
- **Winner Information**:
  - Winning team
  - Win condition (Kill All Crewmates, Complete Tasks, etc.)
- **Detailed Player List**:
  - Player name and color
  - Role played
  - Team affiliation
  - Alive/dead status (✅/💀)
  - Task completion (for Crewmates)
  - Who killed them (if dead)

### 🔍 Search & Filter

#### Player Search
- **Search by Name**: Find any player's complete statistics
- **Comprehensive Stats Display**:
  - Total games played
  - Win/loss record and win rate percentage
  - Games as Crewmate/Impostor/Neutral
  - Death statistics and survival rate
  - Total kills made
  - Task completion rate
  - All roles played with frequency
  - Recent game history (last 5 games with results)

#### Advanced Filters
- **Map Filter**: Show only games from specific maps
  - The Skeld
  - MIRA HQ
  - Polus
  - Or all maps
- **Winner Filter**: Filter by winning team
  - Crewmate wins
  - Impostor wins
  - Or all games
- **Player Count Filter**: Filter by lobby size
  - 1-4 players
  - 5-8 players
  - 9+ players
  - Or all sizes
- **Clear Filters**: One-click reset of all filters

#### Sort Options
- **Newest First**: Most recent games at the top (default)
- **Oldest First**: Historical games first
- **Longest Duration**: Games sorted by longest playtime
- **Shortest Duration**: Quick games first

### 📈 Leaderboard

**Top 20 Players Display**:
- Ranked by win rate percentage
- Secondary sort by total games played
- Medal system (🥇 🥈 🥉) for top 3
- Shows for each player:
  - Total games played
  - Wins and win rate
  - Total kills
  - Total deaths
  - Total tasks completed

### 📊 Overall Statistics

**Live Dashboard** (updates automatically):
- Total games recorded
- Crewmate win rate with percentage
- Impostor win rate with percentage
- Average game duration

### 🛠️ Management Tools

#### File Management
- **Open Folder**: Directly access JSON files location
- **Export Stats**: Generate text report of all statistics
  - Includes overall stats
  - Lists all games with details
  - Timestamped export file
- **Clear All Data**: Delete all game records with confirmation
- **Delete Individual Game**: Remove specific games from history

#### Game Actions
- **Copy Match ID**: Copy full match ID to clipboard
- **View Full Details**: Complete game breakdown
- **Back Navigation**: Easy return to game list

---

## 💾 Installation

### Requirements
- Among Us (Steam or Epic version)
- BepInEx installed
- Town of Us mod installed
- .NET 6.0 Runtime (for viewer only)

### Mod Installation

1. **Locate Your Among Us Folder**:
   ```
   Example: D:\Steam\steamapps\common\Among Us
   ```

2. **Copy Mod Files**:
   - Copy `AUSUMMARY.DLL.dll` to:
     ```
     [Among Us]/BepInEx/plugins/
     ```
   - Copy `AUSUMMARY.Shared.dll` to:
     ```
     [Among Us]/BepInEx/plugins/
     ```

3. **Verify Installation**:
   - Launch Among Us

### Viewer Installation

1. **Extract Viewer**:
   - Extract the AUSUMMARY.Viewer folder anywhere on your computer

2. **Run the Viewer**:
   - Double-click `AUSUMMARY.Viewer.exe`
   - The viewer will automatically find your game summaries

---

## 📁 File Structure

### Output Location
All game summaries are saved to:
```
C:\Users\[YourName]\Documents\AmongUsGameSummaries\
```

### File Format
Each game is saved as a JSON file:
```
game_20260119_043200.json
```

**JSON Structure**:
```json
{
  "MatchId": "unique-match-identifier",
  "Timestamp": "2026-01-19T04:32:00",
  "Metadata": {
    "MapName": "The Skeld",
    "GameMode": "Town of Us",
    "PlayerCount": 8,
    "GameDuration": "00:05:30",
    "TotalMeetings": 3,
    "TotalTasks": 32,
    "CompletedTasks": 28
  },
  "Players": [
    {
      "PlayerName": "Ninja",
      "ColorName": "Red",
      "Role": "Sheriff",
      "Team": "Crewmate",
      "IsAlive": false,
      "DeathCause": "Killed",
      "KilledBy": "Frettedcud",
      "TasksCompleted": 5,
      "TotalTasks": 4
    }
  ],
  "Events": [
    {
      "EventType": "GameStart",
      "Timestamp": 0.0,
      "Description": "Game started"
    },
    {
      "EventType": "PlayerKilled",
      "Timestamp": 45.5,
      "Description": "Ninja was killed by Frettedcud"
    }
  ],
  "Winner": {
    "WinningTeam": "Impostor",
    "WinCondition": "Kill All Crewmates",
    "Winners": ["Frettedcud"]
  },
  "Statistics": {
    "TotalKills": 7,
    "TotalEjections": 2,
    "TaskCompletionRate": 0.875
  }
}
```

---

## 🎨 User Interface

### Modern Dark Theme
- Professional VS Code-inspired dark theme
- Clean, flat design without distracting effects
- High contrast for readability
- Color-coded elements:
  - 🔵 Blue: Primary actions and headers
  - 🟢 Green: Crewmate wins and positive stats
  - 🔴 Red: Impostor wins and danger actions
  - 🟡 Yellow: Timestamps and highlights
  - 🟣 Purple: Player statistics

### Layout
- **Two-Column Design**:
  - Left sidebar: Navigation and filters (320px)
  - Right panel: Main content area (responsive)
- **Top Menu Bar**: Quick actions and tools
- **Status Bar**: Current action and version info

### Navigation
- Clear button labels with emoji icons
- Intuitive back navigation
- Breadcrumb-style status updates
- Responsive hover effects

---

## 🐛 Troubleshooting 

### Mod Not Loading
1. Check BepInEx console for errors
2. Verify both DLL files are in plugins folder
3. Ensure Town of Us mod is installed
4. Check Among Us version compatibility

### No Games Showing in Viewer
1. Play at least one complete game
2. Check that game reached victory/defeat screen
3. Verify JSON files exist in Documents folder
4. Try clicking "Refresh Data" button

### Role Detection Issues
1. Ensure 2-second delay after game start
2. Verify Town of Us mod is active
3. Check BepInEx console for role detection logs
4. Some custom roles may show as generic team role

---

## 📊 Statistics Calculations

### Win Rate
```
Win Rate = (Wins / Total Games) × 100%
```

### Death Rate
```
Death Rate = (Deaths / Total Games) × 100%
```

### Task Completion Rate
```
Task Completion = (Completed Tasks / Total Tasks) × 100%
```

### Kill/Death Ratio
```
K/D = Total Kills / Total Deaths
```

---

## 🚀 Future Features (Planned)

- [ ] Charts and graphs for statistics visualization
- [ ] Export to CSV/Excel/Google Spreadsheets
- [ ] Compare players side-by-side
- [ ] Role-specific statistics
- [ ] Win streak tracking
- [ ] Map-specific win rates
- [ ] Time-based analysis (games per day/week)
- [ ] Discord integration (Webhooks & Bot Support)
- [ ] Custom themes

---

## 📝 Version History

### v1.0.0 (Current)
- ✅ Initial release
- ✅ Complete game tracking
- ✅ Role detection for Town of Us
- ✅ Kill and ejection recording
- ✅ Player statistics viewer
- ✅ Search and filter functionality
- ✅ Leaderboard system
- ✅ Export capabilities

---

### Development Setup
1. Clone the repository
2. Open solution in Visual Studio 2022
3. Restore NuGet packages
4. Build solution
5. Copy DLLs to Among Us plugins folder for testing

---

## ⚠️ Disclaimer

This mod is for personal use only. Use at your own risk. Not affiliated with Innersloth or the Town of Us mod team. This mod does not modify game behavior or provide unfair advantages - it only records game data.

---

## 💬 Support

For issues, questions, or suggestions:
- Open a GitHub issue
- Include logs from BepInEx console
- Describe steps to reproduce any bugs
- Provide JSON file samples if needed (redact player names if preferred)

---

## 🙏 Credits

- Built for the Among Us and Town of Us community
- Inspired by various game stat trackers
- Thanks to BepInEx and Harmony developers

---

**Enjoy tracking your Among Us games! 🚀**

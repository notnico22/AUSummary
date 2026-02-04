# AUSUMMARY 📊

**Advanced Statistics Logger and Viewer for Among Us (Town of Us Mod)**

Automatically tracks and analyzes your Among Us games with detailed statistics, kill tracking, and a beautiful desktop viewer.

# Video
https://github.com/user-attachments/assets/2385507f-1bf2-4581-84af-b371c0f0294d

---

## ✨ What's New in v1.1.1

- **🪲Fixed Role Debugging Glitch
- **🪱Fixed Parasite's Kill Condition
- **🗺️Added Custom Map Support

---

## 🎮 Features

###  Mod (In-Game)
- ✅ Automatic game tracking with zero manual input
- ✅ Tracks roles, kills, deaths, tasks, meetings, and wins
- ✅ **Detailed kill types**: Shot, Reaped, Mauled, Hunted, Bitten, Ignited, Defended, and more
- ✅ **Full neutral support**: Jester, Arsonist, Werewolf, Soul Collector, Juggernaut, and all neutral roles
- ✅ Saves detailed game data for every match

### Desktop Viewer (WPF App)
- 📋 Browse all games with beautiful visual cards
- 🔍 Search players with comprehensive statistics
- 📊 Live statistics dashboard
- **🆕 Click any player** to see death details (time, killer, kill type)
- **🆕 Role breakdown** with visual progress bars showing games per role
- 🎨 Modern dark theme UI

---

## 💾 Installation

### Requirements
- Among Us (Steam/Epic)
- BepInEx 6+
- Town of Us Mira 1.5.0+
- .NET 6.0 Runtime (for viewer)

### Quick Install

1. **Copy mod files** to `[Among Us]/BepInEx/plugins/`:
   - `AUSUMMARY.DLL.dll`
   - `AUSUMMARY.Shared.dll`

2. **Run the viewer**: Extract and launch `AUSUMMARY.Viewer.exe`

Game summaries automatically save to: `C:\Users\[YourName]\Documents\AmongUsGameSummaries\`

---

## 📊 What It Tracks

**Player Data:**
- Names, colors, roles, and modifiers
- Kill counts with specific kill types
- Death details (time, killer, method)
- Task completion with accurate filtering
- Win/loss records

**Game Data:**
- Map, duration, and player count
- Kill events with precise timestamps
- Meeting and ejection events
- **Neutral wins** (Jester, Arsonist, Executioner, etc.)
- Task completion rates

**Supported Kill Types:**
- **Crewmate**: Shot (Sheriff/Vigilante), Hunted (Hunter), Defended (Veteran)
- **Impostor**: Kill, Morphling, Escapist
- **Neutral**: Reaped (Soul Collector), Mauled (Werewolf), Slashed (Juggernaut), Hacked (Glitch), Bitten (Vampire), Ignited (Arsonist), Infected (Pestilence), Vanquished (Inquisitor)

---

## 🎯 Viewer Features

### Browse & Search
- **Game Grid**: Visual cards showing map, duration, kills, and winner
- **Player Search**: Find any player's complete statistics and role history
- **Filters**: Filter by map or winning team (Crewmate/Impostor/Neutral)

### Interactive Details
- **Click Players**: Popup shows death time, killer, and kill type
- **Role Breakdown**: Visual progress bars showing how many games played per role
- **Game Stats**: View duration, kills, tasks, and meetings for each match

### Dashboard
- Total games tracked
- Crewmate and Impostor win rates
- Auto-refresh when new games are played

---

## 📝 Version History

### v1.1.0 (Current)
- Enhanced kill tracking for Sheriff and neutral killers
- Neutral win detection with triple fallback system
- Task tracking improvements (filters impostor fake tasks)
- Interactive viewer with clickable player details
- Role breakdown with visual progress bars

### v1.0.0
- Initial release
- Basic game tracking
- Statistics viewer

---

## ⚠️ Disclaimer

Personal use only. Not affiliated with Innersloth or Town of Us. This mod only records game data - it does not modify gameplay or provide any advantages.

---

**Built for the Among Us & Town of Us community 🚀**

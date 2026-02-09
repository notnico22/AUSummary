# AUSUMMARY - Enhanced Among Us Game Statistics Mod

Automatically tracks and logs Among Us game statistics with MiraAPI support, character avatars, automatic updates, and optional global analytics.

## ✨ Features

### Core Features
- **Automatic Game Tracking** - Records every match automatically
- **Player Role Detection** - Captures roles, teams, and modifiers
- **Death Recording** - Tracks death causes, times, and killers
- **Meeting & Event Logging** - Records all meetings, votes, and ejections
- **Task Completion Tracking** - Monitors task progress per player
- **Win Condition Detection** - Identifies how each game ended
- **JSON Export** - Saves detailed summaries to your Documents folder
- **Companion Viewer App** - Beautiful WPF application to browse your games

### NEW: Enhanced Features v2.0
- ✅ **Automatic Update Checking** - Notifies you when new versions are available
- ✅ **Character Avatar Display** - See player cosmetics (color, hat, pet, skin, visor) in the viewer
- ✅ **MiraAPI Integration** - Full support for MiraAPI custom roles and modifiers
- ✅ **Global Statistics (Optional)** - Contribute anonymous stats to improve the mod
- ✅ **Enhanced Cosmetics Tracking** - Records all player customizations

## 🚀 Installation

### Requirements
- Among Us (Latest version)
- BepInEx 6.0.0+ (IL2CPP)
- **MiraAPI 0.3.7+** (Required dependency)

### Installation Steps

1. **Install BepInEx**
   - Download BepInEx IL2CPP for Among Us
   - Extract to your Among Us folder
   - Run the game once to generate folders

2. **Install MiraAPI**
   - Download MiraAPI from [GitHub](https://github.com/All-Of-Us-Mods/MiraAPI)
   - Place `MiraAPI.dll` in `BepInEx/plugins/`

3. **Install AUSUMMARY**
   - Download the latest release from [Releases](https://github.com/notnico22/AUSummary/releases)
   - Extract the mod folder to `BepInEx/plugins/`
   - Files should be:
     - `BepInEx/plugins/AUSUMMARY/AUSUMMARY.DLL.dll`
     - `BepInEx/plugins/AUSUMMARY/AUSUMMARY.Shared.dll`

4. **Run the Viewer** (Optional)
   - Extract `AUSUMMARY.Viewer.exe` anywhere
   - Run it to browse your game statistics
   - Automatically monitors for new games

## 📖 Usage

### In-Game
- Simply play Among Us normally
- Games are automatically tracked
- Check BepInEx console for "AUSUMMARY" messages
- Update notifications appear in-game when available

### Viewer Application
- Run `AUSUMMARY.Viewer.exe`
- Browse all your games
- Filter by map, winner, date
- View detailed player statistics
- Search for specific players
- See player avatars with cosmetics

### File Locations
- **Game Summaries:** `Documents/AUSUMMARY/Summaries/`
- **Config:** `BepInEx/config/ausummary.mod.cfg`

## ⚙️ Configuration

Edit `BepInEx/config/ausummary.mod.cfg`:

```ini
[General]
# Check for mod updates on startup
CheckForUpdates = true

# Send anonymous game statistics to global dashboard
# Helps improve the mod with usage data
SendAnonymousStats = false
```

## 🎨 Character Avatars

The viewer now displays character avatars showing:
- Player color (Red, Blue, Green, etc.)
- Visual status (Alive/Dead indicator)
- Cosmetic IDs (Hat, Pet, Skin, Visor, Nameplate)
- Role-based coloring

## 📊 Global Statistics Dashboard

If enabled, anonymous game statistics are sent to help improve the mod:

**What is sent:**
- Map name, game mode, player count
- Win conditions and team distribution
- Task completion rates
- Role popularity (anonymous)
- Game duration

**What is NOT sent:**
- Player names
- Discord usernames  
- IP addresses
- Any personal information

Data helps us understand:
- Which maps are played most
- Balance issues with roles
- Average game lengths
- Win rate distributions

## 🔄 Automatic Updates

AUSUMMARY checks GitHub for new versions on startup. When an update is available:
1. You'll see a notification in the BepInEx console
2. An in-game notification appears (if in lobby)
3. Download link provided in console
4. No automatic downloads - you stay in control

## 🛠️ For Developers

### Building from Source

```bash
# Clone repository
git clone https://github.com/notnico22/AUSummary.git
cd AUSummary

# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release

# Output in bin/Release/net6.0/
```

### Project Structure
- `AUSUMMARY.DLL` - Main mod plugin (BepInEx)
- `AUSUMMARY.Shared` - Shared models and utilities
- `AUSUMMARY.Viewer` - WPF viewer application

### Setting Up Vercel Dashboard

1. Create a Vercel project
2. Add API endpoint at `/api/stats.ts`
3. Update `VercelStatsSender.cs` with your endpoint URL
4. Deploy to Vercel

Example API endpoint:
```typescript
import type { VercelRequest, VercelResponse } from '@vercel/node';

export default async function handler(req: VercelRequest, res: VercelResponse) {
  const stats = req.body;
  // Store in database (Vercel KV, Postgres, MongoDB, etc.)
  return res.status(200).json({ success: true });
}
```

## 🤝 Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📝 Compatibility

- ✅ Vanilla Among Us
- ✅ MiraAPI mods
- ✅ Town of Us: Mira
- ✅ Custom roles and modifiers
- ⚠️ Other mod packs - may have limited support

## 🐛 Troubleshooting

### Mod not loading
- Ensure MiraAPI is installed
- Check BepInEx/LogOutput.txt for errors
- Verify BepInEx is IL2CPP version

### Games not being tracked
- Check BepInEx console for errors
- Ensure permissions on Documents folder
- Try running Among Us as administrator

### Viewer not showing games
- Check `Documents/AUSUMMARY/Summaries/` exists
- Verify JSON files are being created
- Click "Refresh Data" in viewer

### Update check fails
- Normal if offline or GitHub is down
- Can disable in config if problematic
- Doesn't affect mod functionality

## 📄 License

MIT License - See LICENSE file for details

## 🙏 Credits

- Built with **MiraAPI** by the All Of Us team
- Inspired by the Among Us modding community
- Uses BepInEx framework
- Viewer uses LiveCharts for statistics

## 📮 Support

- **Issues:** [GitHub Issues](https://github.com/notnico22/AUSummary/issues)
- **Discord:** [Join our server](#) (TODO: Add link)
- **Email:** support@ausummary.com (TODO: Add email)

---

Made with ❤️ for the Among Us community

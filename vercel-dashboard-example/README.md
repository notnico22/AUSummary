# AUSUMMARY Vercel Dashboard

Beautiful web dashboard to view and analyze global game statistics from the AUSUMMARY Among Us mod.

## 🌟 Features

### 📊 Main Dashboard (`/index.html`)
- Real-time statistics overview
- Beautiful charts showing:
  - Popular maps
  - Win rate distribution
  - Most played roles
  - Games timeline
- Recent games table
- Auto-refreshes every 30 seconds

### 🔍 All Games Viewer (`/all-games.html`)
- Developer/admin panel
- View ALL game data with filters
- Search by map, winner, or match ID
- Export to JSON
- View raw JSON for each game
- Terminal-style UI for that hacker aesthetic 😎

## 🚀 Quick Deployment

### 1. Install & Deploy
```bash
cd vercel-dashboard-example
npm install
npx vercel
```

### 2. Visit Your Dashboard
After deployment, you'll get a URL like: `https://ausummary-xyz.vercel.app`

**Pages:**
- `/` or `/index.html` - Main dashboard with charts
- `/all-games.html` - View all games with filters
- `/api/analytics` - Raw analytics JSON
- `/api/all-games` - All games JSON
- `/api/stats` - POST endpoint for receiving data

### 3. Test with Sample Data
Visit: `https://your-url.vercel.app/api/populate-test-data`

This creates 50 fake games so you can see the dashboard in action!

### 4. Update the Mod
Edit `AUSUMMARY.Shared/VercelStatsSender.cs`:
```csharp
private const string VercelEndpoint = "https://your-actual-url.vercel.app/api/stats";
```

Rebuild and reinstall the mod, then enable stats in config:
```ini
SendAnonymousStats = true
```

## 📡 API Endpoints

### POST `/api/stats`
Receives game data from mods.

**Example Request:**
```bash
curl -X POST https://your-url.vercel.app/api/stats \
  -H "Content-Type: application/json" \
  -d '{
    "matchId": "abc123",
    "timestamp": "2025-02-09T12:00:00Z",
    "mapName": "The Skeld",
    "playerCount": 10,
    "winningTeam": "Crewmate"
  }'
```

### GET `/api/analytics`
Returns aggregated statistics.

**Example Response:**
```json
{
  "totalGames": 150,
  "totalPlayers": 1500,
  "popularMaps": {
    "The Skeld": 50,
    "Polus": 40,
    "MIRA HQ": 30
  },
  "winRates": {
    "crewmate": 0.52,
    "impostor": 0.48
  },
  "averageGameDuration": 480,
  "totalKills": 450,
  "popularRoles": {
    "Crewmate": 1000,
    "Impostor": 300
  },
  "recentGames": [...]
}
```

### GET `/api/all-games`
Returns all stored games.

### GET `/api/populate-test-data`
Generates 50 test games for testing.

## 💾 Adding a Real Database

The current setup uses **in-memory storage** which resets when Vercel redeploys. For production, add a real database:

### Option 1: Vercel KV (Redis) - Recommended for beginners

1. **Enable in Vercel Dashboard:**
   - Go to your project → Storage → Create → KV

2. **Update `api/stats.ts`:**
```typescript
import { kv } from '@vercel/kv';

// In the handler:
await kv.lpush('game_stats', JSON.stringify(stats));
await kv.ltrim('game_stats', 0, 999); // Keep last 1000
```

3. **Update `api/analytics.ts`:**
```typescript
import { kv } from '@vercel/kv';

const gamesData = await kv.lrange('game_stats', 0, -1);
const games = gamesData.map(g => JSON.parse(g));
```

### Option 2: Vercel Postgres - Best for complex queries

1. **Enable in Vercel Dashboard:**
   - Storage → Create → Postgres

2. **Create table:**
```sql
CREATE TABLE game_stats (
  id SERIAL PRIMARY KEY,
  match_id VARCHAR(255) UNIQUE,
  timestamp TIMESTAMPTZ,
  map_name VARCHAR(100),
  player_count INT,
  winning_team VARCHAR(50),
  data JSONB,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_timestamp ON game_stats(timestamp DESC);
CREATE INDEX idx_map ON game_stats(map_name);
```

3. **Update code:**
```typescript
import { sql } from '@vercel/postgres';

// Save game
await sql`
  INSERT INTO game_stats (match_id, timestamp, map_name, player_count, winning_team, data)
  VALUES (${stats.matchId}, ${stats.timestamp}, ${stats.mapName}, 
          ${stats.playerCount}, ${stats.winningTeam}, ${JSON.stringify(stats)})
`;

// Get games
const { rows } = await sql`SELECT * FROM game_stats ORDER BY timestamp DESC LIMIT 1000`;
```

### Option 3: MongoDB Atlas - Best for flexibility

1. **Setup:**
   - Create account at mongodb.com
   - Create cluster (free tier available)
   - Get connection string

2. **Add to Vercel:**
   - Settings → Environment Variables
   - `MONGODB_URI` = your connection string

3. **Update code:**
```typescript
import { MongoClient } from 'mongodb';

const client = new MongoClient(process.env.MONGODB_URI!);
await client.connect();
const db = client.db('ausummary');

// Save game
await db.collection('game_stats').insertOne(stats);

// Get games
const games = await db.collection('game_stats')
  .find({})
  .sort({ timestamp: -1 })
  .limit(1000)
  .toArray();
```

### Option 4: Supabase - Best for real-time features

1. **Setup:**
   - Create project at supabase.com
   - Get URL and anon key

2. **Create table in Supabase SQL editor:**
```sql
create table game_stats (
  id bigserial primary key,
  match_id text unique,
  data jsonb,
  timestamp timestamptz,
  created_at timestamptz default now()
);
```

3. **Update code:**
```typescript
import { createClient } from '@supabase/supabase-js';

const supabase = createClient(
  process.env.SUPABASE_URL!,
  process.env.SUPABASE_KEY!
);

// Save game
await supabase.from('game_stats').insert({
  match_id: stats.matchId,
  data: stats,
  timestamp: stats.timestamp
});

// Get games
const { data } = await supabase
  .from('game_stats')
  .select('*')
  .order('timestamp', { ascending: false })
  .limit(1000);
```

## 🎨 Customizing the Dashboard

### Colors
Edit the CSS in `public/index.html`:
```css
/* Change the color scheme */
background: linear-gradient(135deg, #0a0a0a 0%, #1a1a1a 100%);
background: linear-gradient(135deg, #0ea5e9 0%, #8b5cf6 100%);
```

### Charts
Modify the Chart.js configurations in the JavaScript section.

### Add More Stats
Update `api/analytics.ts` to calculate additional metrics.

## 🔒 Security

### Rate Limiting
Add rate limiting to prevent abuse:

```typescript
// api/stats.ts
const rateLimits = new Map();

export default async function handler(req: VercelRequest, res: VercelResponse) {
  const ip = req.headers['x-forwarded-for'] || 'unknown';
  const now = Date.now();
  const limit = rateLimits.get(ip) || [];
  
  // Allow 10 requests per minute
  const recentRequests = limit.filter((time: number) => now - time < 60000);
  
  if (recentRequests.length >= 10) {
    return res.status(429).json({ error: 'Rate limit exceeded' });
  }
  
  recentRequests.push(now);
  rateLimits.set(ip, recentRequests);
  
  // ... rest of handler
}
```

### API Keys (Optional)
Require an API key for submissions:

```typescript
const API_KEY = process.env.AUSUMMARY_API_KEY;

if (req.headers['x-api-key'] !== API_KEY) {
  return res.status(401).json({ error: 'Unauthorized' });
}
```

Then update the mod to send the key.

## 📊 Example Queries

### Most Popular Map
```sql
SELECT map_name, COUNT(*) as games
FROM game_stats
GROUP BY map_name
ORDER BY games DESC
LIMIT 1;
```

### Average Win Rate by Map
```sql
SELECT 
  map_name,
  AVG(CASE WHEN winning_team = 'Crewmate' THEN 1 ELSE 0 END) as crew_rate
FROM game_stats
GROUP BY map_name;
```

### Games Per Day
```sql
SELECT 
  DATE(timestamp) as date,
  COUNT(*) as games
FROM game_stats
GROUP BY DATE(timestamp)
ORDER BY date DESC
LIMIT 30;
```

## 🐛 Troubleshooting

### Dashboard shows "Loading..." forever
- Check browser console for errors
- Verify `/api/analytics` endpoint works
- Try visiting `/api/populate-test-data` first

### No data appears after games
- Check mod is sending data (BepInEx console)
- Verify endpoint URL is correct
- Check Vercel logs: `vercel logs`

### Charts not rendering
- Ensure Chart.js CDN is accessible
- Check browser console for errors
- Verify data format matches Chart.js expectations

## 📱 Mobile Support

The dashboard is fully responsive and works on mobile devices!

## 🚀 Performance Tips

1. **Limit stored games** - Keep last 1000 games max
2. **Add caching** - Cache analytics calculations
3. **Use CDN** - Vercel automatically handles this
4. **Optimize queries** - Add database indexes

## 📝 License

Part of the AUSUMMARY project - MIT License

## 🙏 Support

- Main Repo: https://github.com/notnico22/AUSummary
- Issues: https://github.com/notnico22/AUSummary/issues

---

**Made with 💚 for the Among Us modding community**

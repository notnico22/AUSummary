import { MongoClient, Db } from 'mongodb';
import type { VercelRequest, VercelResponse } from '@vercel/node';

// MongoDB connection
let cachedClient: MongoClient | null = null;
let cachedDb: Db | null = null;

async function connectToDatabase() {
  if (cachedClient && cachedDb) {
    return { client: cachedClient, db: cachedDb };
  }

  if (!process.env.MONGODB_URI) {
    throw new Error('MONGODB_URI is not defined in environment variables');
  }

  const client = await MongoClient.connect(process.env.MONGODB_URI);
  const db = client.db('ausummary');

  cachedClient = client;
  cachedDb = db;

  return { client, db };
}

/**
 * Get aggregated analytics from MongoDB
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Enable CORS
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  if (req.method !== 'GET') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  try {
    const { db } = await connectToDatabase();
    const gamesCollection = db.collection('games');

    // Get all games for analytics
    const allGames = await gamesCollection.find({}).toArray();

    if (allGames.length === 0) {
      return res.status(200).json({
        totalGames: 0,
        totalPlayers: 0,
        uniquePlayers: 0,
        uniquePlayerIds: 0,
        totalKills: 0,
        totalMeetings: 0,
        totalBodiesReported: 0,
        totalEjections: 0,
        totalTasksCompleted: 0,
        totalTasks: 0,
        taskCompletionRate: 0,
        averageGameDuration: 0,
        winRates: { crewmate: 0, impostor: 0, neutral: 0 },
        popularMaps: {},
        popularRoles: {},
        gamesPerDay: {},
        recentGames: []
      });
    }

    // Calculate statistics
    const totalGames = allGames.length;
    let totalPlayers = 0;
    let totalKills = 0;
    let totalGameDuration = 0;
    let crewmateWins = 0;
    let impostorWins = 0;
    let neutralWins = 0;
    let totalMeetings = 0;
    let totalBodiesReported = 0;
    let totalEjections = 0;
    let totalTasksCompleted = 0;
    let totalTasks = 0;
    const uniquePlayerIds = new Set<string>();
    const uniquePlayerNames = new Set<string>();
    const mapCounts: Record<string, number> = {};
    const roleCounts: Record<string, number> = {};
    const gamesPerDay: Record<string, number> = {};

    allGames.forEach((game: any) => {
      // Count players
      const playerCount = game.metadata?.playerCount || game.players?.length || 0;
      totalPlayers += playerCount;

      // Count kills and other statistics
      totalKills += game.statistics?.totalKills || 0;
      totalEjections += game.statistics?.totalEjections || 0;
      
      // Count meetings and bodies reported from metadata
      totalMeetings += game.metadata?.totalMeetings || 0;
      
      // Count tasks
      totalTasksCompleted += game.metadata?.completedTasks || 0;
      totalTasks += game.metadata?.totalTasks || 0;
      
      // Count bodies reported from events
      if (game.events && Array.isArray(game.events)) {
        const bodiesReported = game.events.filter((e: any) => 
          e.eventType === 'BodyReported' || e.description?.toLowerCase().includes('reported')
        ).length;
        totalBodiesReported += bodiesReported;
      }
      
      // Track unique players by userId (more accurate than name)
      // Each game has a userId field representing who uploaded it
      if (game.userId) {
        uniquePlayerIds.add(game.userId);
      }
      
      // Also track unique player names for display purposes
      if (game.players && Array.isArray(game.players)) {
        game.players.forEach((player: any) => {
          if (player.playerName) {
            uniquePlayerNames.add(player.playerName.toLowerCase().trim());
          }
        });
      }

      // Sum game duration (handle both number and TimeSpan string formats)
      const gameDuration = game.metadata?.gameDuration;
      if (typeof gameDuration === 'number') {
        totalGameDuration += gameDuration;
      } else if (typeof gameDuration === 'string') {
        // Parse TimeSpan format like "00:13:27.0728997"
        const parts = gameDuration.split(':');
        if (parts.length >= 3) {
          const hours = parseInt(parts[0]) || 0;
          const minutes = parseInt(parts[1]) || 0;
          const seconds = parseFloat(parts[2]) || 0;
          totalGameDuration += (hours * 3600) + (minutes * 60) + seconds;
        }
      }

      // Count wins by team
      if (game.winner?.winningTeam === 'Crewmate') crewmateWins++;
      else if (game.winner?.winningTeam === 'Impostor') impostorWins++;
      else if (game.winner?.winningTeam === 'Neutral') neutralWins++;

      // Count maps
      const mapName = game.metadata?.mapName || 'Unknown';
      mapCounts[mapName] = (mapCounts[mapName] || 0) + 1;

      // Count roles
      if (game.players && Array.isArray(game.players)) {
        game.players.forEach((player: any) => {
          if (player.role) {
            roleCounts[player.role] = (roleCounts[player.role] || 0) + 1;
          }
        });
      }

      // Games per day
      const date = new Date(game.timestamp).toISOString().split('T')[0];
      gamesPerDay[date] = (gamesPerDay[date] || 0) + 1;
    });

    // Get recent 20 games
    const recentGames = allGames
      .sort((a: any, b: any) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
      .slice(0, 20)
      .map((game: any) => {
        // Convert game duration to seconds (handle both formats)
        let durationSeconds = 0;
        const gameDuration = game.metadata?.gameDuration;
        if (typeof gameDuration === 'number') {
          durationSeconds = gameDuration;
        } else if (typeof gameDuration === 'string') {
          const parts = gameDuration.split(':');
          if (parts.length >= 3) {
            const hours = parseInt(parts[0]) || 0;
            const minutes = parseInt(parts[1]) || 0;
            const seconds = parseFloat(parts[2]) || 0;
            durationSeconds = (hours * 3600) + (minutes * 60) + seconds;
          }
        }
        
        return {
          matchId: game.matchId,
          timestamp: game.timestamp,
          mapName: game.metadata?.mapName,
          playerCount: game.metadata?.playerCount,
          gameDuration: durationSeconds,
          winningTeam: game.winner?.winningTeam,
          winCondition: game.winner?.winCondition,
          totalKills: game.statistics?.totalKills,
          taskCompletionRate: game.statistics?.taskCompletionRate
        };
      });

    // Calculate averages
    const averageGameDuration = totalGames > 0 ? totalGameDuration / totalGames : 0;
    const crewmateWinRate = totalGames > 0 ? crewmateWins / totalGames : 0;
    const impostorWinRate = totalGames > 0 ? impostorWins / totalGames : 0;
    const neutralWinRate = totalGames > 0 ? neutralWins / totalGames : 0;

    // Get top 5 most popular roles
    const topRoles = Object.entries(roleCounts)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 10)
      .reduce((obj, [role, count]) => ({ ...obj, [role]: count }), {});

    return res.status(200).json({
      totalGames,
      totalPlayers,
      uniquePlayers: uniquePlayerNames.size,
      uniquePlayerIds: uniquePlayerIds.size,
      totalKills,
      totalMeetings,
      totalBodiesReported,
      totalEjections,
      totalTasksCompleted,
      totalTasks,
      taskCompletionRate: totalTasks > 0 ? totalTasksCompleted / totalTasks : 0,
      averageGameDuration: Math.round(averageGameDuration),
      winRates: {
        crewmate: crewmateWinRate,
        impostor: impostorWinRate,
        neutral: neutralWinRate
      },
      popularMaps: mapCounts,
      popularRoles: topRoles,
      gamesPerDay: Object.fromEntries(
        Object.entries(gamesPerDay)
          .sort((a, b) => a[0].localeCompare(b[0]))
          .slice(-30) // Last 30 days
      ),
      recentGames
    });

  } catch (error) {
    console.error('Error fetching analytics:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

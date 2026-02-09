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
 * Player lookup API
 * Search by playerName or userId
 * Example: /api/player-lookup?name=Ninja
 * Example: /api/player-lookup?userId=abc-123
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Enable CORS
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  // Handle preflight
  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  if (req.method !== 'GET') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  try {
    const { name, userId } = req.query;

    if (!name && !userId) {
      return res.status(400).json({ 
        error: 'Missing query parameter',
        message: 'Provide either "name" or "userId" parameter'
      });
    }

    const { db } = await connectToDatabase();
    const gamesCollection = db.collection('games');

    let games;
    let searchCriteria;

    if (userId) {
      // Search by user ID
      searchCriteria = { userId: userId as string };
      games = await gamesCollection
        .find(searchCriteria)
        .sort({ timestamp: -1 })
        .limit(100)
        .toArray();
    } else {
      // Search by player name (case-insensitive)
      searchCriteria = { 
        'players.playerName': { 
          $regex: name as string, 
          $options: 'i' 
        } 
      };
      games = await gamesCollection
        .find(searchCriteria)
        .sort({ timestamp: -1 })
        .limit(100)
        .toArray();
    }

    if (games.length === 0) {
      return res.status(404).json({
        error: 'No games found',
        searchedFor: userId ? { userId } : { playerName: name }
      });
    }

    // Calculate player statistics
    let totalGames = games.length;
    let totalWins = 0;
    let totalKills = 0;
    let totalDeaths = 0;
    let totalTasksCompleted = 0;
    let totalTasks = 0;
    let roleStats: Record<string, number> = {};
    let teamStats = { Crewmate: 0, Impostor: 0, Neutral: 0 };
    let winsByRole: Record<string, number> = {};
    let gamesAsImpostor = 0;
    let impostorWins = 0;

    games.forEach((game: any) => {
      // Find the player in this game
      const player = game.players?.find((p: any) => {
        if (userId) {
          return game.userId === userId;
        } else {
          return p.playerName.toLowerCase().includes((name as string).toLowerCase());
        }
      });

      if (player) {
        // Count roles
        roleStats[player.role] = (roleStats[player.role] || 0) + 1;

        // Count teams
        if (player.team === 'Crewmate') teamStats.Crewmate++;
        else if (player.team === 'Impostor') teamStats.Impostor++;
        else teamStats.Neutral++;

        // Track kills and deaths
        totalKills += player.killCount || 0;
        if (!player.isAlive) totalDeaths++;

        // Track tasks
        totalTasksCompleted += player.tasksCompleted || 0;
        totalTasks += player.totalTasks || 0;

        // Track wins
        const didWin = game.winner?.winningTeam === player.team;
        if (didWin) {
          totalWins++;
          winsByRole[player.role] = (winsByRole[player.role] || 0) + 1;
        }

        // Impostor-specific stats
        if (player.team === 'Impostor') {
          gamesAsImpostor++;
          if (didWin) impostorWins++;
        }
      }
    });

    const winRate = totalGames > 0 ? (totalWins / totalGames) * 100 : 0;
    const survivalRate = totalGames > 0 ? ((totalGames - totalDeaths) / totalGames) * 100 : 0;
    const taskCompletionRate = totalTasks > 0 ? (totalTasksCompleted / totalTasks) * 100 : 0;
    const impostorWinRate = gamesAsImpostor > 0 ? (impostorWins / gamesAsImpostor) * 100 : 0;

    // Format response
    const playerStats = {
      searchCriteria: userId ? { userId } : { playerName: name },
      totalGames,
      totalWins,
      winRate: winRate.toFixed(1) + '%',
      survivalRate: survivalRate.toFixed(1) + '%',
      statistics: {
        totalKills,
        totalDeaths,
        killDeathRatio: totalDeaths > 0 ? (totalKills / totalDeaths).toFixed(2) : totalKills.toString(),
        tasksCompleted: totalTasksCompleted,
        totalTasks,
        taskCompletionRate: taskCompletionRate.toFixed(1) + '%',
      },
      teamDistribution: teamStats,
      roleDistribution: Object.entries(roleStats)
        .sort((a, b) => b[1] - a[1])
        .reduce((obj, [role, count]) => ({ ...obj, [role]: count }), {}),
      winsByRole: Object.entries(winsByRole)
        .sort((a, b) => b[1] - a[1])
        .reduce((obj, [role, wins]) => ({ ...obj, [role]: wins }), {}),
      impostorStats: {
        gamesAsImpostor,
        impostorWins,
        impostorWinRate: impostorWinRate.toFixed(1) + '%'
      },
      recentGames: games.slice(0, 10).map((game: any) => {
        const player = game.players?.find((p: any) => {
          if (userId) {
            return game.userId === userId;
          } else {
            return p.playerName.toLowerCase().includes((name as string).toLowerCase());
          }
        });

        return {
          matchId: game.matchId,
          timestamp: game.timestamp,
          map: game.metadata?.mapName,
          role: player?.role,
          team: player?.team,
          survived: player?.isAlive,
          won: game.winner?.winningTeam === player?.team,
          kills: player?.killCount || 0,
          tasksCompleted: `${player?.tasksCompleted || 0}/${player?.totalTasks || 0}`,
        };
      }),
    };

    return res.status(200).json(playerStats);

  } catch (error) {
    console.error('Error in player lookup:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

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
 * Normalize game data to handle both PascalCase and camelCase properties
 */
function normalizeGameData(data: any): any {
  // If data already has lowercase properties, return as-is
  if (data.matchId) return data;

  // Convert PascalCase to camelCase for storage
  return {
    userId: data.userId || data.UserId,
    matchId: data.matchId || data.MatchId,
    timestamp: data.timestamp || data.Timestamp,
    metadata: {
      mapName: data.Metadata?.MapName || data.metadata?.mapName,
      gameMode: data.Metadata?.GameMode || data.metadata?.gameMode,
      playerCount: data.Metadata?.PlayerCount || data.metadata?.playerCount,
      gameDuration: data.Metadata?.GameDuration || data.metadata?.gameDuration,
      totalMeetings: data.Metadata?.TotalMeetings || data.metadata?.totalMeetings,
      totalTasks: data.Metadata?.TotalTasks || data.metadata?.totalTasks,
      completedTasks: data.Metadata?.CompletedTasks || data.metadata?.completedTasks,
      modVersion: data.Metadata?.ModVersion || data.metadata?.modVersion,
    },
    players: (data.Players || data.players || []).map((p: any) => ({
      playerName: p.PlayerName || p.playerName,
      playerId: p.PlayerId ?? p.playerId,
      colorName: p.ColorName || p.colorName,
      role: p.Role || p.role,
      team: p.Team || p.team,
      modifiers: p.Modifiers || p.modifiers || [],
      isAlive: p.IsAlive ?? p.isAlive,
      deathCause: p.DeathCause || p.deathCause,
      killType: p.KillType || p.killType,
      timeOfDeath: p.TimeOfDeath ?? p.timeOfDeath,
      killedBy: p.KilledBy || p.killedBy,
      killCount: p.KillCount ?? p.killCount ?? 0,
      tasksCompleted: p.TasksCompleted ?? p.tasksCompleted ?? 0,
      totalTasks: p.TotalTasks ?? p.totalTasks ?? 0,
      wasEjected: p.WasEjected ?? p.wasEjected ?? false,
      survivedRounds: p.SurvivedRounds ?? p.survivedRounds ?? 0,
    })),
    events: (data.Events || data.events || []).map((e: any) => ({
      eventType: e.EventType || e.eventType,
      timestamp: e.Timestamp ?? e.timestamp,
      description: e.Description || e.description,
      involvedPlayers: e.InvolvedPlayers || e.involvedPlayers || [],
      data: e.Data || e.data,
    })),
    winner: {
      winningTeam: data.Winner?.WinningTeam || data.winner?.winningTeam,
      winCondition: data.Winner?.WinCondition || data.winner?.winCondition,
      winners: data.Winner?.Winners || data.winner?.winners || [],
      mvp: data.Winner?.Mvp || data.winner?.mvp,
    },
    statistics: {
      totalKills: data.Statistics?.TotalKills ?? data.statistics?.totalKills ?? 0,
      totalEjections: data.Statistics?.TotalEjections ?? data.statistics?.totalEjections ?? 0,
      totalDeaths: data.Statistics?.TotalDeaths ?? data.statistics?.totalDeaths ?? 0,
      taskCompletionRate: data.Statistics?.TaskCompletionRate ?? data.statistics?.taskCompletionRate ?? 0,
      averageMeetingTime: data.Statistics?.AverageMeetingTime ?? data.statistics?.averageMeetingTime ?? 0,
      impostorWinRate: data.Statistics?.ImpostorWinRate ?? data.statistics?.impostorWinRate ?? 0,
      crewmateWinRate: data.Statistics?.CrewmateWinRate ?? data.statistics?.crewmateWinRate ?? 0,
    },
  };
}

/**
 * API endpoint to receive game statistics from AUSUMMARY mod
 * Stores complete game data in MongoDB Atlas
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  // Enable CORS
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  // Handle preflight
  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  // Only accept POST requests
  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  try {
    const rawData = req.body;

    // Normalize the data to handle both PascalCase and camelCase
    const gameData = normalizeGameData(rawData);

    // Validate required fields
    if (!gameData.matchId || !gameData.timestamp) {
      return res.status(400).json({ 
        error: 'Missing required fields: matchId, timestamp',
        received: { matchId: gameData.matchId, timestamp: gameData.timestamp }
      });
    }

    // Log the received game
    console.log('📊 Received game data:', {
      matchId: gameData.matchId,
      userId: gameData.userId,
      map: gameData.metadata?.mapName,
      winner: gameData.winner?.winningTeam,
      players: gameData.metadata?.playerCount,
      timestamp: gameData.timestamp
    });

    // Connect to MongoDB
    const { db } = await connectToDatabase();
    const gamesCollection = db.collection('games');

    // Create indexes if they don't exist
    await gamesCollection.createIndex({ matchId: 1 }, { unique: true });
    await gamesCollection.createIndex({ timestamp: -1 });
    await gamesCollection.createIndex({ 'metadata.mapName': 1 });
    await gamesCollection.createIndex({ 'winner.winningTeam': 1 });
    await gamesCollection.createIndex({ userId: 1 });
    await gamesCollection.createIndex({ 'players.playerName': 1 });

    // Insert or update the complete game data
    const result = await gamesCollection.updateOne(
      { matchId: gameData.matchId },
      { $set: gameData },
      { upsert: true }
    );

    console.log(`✅ Game ${result.upsertedId ? 'inserted' : 'updated'} in MongoDB:`, gameData.matchId);

    return res.status(200).json({ 
      success: true,
      message: 'Game data received and stored successfully',
      matchId: gameData.matchId,
      inserted: !!result.upsertedId
    });

  } catch (error) {
    console.error('❌ Error processing game data:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

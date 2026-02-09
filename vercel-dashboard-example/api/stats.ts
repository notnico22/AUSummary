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
    const gameData = req.body;

    // Validate required fields
    if (!gameData.matchId || !gameData.timestamp) {
      return res.status(400).json({ error: 'Missing required fields: matchId, timestamp' });
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

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
 * Fetch all games from MongoDB
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

    // Get query parameters for filtering
    const { limit = '50', skip = '0', map, userId } = req.query;
    
    // Build filter query
    const filter: any = {};
    if (map) filter['metadata.mapName'] = map;
    if (userId) filter.userId = userId;

    // Fetch games with pagination
    const games = await gamesCollection
      .find(filter)
      .sort({ timestamp: -1 })
      .skip(parseInt(skip as string))
      .limit(parseInt(limit as string))
      .toArray();

    // Get total count for pagination
    const totalGames = await gamesCollection.countDocuments(filter);

    return res.status(200).json({
      games,
      pagination: {
        total: totalGames,
        limit: parseInt(limit as string),
        skip: parseInt(skip as string),
        hasMore: totalGames > (parseInt(skip as string) + games.length)
      }
    });

  } catch (error) {
    console.error('Error fetching games:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

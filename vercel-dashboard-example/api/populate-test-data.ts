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
 * Populate test data into MongoDB
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    // Generate test games
    const testGames = [];
    const maps = ['The Skeld', 'MIRA HQ', 'Polus', 'The Airship', 'The Fungle'];
    const teams = ['Crewmate', 'Impostor', 'Neutral'];
    const roles = ['Crewmate', 'Impostor', 'Engineer', 'Scientist', 'Shapeshifter', 'Guardian Angel', 'Jester', 'Arsonist'];

    for (let i = 0; i < 50; i++) {
      const playerCount = 8 + Math.floor(Math.random() * 7);
      const winningTeam = teams[Math.floor(Math.random() * teams.length)];
      const gameDuration = 300 + Math.floor(Math.random() * 600);

      const roleData = [];
      for (let j = 0; j < playerCount; j++) {
        const playerRole = roles[Math.floor(Math.random() * roles.length)];
        const isImpostor = playerRole === 'Impostor' || playerRole === 'Shapeshifter';
        const isNeutral = ['Jester', 'Arsonist'].includes(playerRole);
        
        roleData.push({
          role: playerRole,
          team: isNeutral ? 'Neutral' : (isImpostor ? 'Impostor' : 'Crewmate'),
          survived: Math.random() > 0.4,
          killCount: isImpostor ? Math.floor(Math.random() * 3) : 0,
          tasksCompleted: !isImpostor ? Math.floor(Math.random() * 10) : 0,
          totalTasks: !isImpostor ? 10 : 0
        });
      }

      const timestamp = new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000);
      const matchId = `test-${timestamp.getTime()}-${i}`;

      const game = {
        matchId: matchId,
        timestamp: timestamp.toISOString(),
        mapName: maps[Math.floor(Math.random() * maps.length)],
        gameMode: 'Classic',
        playerCount: playerCount,
        gameDuration: gameDuration,
        winningTeam: winningTeam,
        winCondition: winningTeam === 'Crewmate' ? 
          (Math.random() > 0.5 ? 'HumansByTask' : 'HumansByVote') :
          (winningTeam === 'Impostor' ? 
            (Math.random() > 0.5 ? 'ImpostorByKill' : 'ImpostorBySabotage') :
            'NeutralWin'),
        totalKills: Math.floor(Math.random() * 8),
        totalEjections: Math.floor(Math.random() * 3),
        taskCompletionRate: 0.3 + Math.random() * 0.6,
        modVersion: '2.0.0',
        roles: roleData
      };

      testGames.push(game);
    }

    // Connect to MongoDB
    const { db } = await connectToDatabase();
    const gamesCollection = db.collection('games');

    // Create indexes
    await gamesCollection.createIndex({ matchId: 1 }, { unique: true });
    await gamesCollection.createIndex({ timestamp: -1 });
    await gamesCollection.createIndex({ mapName: 1 });
    await gamesCollection.createIndex({ winningTeam: 1 });

    // Insert all test games (use insertMany with ordered: false to continue on duplicates)
    let insertedCount = 0;
    try {
      const result = await gamesCollection.insertMany(testGames, { ordered: false });
      insertedCount = result.insertedCount;
    } catch (error: any) {
      // Some games might already exist, count successful insertions
      if (error.writeErrors) {
        insertedCount = testGames.length - error.writeErrors.length;
      }
    }

    // Get total games count
    const totalGames = await gamesCollection.countDocuments();

    return res.status(200).json({
      success: true,
      message: 'Test data populated successfully in MongoDB',
      gamesAdded: insertedCount,
      totalGames: totalGames
    });

  } catch (error) {
    console.error('Error populating test data:', error);
    return res.status(500).json({
      error: 'Failed to populate test data',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

import type { VercelRequest, VercelResponse } from '@vercel/node';

// In-memory storage for demo purposes
// In production, replace with a real database
let gamesData: any[] = [];

/**
 * API endpoint to receive game statistics from AUSUMMARY mod
 * 
 * Deploy this to Vercel and update the endpoint URL in VercelStatsSender.cs
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
    const stats = req.body;

    // Validate required fields
    if (!stats.matchId || !stats.timestamp) {
      return res.status(400).json({ error: 'Missing required fields: matchId, timestamp' });
    }

    // Log the received stats (for debugging)
    console.log('📊 Received game stats:', {
      matchId: stats.matchId,
      map: stats.mapName,
      winner: stats.winningTeam,
      players: stats.playerCount,
      timestamp: stats.timestamp
    });

    // Store in memory (for demo)
    // TODO: In production, replace with database storage
    gamesData.push(stats);
    
    // Keep only last 1000 games to prevent memory issues
    if (gamesData.length > 1000) {
      gamesData = gamesData.slice(-1000);
    }

    console.log(`✅ Total games stored: ${gamesData.length}`);

    // DATABASE EXAMPLES:
    
    // Vercel KV (Redis):
    // import { kv } from '@vercel/kv';
    // await kv.lpush('game_stats', JSON.stringify(stats));
    // await kv.ltrim('game_stats', 0, 999); // Keep last 1000
    
    // Vercel Postgres:
    // import { sql } from '@vercel/postgres';
    // await sql`
    //   INSERT INTO game_stats (
    //     match_id, timestamp, map_name, game_mode, player_count,
    //     winning_team, win_condition, total_kills, task_completion_rate, data
    //   ) VALUES (
    //     ${stats.matchId}, ${stats.timestamp}, ${stats.mapName}, 
    //     ${stats.gameMode}, ${stats.playerCount}, ${stats.winningTeam},
    //     ${stats.winCondition}, ${stats.totalKills}, ${stats.taskCompletionRate},
    //     ${JSON.stringify(stats)}
    //   )
    // `;
    
    // MongoDB Atlas:
    // import { MongoClient } from 'mongodb';
    // const client = new MongoClient(process.env.MONGODB_URI!);
    // await client.connect();
    // await client.db('ausummary').collection('game_stats').insertOne({
    //   ...stats,
    //   _id: stats.matchId,
    //   receivedAt: new Date()
    // });
    // await client.close();
    
    // Supabase:
    // import { createClient } from '@supabase/supabase-js';
    // const supabase = createClient(
    //   process.env.SUPABASE_URL!,
    //   process.env.SUPABASE_KEY!
    // );
    // await supabase.from('game_stats').insert({
    //   match_id: stats.matchId,
    //   data: stats,
    //   timestamp: stats.timestamp
    // });

    return res.status(200).json({ 
      success: true,
      message: 'Stats received and stored successfully',
      gamesStored: gamesData.length
    });

  } catch (error) {
    console.error('❌ Error processing stats:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

// Export for analytics endpoint
export { gamesData };

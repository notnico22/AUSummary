import type { VercelRequest, VercelResponse } from '@vercel/node';

// Global in-memory storage (shared across all functions in this deployment)
// Note: This will reset on each new deployment. Use a database for production.
global.gamesData = global.gamesData || [];

/**
 * API endpoint to receive game statistics from AUSUMMARY mod
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

    // Log the received stats
    console.log('📊 Received game stats:', {
      matchId: stats.matchId,
      map: stats.mapName,
      winner: stats.winningTeam,
      players: stats.playerCount,
      timestamp: stats.timestamp
    });

    // Store in global memory
    global.gamesData.push(stats);
    
    // Keep only last 1000 games to prevent memory issues
    if (global.gamesData.length > 1000) {
      global.gamesData = global.gamesData.slice(-1000);
    }

    console.log(`✅ Total games stored: ${global.gamesData.length}`);

    return res.status(200).json({ 
      success: true,
      message: 'Stats received and stored successfully',
      gamesStored: global.gamesData.length
    });

  } catch (error) {
    console.error('❌ Error processing stats:', error);
    return res.status(500).json({ 
      error: 'Internal server error',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

import { kv } from '@vercel/kv';
import type { VercelRequest, VercelResponse } from '@vercel/node';

/**
 * Fetch all games from Vercel KV storage
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET');

    // Get all game IDs sorted by timestamp (newest first)
    const gameIds = await kv.zrange<string[]>('games:all', 0, -1, { rev: true });
    
    if (!gameIds || gameIds.length === 0) {
      return res.status(200).json({
        success: true,
        total: 0,
        games: []
      });
    }

    // Fetch all games in parallel
    const gamePromises = gameIds.map(id => kv.get(`game:${id}`));
    const games = await Promise.all(gamePromises);

    // Filter out any null values
    const validGames = games.filter(g => g !== null);

    return res.status(200).json({
      success: true,
      total: validGames.length,
      games: validGames
    });

  } catch (error) {
    console.error('Error fetching all games:', error);
    return res.status(500).json({
      error: 'Failed to fetch games',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

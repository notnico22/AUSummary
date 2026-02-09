import type { VercelRequest, VercelResponse } from '@vercel/node';

/**
 * Returns all stored game data
 * Used by the all-games.html admin panel
 */
export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    // Import the in-memory storage
    const { gamesData } = await import('./stats');

    // Enable CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET');

    // Return all games sorted by timestamp (newest first)
    const sortedGames = [...gamesData].sort((a, b) => 
      new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );

    return res.status(200).json({
      success: true,
      total: sortedGames.length,
      games: sortedGames
    });

  } catch (error) {
    console.error('Error fetching all games:', error);
    return res.status(500).json({
      error: 'Failed to fetch games',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

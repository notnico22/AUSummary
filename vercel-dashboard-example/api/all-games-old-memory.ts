import type { VercelRequest, VercelResponse } from '@vercel/node';

declare global {
  var gamesData: any[];
}

export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    if (!global.gamesData) {
      global.gamesData = [];
    }

    const sortedGames = [...global.gamesData].sort((a, b) => 
      new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );

    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET');

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

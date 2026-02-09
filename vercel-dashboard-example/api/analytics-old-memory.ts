import type { VercelRequest, VercelResponse } from '@vercel/node';

// Access the global games data
declare global {
  var gamesData: any[];
}

interface AnalyticsData {
  totalGames: number;
  totalPlayers: number;
  popularMaps: { [key: string]: number };
  winRates: {
    crewmate: number;
    impostor: number;
  };
  averageGameDuration: number;
  totalKills: number;
  popularRoles: { [key: string]: number };
  gamesPerDay: { [key: string]: number };
  recentGames: any[];
}

export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    // Initialize global storage if it doesn't exist
    if (!global.gamesData) {
      global.gamesData = [];
    }

    const games = global.gamesData;

    if (games.length === 0) {
      return res.status(200).json({
        totalGames: 0,
        totalPlayers: 0,
        popularMaps: {},
        winRates: { crewmate: 0, impostor: 0 },
        averageGameDuration: 0,
        totalKills: 0,
        popularRoles: {},
        gamesPerDay: {},
        recentGames: []
      });
    }

    const analytics = calculateAnalytics(games);
    
    // Enable CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET');
    
    return res.status(200).json(analytics);

  } catch (error) {
    console.error('Error fetching analytics:', error);
    return res.status(500).json({ 
      error: 'Failed to fetch analytics',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

function calculateAnalytics(games: any[]): AnalyticsData {
  const analytics: AnalyticsData = {
    totalGames: games.length,
    totalPlayers: 0,
    popularMaps: {},
    winRates: { crewmate: 0, impostor: 0 },
    averageGameDuration: 0,
    totalKills: 0,
    popularRoles: {},
    gamesPerDay: {},
    recentGames: []
  };

  let crewWins = 0;
  let impWins = 0;
  let totalDuration = 0;

  games.forEach(game => {
    // Count maps
    const map = game.mapName || 'Unknown';
    analytics.popularMaps[map] = (analytics.popularMaps[map] || 0) + 1;

    // Count wins
    if (game.winningTeam === 'Crewmate') crewWins++;
    if (game.winningTeam === 'Impostor') impWins++;

    // Sum durations
    totalDuration += game.gameDuration || 0;

    // Sum kills
    analytics.totalKills += game.totalKills || 0;

    // Count roles
    if (game.roles && Array.isArray(game.roles)) {
      game.roles.forEach((roleData: any) => {
        const role = roleData.role || 'Unknown';
        analytics.popularRoles[role] = (analytics.popularRoles[role] || 0) + 1;
      });
      
      analytics.totalPlayers += game.roles.length;
    }

    // Games per day
    const date = new Date(game.timestamp).toISOString().split('T')[0];
    analytics.gamesPerDay[date] = (analytics.gamesPerDay[date] || 0) + 1;
  });

  // Calculate averages
  if (games.length > 0) {
    analytics.winRates.crewmate = crewWins / games.length;
    analytics.winRates.impostor = impWins / games.length;
    analytics.averageGameDuration = totalDuration / games.length;
  }

  // Get recent games (last 20)
  analytics.recentGames = games
    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
    .slice(0, 20)
    .map(game => ({
      timestamp: game.timestamp,
      mapName: game.mapName,
      playerCount: game.playerCount,
      winningTeam: game.winningTeam,
      gameDuration: game.gameDuration,
      totalKills: game.totalKills,
      taskCompletionRate: game.taskCompletionRate || 0
    }));

  return analytics;
}

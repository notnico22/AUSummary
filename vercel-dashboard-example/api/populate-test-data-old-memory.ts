import type { VercelRequest, VercelResponse } from '@vercel/node';

declare global {
  var gamesData: any[];
}

export default async function handler(req: VercelRequest, res: VercelResponse) {
  try {
    // Initialize if needed
    if (!global.gamesData) {
      global.gamesData = [];
    }

    // Generate test games
    const testGames = [];
    const maps = ['The Skeld', 'MIRA HQ', 'Polus', 'The Airship', 'The Fungle'];
    const teams = ['Crewmate', 'Impostor'];
    const roles = ['Crewmate', 'Impostor', 'Engineer', 'Scientist', 'Shapeshifter', 'Guardian Angel'];

    for (let i = 0; i < 50; i++) {
      const playerCount = 8 + Math.floor(Math.random() * 7);
      const winningTeam = teams[Math.floor(Math.random() * teams.length)];
      const gameDuration = 300 + Math.floor(Math.random() * 600);

      const roleData = [];
      for (let j = 0; j < playerCount; j++) {
        roleData.push({
          role: roles[Math.floor(Math.random() * roles.length)],
          team: Math.random() > 0.7 ? 'Impostor' : 'Crewmate',
          survived: Math.random() > 0.4,
          killCount: Math.floor(Math.random() * 3),
          tasksCompleted: Math.floor(Math.random() * 10),
          totalTasks: 10
        });
      }

      const game = {
        matchId: `test-${Date.now()}-${i}`,
        timestamp: new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000).toISOString(),
        mapName: maps[Math.floor(Math.random() * maps.length)],
        gameMode: 'Classic',
        playerCount: playerCount,
        gameDuration: gameDuration,
        winningTeam: winningTeam,
        winCondition: winningTeam === 'Crewmate' ? 
          (Math.random() > 0.5 ? 'HumansByTask' : 'HumansByVote') :
          (Math.random() > 0.5 ? 'ImpostorByKill' : 'ImpostorBySabotage'),
        totalKills: Math.floor(Math.random() * 8),
        totalEjections: Math.floor(Math.random() * 3),
        taskCompletionRate: 0.3 + Math.random() * 0.6,
        modVersion: '2.0.0',
        roles: roleData
      };

      testGames.push(game);
    }

    global.gamesData.push(...testGames);

    return res.status(200).json({
      success: true,
      message: 'Test data populated successfully',
      gamesAdded: testGames.length,
      totalGames: global.gamesData.length
    });

  } catch (error) {
    console.error('Error populating test data:', error);
    return res.status(500).json({
      error: 'Failed to populate test data',
      message: error instanceof Error ? error.message : 'Unknown error'
    });
  }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AUSUMMARY.Shared.Models;

/// <summary>
/// Represents a complete Among Us match summary with all game data
/// </summary>
public class GameSummary
{
    [JsonProperty("matchId")]
    public string MatchId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonProperty("metadata")]
    public GameMetadata Metadata { get; set; } = new();

    [JsonProperty("players")]
    public List<PlayerSnapshot> Players { get; set; } = new();

    [JsonProperty("events")]
    public List<GameEvent> Events { get; set; } = new();

    [JsonProperty("winner")]
    public WinnerInfo Winner { get; set; } = new();

    [JsonProperty("statistics")]
    public MatchStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Metadata about the game match
/// </summary>
public class GameMetadata
{
    [JsonProperty("mapName")]
    public string MapName { get; set; } = "Unknown";

    [JsonProperty("gameMode")]
    public string GameMode { get; set; } = "Classic";

    [JsonProperty("playerCount")]
    public int PlayerCount { get; set; }

    [JsonProperty("gameDuration")]
    public TimeSpan GameDuration { get; set; }

    [JsonProperty("totalMeetings")]
    public int TotalMeetings { get; set; }

    [JsonProperty("totalTasks")]
    public int TotalTasks { get; set; }

    [JsonProperty("completedTasks")]
    public int CompletedTasks { get; set; }

    [JsonProperty("modVersion")]
    public string ModVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Snapshot of a player's game state
/// </summary>
public class PlayerSnapshot
{
    [JsonProperty("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonProperty("playerId")]
    public byte PlayerId { get; set; }

    [JsonProperty("colorName")]
    public string ColorName { get; set; } = "";

    [JsonProperty("colorId")]
    public int ColorId { get; set; }

    [JsonProperty("hatId")]
    public string HatId { get; set; } = "";

    [JsonProperty("petId")]
    public string PetId { get; set; } = "";

    [JsonProperty("skinId")]
    public string SkinId { get; set; } = "";

    [JsonProperty("visorId")]
    public string VisorId { get; set; } = "";

    [JsonProperty("nameplateId")]
    public string NameplateId { get; set; } = "";

    [JsonProperty("role")]
    public string Role { get; set; } = "Crewmate";

    [JsonProperty("team")]
    public string Team { get; set; } = "Crewmate";

    [JsonProperty("modifiers")]
    public List<string> Modifiers { get; set; } = new();

    [JsonProperty("isAlive")]
    public bool IsAlive { get; set; } = true;

    [JsonProperty("deathCause")]
    public string? DeathCause { get; set; }

    [JsonProperty("killType")]
    public string? KillType { get; set; }

    [JsonProperty("timeOfDeath")]
    public float? TimeOfDeath { get; set; }

    [JsonProperty("killedBy")]
    public string? KilledBy { get; set; }

    [JsonProperty("killCount")]
    public int KillCount { get; set; }

    [JsonProperty("tasksCompleted")]
    public int TasksCompleted { get; set; }

    [JsonProperty("totalTasks")]
    public int TotalTasks { get; set; }

    [JsonProperty("wasEjected")]
    public bool WasEjected { get; set; }

    [JsonProperty("survivedRounds")]
    public int SurvivedRounds { get; set; }
}

/// <summary>
/// Represents a game event (meeting, death, etc)
/// </summary>
public class GameEvent
{
    [JsonProperty("eventType")]
    public string EventType { get; set; } = "";

    [JsonProperty("timestamp")]
    public float Timestamp { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("involvedPlayers")]
    public List<string> InvolvedPlayers { get; set; } = new();

    [JsonProperty("data")]
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Information about the winning team/players
/// </summary>
public class WinnerInfo
{
    [JsonProperty("winningTeam")]
    public string WinningTeam { get; set; } = "";

    [JsonProperty("winCondition")]
    public string WinCondition { get; set; } = "";

    [JsonProperty("winners")]
    public List<string> Winners { get; set; } = new();

    [JsonProperty("mvp")]
    public string? Mvp { get; set; }
}


public class MatchStatistics
{
    [JsonProperty("totalKills")]
    public int TotalKills { get; set; }

    [JsonProperty("totalEjections")]
    public int TotalEjections { get; set; }

    [JsonProperty("totalDeaths")]
    public int TotalDeaths { get; set; }

    [JsonProperty("taskCompletionRate")]
    public float TaskCompletionRate { get; set; }

    [JsonProperty("averageMeetingTime")]
    public float AverageMeetingTime { get; set; }

    [JsonProperty("impostorWinRate")]
    public float ImpostorWinRate { get; set; }

    [JsonProperty("crewmateWinRate")]
    public float CrewmateWinRate { get; set; }
}

/// <summary>
/// Enumeration of possible death causes
/// </summary>
public enum DeathCause
{
    Unknown,
    Killed,
    Ejected,
    Suicide,
    Disconnected,
    Sabotage
}

/// <summary>
/// Enumeration of event types
/// </summary>
public enum EventType
{
    GameStart,
    GameEnd,
    MeetingCalled,
    EmergencyMeeting,
    BodyReported,
    PlayerVoted,
    PlayerEjected,
    PlayerKilled,
    TaskCompleted,
    SabotageStarted,
    SabotageFixed
}

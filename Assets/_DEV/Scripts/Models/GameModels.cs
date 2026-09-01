using System;
using System.Collections.Generic;

// ─── Config (GET /games/configs) ─────────────────────────────────────────────

[Serializable]
public class GameConfig
{
    public string name;
    public int energy_consumption;
    public int coins_rate;
    public int experience_rate;
    public List<GameReward> rewards;
}

[Serializable]
public class GameReward
{
    public string type;
    public int amount;
}

// ─── Start (POST /games/start) ────────────────────────────────────────────────

[Serializable]
public class GameStartRequest
{
    public string name;
}

[Serializable]
public class UserGame
{
    public string _id;
    public string user;
    public string game_config;
    public bool is_active;
    public string createdAt;
    public string updatedAt;
}

[Serializable]
public class GameStartResponse
{
    public string message;
    public UserGame userGame;
}

// ─── End (POST /games/end) ────────────────────────────────────────────────────

[Serializable]
public class GameEndRequest
{
    public int points;
}

// ─── Immutable result types ───────────────────────────────────────────────────

public class GameConfigsResult
{
    public bool Success { get; private set; }
    public List<GameConfig> Configs { get; private set; }
    public string Error { get; private set; }

    public static GameConfigsResult Ok(List<GameConfig> configs) => new GameConfigsResult { Success = true, Configs = configs };
    public static GameConfigsResult Fail(string error)           => new GameConfigsResult { Success = false, Error = error };
}

public class GameStartResult
{
    public bool Success { get; private set; }
    public UserGame UserGame { get; private set; }
    public string Message { get; private set; }
    public string Error { get; private set; }
    public long ResponseCode { get; private set; }

    public static GameStartResult Ok(UserGame userGame, string message) => new GameStartResult { Success = true, UserGame = userGame, Message = message };
    public static GameStartResult Fail(string error, long code = 0)     => new GameStartResult { Success = false, Error = error, ResponseCode = code };
}

public class GameEndResult
{
    public bool Success { get; private set; }
    public PlayerProgress Progress { get; private set; }
    public string Error { get; private set; }
    public long ResponseCode { get; private set; }

    public static GameEndResult Ok(PlayerProgress progress) => new GameEndResult { Success = true, Progress = progress };
    public static GameEndResult Fail(string error, long code = 0) => new GameEndResult { Success = false, Error = error, ResponseCode = code };
}

// Quick Match tuning values - the single source for each, never duplicate them elsewhere.
public static class MatchmakingConfig
{
    // A match always has six participants; bots fill empty seats (FD-07).
    public const int MaxPlayers = 6;

    // The match countdown: every match starts this many seconds after the search began (solo, a
    // partial lobby or a full one), bots filling the empty seats. Also the search timer's ceiling.
    public const float SearchDurationSeconds = 30f;

    // Seconds each extra player in the lobby takes off the countdown. 0 = the countdown is always
    // 120 s; a full lobby of ready players starts at once instead (MatchStartRule).
    // (Was 15: 1 player 120 s, 2 players 105 s ... 6 players 45 s.)
    public const float SecondsOffPerExtraPlayer = 0f;

    // Old rule, no longer used to start a match (MatchStartRule replaced it). The flow controller
    // still uses it to name its phases.
    public const float BotOptionUnlockSeconds = 30f;

    // "Player found! Joining lobby in N".
    public const float LobbyJoinCountdownSeconds = 3f;

    // Host migration: time for players to reconnect before their cars become bots...
    public const float ReconnectWindowSeconds = 10f;
    // ...then the shared "Game resumes in N" countdown.
    public const float ResumeCountdownSeconds = 3f;
    // Minimum time the "reconnecting" notice stays up, so it doesn't flicker.
    public const float ReconnectingNoticeSeconds = 1.5f;
    // (Snapshot frequency is NetworkProjectConfig > HostMigration > UpdateDelay.)

    // Stamped into SessionProperties to separate this game's sessions on a shared App ID.
    public const string GameId = "ctf";
}

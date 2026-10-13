// Quick Match tuning values - the single source for each, never duplicate them elsewhere.
public static class MatchmakingConfig
{
    // A match always has six participants; bots fill empty seats (FD-07).
    public const int MaxPlayers = 6;

    // Display ceiling of the search timer.
    public const float SearchDurationSeconds = 120f;

    // The bot option ("start with computer players" / ready toggle) unlocks at this timer value.
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

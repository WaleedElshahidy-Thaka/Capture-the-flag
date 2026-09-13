// Single named source of truth for the values this milestone's Find Match flow tunes on —
// nothing here should ever be hardcoded again at a second call site.
public static class MatchmakingConfig
{
    public const int MaxPlayers = 4;

    // The displayed search timer's hard ceiling - counts UP from 0 to this. Purely a display
    // cap / TickTimer duration, separate from when the bot-fill option actually unlocks.
    public const float SearchDurationSeconds = 120f;

    // The bot-fill option (solo "start with computer players" button, or the per-player ready
    // toggle for 2-3 real players) becomes available once this many seconds have elapsed -
    // well before the timer's 120s display cap.
    public const float BotOptionUnlockSeconds = 30f;

    // Stamped into SessionProperties so this game's sessions can eventually be told apart
    // from another game's if this Photon App ID is ever shared across games.
    public const string GameId = "ctf";
}

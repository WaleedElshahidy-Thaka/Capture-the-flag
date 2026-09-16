// Single named source of truth for the values the Quick Match flow tunes on — nothing here
// should ever be hardcoded again at a second call site.
public static class MatchmakingConfig
{
    // Six, per Glowtag FD-07: the lobby fills every empty slot with a bot, so a match always has
    // exactly six participants and the partial-field cases earlier drafts planned around no
    // longer occur. FD-06 requires six start anchors to match.
    public const int MaxPlayers = 6;

    // Display ceiling of the search timer. Each player's timer starts at 0 on their own Quick
    // Match press; once two or more are searching together the shown timer is the highest of
    // theirs (the shared timer).
    public const float SearchDurationSeconds = 120f;

    // The bot option unlocks once the (shared) timer reaches this. Solo: "start with computer
    // players" starts immediately. With 2+ players: it becomes a ready toggle, and the match
    // starts with bots once every searching player is ready.
    public const float BotOptionUnlockSeconds = 30f;

    // "Player found! Joining lobby in N" - counted down by every matched searcher from the same
    // host-stamped tick, so the cars appear to each other on the same tick everywhere.
    public const float LobbyJoinCountdownSeconds = 3f;

    // Host migration. After the new host restores the match, players have this long to
    // reconnect before the round resumes without them (their cars become bots)...
    public const float ReconnectWindowSeconds = 10f;
    // ...and once everyone is back (or the window closes) this is the "Game resumes in N" that
    // every peer counts down together to the same tick. The wait before it is out of our hands
    // (Photon's host-loss detection, election, reconnects), which is why the UI shows no number
    // for that part.
    public const float ResumeCountdownSeconds = 3f;
    // A clean host quit is detected almost instantly and a lobby hand-over completes in well
    // under a second, which showed the "reconnecting" notice for a few frames - a flicker. It
    // stays up at least this long so it reads as a message.
    public const float ReconnectingNoticeSeconds = 1.5f;
    // (How often the host pushes a migration snapshot lives in NetworkProjectConfig >
    // HostMigration > UpdateDelay - play rewinds to the last snapshot on a hand-over.)

    // Stamped into SessionProperties so this game's sessions can eventually be told apart
    // from another game's if this Photon App ID is ever shared across games.
    public const string GameId = "ctf";
}

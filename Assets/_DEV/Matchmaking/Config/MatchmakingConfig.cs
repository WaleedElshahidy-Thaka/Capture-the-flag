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

    // Stamped into SessionProperties so this game's sessions can eventually be told apart
    // from another game's if this Photon App ID is ever shared across games.
    public const string GameId = "ctf";
}

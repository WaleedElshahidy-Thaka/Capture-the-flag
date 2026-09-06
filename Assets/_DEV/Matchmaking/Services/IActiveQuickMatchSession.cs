using System;

// Represents "the Quick Match session I'm currently in". Returned by IQuickMatchService on
// success. Everything MatchmakingFlowController needs lives here so it never touches Fusion
// types directly - QuickMatchLocalService and QuickMatchFusionService both satisfy this
// exact same contract, one with a fake single-player session, one backed by a live
// NetworkRunner. Trimmed sibling of the shelved IActiveRoomSession - no RoomCode/IsVisible/
// MakeVisible, since Quick Match has no room codes or visibility choice.
public interface IActiveQuickMatchSession
{
    int PlayerCount { get; }
    int MaxPlayers { get; }
    bool IsHost { get; }

    // Search timer. Counts UP from 0 to MatchmakingConfig.SearchDurationSeconds. Network-
    // synchronized in the Fusion backend (see FusionActiveQuickMatchSession) so a player
    // joining mid-search sees the true elapsed time, not a fresh countdown.
    float ElapsedSeconds { get; }

    // True once MatchmakingConfig.BotOptionUnlockSeconds has elapsed - well before the
    // timer's full 120s display cap. Gates the solo bot button / ready toggle appearing.
    bool BotOptionUnlocked { get; }

    // This client's own ready state. Meaningless while solo.
    bool IsReady { get; }
    void SetReady(bool ready);

    // Solo-only: skip waiting out the timer and start immediately, bots filling every other slot.
    void RequestStartWithBots();

    // Set once the start decision has been made - by whichever peer holds authority to
    // decide (the host, in the Fusion backend): room filled naturally, every present real
    // player is ready, or the solo bot request. BotCount is resolved at that same moment
    // and never changes after.
    bool MatchStarting { get; }
    int BotCount { get; }

    // Raised whenever any of the above may have changed.
    event Action Changed;

    void Leave();
}

using System;

// Represents "the Quick Match session I'm connected to" - joined automatically on scene start.
// Everything MatchmakingFlowController needs lives here so it never touches Fusion types
// directly; QuickMatchLocalService and QuickMatchFusionService both satisfy this same contract.
public interface IActiveQuickMatchSession
{
    int PlayerCount { get; }
    int MaxPlayers { get; }
    bool IsHost { get; }

    // True once this client's own car has been spawned and seated - the loading screen
    // stays up until then.
    bool IsLocalPlayerSpawned { get; }

    // How many connected players have pressed Quick Match, as opposed to just being in the
    // arena. This is what counts toward a match.
    int SearchingCount { get; }
    bool IsSearching { get; }
    void SetSearching(bool searching);

    // The displayed timer. Alone, it's your own (from your Quick Match press). With 2+ players
    // searching together it's the highest of theirs - the shared timer.
    float ElapsedSeconds { get; }

    // True once ElapsedSeconds passes MatchmakingConfig.BotOptionUnlockSeconds.
    bool BotOptionUnlocked { get; }

    // This client's own ready state (2+ players, after the bot option unlocked).
    bool IsReady { get; }
    void SetReady(bool ready);

    // Solo-only: start immediately, bots filling every other slot.
    void RequestStartWithBots();

    // Set once the start decision has been made by the authority (the host, in the Fusion
    // backend): room filled naturally, every searching player ready, or the solo bot request.
    // BotCount is resolved at that same moment and never changes after.
    bool MatchStarting { get; }
    int BotCount { get; }

    // Raised whenever any of the above may have changed.
    event Action Changed;

    void Leave();
}

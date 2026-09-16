using System;

// Represents "the Quick Match session I'm connected to" - joined automatically on scene start.
// Everything MatchmakingFlowController needs lives here so it never touches Fusion types
// directly; QuickMatchLocalService and QuickMatchFusionService both satisfy this same contract.
public interface IActiveQuickMatchSession
{
    int PlayerCount { get; }
    int MaxPlayers { get; }
    bool IsHost { get; }

    // True once this client's own car has been spawned and seated - the searching request
    // waits for it (it's an RPC from the car).
    bool IsLocalPlayerSpawned { get; }

    // alreadyElapsedSeconds: how long the local timer has been running before the session knew
    // - the click happens before the connection - so the session's timer starts back-dated.
    bool IsSearching { get; }
    void SetSearching(bool searching, float alreadyElapsedSeconds = 0f);

    // The moment a second searcher appears, the authority pairs everyone searching: each counts
    // the same LobbyJoinCountdownSeconds down ("Player found! Joining lobby in N") and enters
    // the lobby on the same tick. > 0 only while that countdown is running.
    float LobbyJoinSecondsRemaining { get; }
    bool InLobby { get; }

    // Players whose countdown has finished - the ones you can see, and whose timers are
    // shared. This is what counts toward a match.
    int LobbyCount { get; }

    // The session's timer. Before you're in the lobby it's your own (back-dated to your Quick
    // Match press). In the lobby it's the highest of everyone there - the shared timer.
    float ElapsedSeconds { get; }

    // True once ElapsedSeconds passes MatchmakingConfig.BotOptionUnlockSeconds.
    bool BotOptionUnlocked { get; }

    // After a host migration: every car holds still until the new host has everyone back (or
    // gives up waiting), then all peers count down to the same tick. ResumeSecondsRemaining is
    // negative while still waiting - the UI shows no number it can't promise.
    bool IsFrozen { get; }
    float ResumeSecondsRemaining { get; }

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

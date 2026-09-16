using System;
using System.Collections;

public interface IQuickMatchService
{
    IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult);
    void CancelQuickMatch();

    // Host migration, as seen from the flow. HostLost: the session we were in is gone and a
    // hand-over is under way - the old IActiveQuickMatchSession is dead from this moment.
    // SessionRestored: we're in the new session (as its host or as a client); the cars are back
    // and MatchmakingSessionState decides when play resumes. SessionFailed: the hand-over
    // couldn't complete; the caller starts over as if the game had just been opened.
    event Action HostLost;
    event Action<IActiveQuickMatchSession> SessionRestored;
    event Action<string> SessionFailed;
}

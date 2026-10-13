using System;
using System.Collections;

// Connects to (and cancels) a Quick Match, and reports host-migration events.
public interface IQuickMatchService
{
    IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult);
    void CancelQuickMatch();

    // The host dropped; the current session is dead from this moment.
    event Action HostLost;
    // We're in the new session after a host migration.
    event Action<IActiveQuickMatchSession> SessionRestored;
    // The hand-over failed; start over from the menu.
    event Action<string> SessionFailed;
}

using System;
using System.Collections;

public interface IQuickMatchService
{
    IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult);
    void CancelQuickMatch();
}

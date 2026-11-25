using System;
using System.Collections;

public interface IGameService
{
    IEnumerator FetchConfigs(string token, Action<GameConfigsResult> onResult);
    IEnumerator StartGame(string token, string configName, Action<GameStartResult> onResult);
    IEnumerator EndGame(string token, int points, Action<GameEndResult> onResult);
}

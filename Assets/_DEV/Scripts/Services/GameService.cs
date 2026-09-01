using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GameService : IGameService
{
    private const string BASE_URL   = "https://dev.01skills.com/api/v2";
    private const string CONFIGS_URL = BASE_URL + "/games/configs";
    private const string START_URL   = BASE_URL + "/games/start";
    private const string END_URL     = BASE_URL + "/games/end";

    public IEnumerator FetchConfigs(string token, Action<GameConfigsResult> onResult)
    {
        using (var request = UnityWebRequest.Get(CONFIGS_URL))
        {
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            Debug.Log($"[GameService] GET /games/configs → {request.responseCode}\n{request.downloadHandler.text}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(GameConfigsResult.Fail($"{request.error} (code {request.responseCode})"));
                yield break;
            }

            try
            {
                string arrayJson = ExtractArray(request.downloadHandler.text, "configs");
                string wrapped   = $"{{\"items\":{arrayJson}}}";
                var    wrapper   = JsonUtility.FromJson<ConfigsWrapper>(wrapped);
                onResult?.Invoke(GameConfigsResult.Ok(wrapper.items ?? new List<GameConfig>()));
            }
            catch (Exception ex)
            {
                onResult?.Invoke(GameConfigsResult.Fail($"Parse error: {ex.Message}"));
            }
        }
    }

    public IEnumerator StartGame(string token, string configName, Action<GameStartResult> onResult)
    {
        string json = JsonUtility.ToJson(new GameStartRequest { name = configName });
        Debug.Log($"[GameService] POST /games/start → body: {json}");

        using (var request = new UnityWebRequest(START_URL, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            Debug.Log($"[GameService] POST /games/start ← {request.responseCode}\n{request.downloadHandler.text}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(GameStartResult.Fail(request.error, request.responseCode));
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<GameStartResponse>(request.downloadHandler.text);
                onResult?.Invoke(GameStartResult.Ok(response.userGame, response.message));
            }
            catch (Exception ex)
            {
                onResult?.Invoke(GameStartResult.Fail($"Parse error: {ex.Message}"));
            }
        }
    }

    public IEnumerator EndGame(string token, int points, Action<GameEndResult> onResult)
    {
        string json = JsonUtility.ToJson(new GameEndRequest { points = points });
        Debug.Log($"[GameService] POST /games/end → body: {json}");

        using (var request = new UnityWebRequest(END_URL, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            Debug.Log($"[GameService] POST /games/end ← {request.responseCode}\n{request.downloadHandler.text}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(GameEndResult.Fail(request.error, request.responseCode));
                yield break;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<EndGameWrapper>(request.downloadHandler.text);
                PlayerDataStore.Instance.SetProgress(wrapper.progress);
                onResult?.Invoke(GameEndResult.Ok(wrapper.progress));
            }
            catch (Exception ex)
            {
                onResult?.Invoke(GameEndResult.Fail($"Parse error: {ex.Message}"));
            }
        }
    }

    // Replicates ApiService bracket-matching without inheriting from it.
    private static string ExtractArray(string json, string fieldName)
    {
        string search = $"\"{fieldName}\":";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start == -1) { Debug.LogWarning($"[GameService] Field '{fieldName}' not found."); return "[]"; }

        start += search.Length;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;

        if (start >= json.Length || json[start] != '[')
        { Debug.LogWarning($"[GameService] Field '{fieldName}' is not an array."); return "[]"; }

        int depth = 0, end = start;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            if (json[i] == ']') depth--;
            if (depth == 0) { end = i; break; }
        }
        return json.Substring(start, end - start + 1);
    }

    [Serializable]
    private class ConfigsWrapper { public List<GameConfig> items; }

    [Serializable]
    private class EndGameWrapper { public PlayerProgress progress; }
}

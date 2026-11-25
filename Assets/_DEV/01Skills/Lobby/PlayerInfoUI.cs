using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// Shows the local lobby player's name and level in existing TMP texts.
//   name  = --playerName (DataTransferHandler.playerName), or "Player" if empty
//   level = --level, replaced by PlayerDataStore.Level whenever progress arrives
//           (e.g. POST /games/end returns new progress, so the level can rise after a match)
// If --level is missing but a token exists, fetches GET /users/progress once per session.
// Either text may be left empty.
public class PlayerInfoUI : MonoBehaviour
{
    const string ProgressUrl = "https://dev.01skills.com/api/v2/users/progress";

    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text levelText;
    [SerializeField] string fallbackName = "Player";
    [Tooltip("{0} = level number.")]
    [SerializeField] string levelFormat = "LV. {0}";
    [Tooltip("Hide the level text while the level is unknown (0).")]
    [SerializeField] bool hideUnknownLevel = true;

    static bool fetchStarted;   // one progress request per session, however many PlayerInfoUIs exist

    void Awake() => PlayerDataStore.OnProgressChanged += Refresh;
    void OnDestroy() => PlayerDataStore.OnProgressChanged -= Refresh;

    void Start()
    {
        Refresh();

        bool levelMissing = DataTransferHandler.playerLevel <= 0;
        if (levelMissing && !fetchStarted && AuthTokenStore.Instance.HasToken && !PlayerDataStore.Instance.HasProgress)
        {
            fetchStarted = true;
            StartCoroutine(FetchProgress(AuthTokenStore.Instance.Token));
        }
    }

    void Refresh()
    {
        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(DataTransferHandler.playerName) ? fallbackName : DataTransferHandler.playerName;

        if (levelText != null)
        {
            int level = PlayerLobbyProfile.LocalLevel;
            bool show = level > 0 || !hideUnknownLevel;
            levelText.gameObject.SetActive(show);
            if (show) levelText.text = string.Format(levelFormat, level);
        }
    }

    static IEnumerator FetchProgress(string token)
    {
        using (var request = UnityWebRequest.Get(ProgressUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return request.SendWebRequest();

            string raw = request.downloadHandler != null ? request.downloadHandler.text : "";
            Debug.Log($"[PlayerInfoUI] GET /users/progress -> {request.responseCode}\n{raw}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PlayerInfoUI] Progress request failed: {request.error}");
                yield break;
            }

            PlayerProgress progress = null;
            try { progress = ParseProgress(raw); }
            catch (Exception ex) { Debug.LogWarning($"[PlayerInfoUI] Could not parse progress: {ex.Message}"); }

            if (progress != null) PlayerDataStore.Instance.SetProgress(progress);
            else Debug.LogWarning("[PlayerInfoUI] Progress response had no recognisable progress object.");
        }
    }

    // The exact shape isn't confirmed, so accept: {"progress":{...}}, {"data":{"progress":{...}}},
    // {"data":{...}} or the progress object itself. JsonUtility never returns null nested objects,
    // so the key names decide which shape it is.
    static PlayerProgress ParseProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        if (json.Contains("\"data\""))
        {
            if (json.Contains("\"progress\"")) return JsonUtility.FromJson<DataProgressWrapper>(json).data.progress;
            return JsonUtility.FromJson<DataWrapper>(json).data;
        }
        if (json.Contains("\"progress\"")) return JsonUtility.FromJson<ProgressWrapper>(json).progress;
        if (json.Contains("\"level\"")) return JsonUtility.FromJson<PlayerProgress>(json);
        return null;
    }

    [Serializable] class ProgressWrapper { public PlayerProgress progress; }
    [Serializable] class DataWrapper { public PlayerProgress data; }
    [Serializable] class DataProgressWrapper { public ProgressWrapper data; }
}

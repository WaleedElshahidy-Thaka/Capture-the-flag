using UnityEngine;

// Reads the 01Skills lobby's launch arguments (--key=value) into the shared static stores, before
// any other script's Awake. Put it on one object in the first scene (Game-Arena).
//
// The editorDebug* fields are used ONLY in the Editor, and only when the argument is missing, so a
// build never falls back to Inspector values. Never save a real token in editorDebugToken - type it
// in during Play Mode testing only.
[DefaultExecutionOrder(-100)]
public class LobbyArgsReader : MonoBehaviour
{
    [Header("Editor Testing (used only in the Editor when the argument is missing)")]
    [Tooltip("--token=. Do NOT save a real token in the scene.")]
    [SerializeField] string editorDebugToken;
    [Tooltip("--bot=. Robot name exactly as in BotSkinLibrary (Gravy, RN, Spark, Core, Cyclone, Bolt).")]
    [SerializeField] string editorDebugBot;
    [Tooltip("--skin=. Skin name as listed for that robot in BotSkinLibrary (e.g. 1..6).")]
    [SerializeField] string editorDebugSkin;
    [Tooltip("--playerName=.")]
    [SerializeField] string editorDebugPlayerName;
    [Tooltip("--level=. 0 = unknown.")]
    [SerializeField, Min(0)] int editorDebugLevel;
    [Tooltip("--lobbyPath=. Lobby .exe to relaunch on Return to Lobby.")]
    [SerializeField] string editorDebugLobbyPath;

    void Awake()
    {
        AuthTokenStore.Instance.Token = LaunchArgs.Get("token", EditorOnly(editorDebugToken, ""));

        DataTransferHandler.selectedBotName = LaunchArgs.Get("bot", EditorOnly(editorDebugBot, DataTransferHandler.selectedBotName));
        DataTransferHandler.selectedSkinName = LaunchArgs.Get("skin", EditorOnly(editorDebugSkin, DataTransferHandler.selectedSkinName));
        DataTransferHandler.playerName = LaunchArgs.Get("playerName", EditorOnly(editorDebugPlayerName, DataTransferHandler.playerName));

        string levelArg = LaunchArgs.Get("level");
        DataTransferHandler.playerLevel = int.TryParse(levelArg, out int level) ? Mathf.Max(0, level)
                                        : Application.isEditor ? editorDebugLevel : 0;

        // Needed to hand control back to the lobby (LobbyReturn).
        DataTransferHandler.lobbyPath = LaunchArgs.Get("lobbyPath", EditorOnly(editorDebugLobbyPath, DataTransferHandler.lobbyPath));
        DataTransferHandler.gamesManifestPath = LaunchArgs.Get("gamesManifest", DataTransferHandler.gamesManifestPath);

        // The networked display name everyone sees (seats, scoreboard, results) comes from PlayerIdentity.
        if (!string.IsNullOrWhiteSpace(DataTransferHandler.playerName))
            PlayerIdentity.SetLocalDisplayName(DataTransferHandler.playerName);

        Debug.Log($"[LobbyArgsReader] bot='{DataTransferHandler.selectedBotName}' skin='{DataTransferHandler.selectedSkinName}' " +
                  $"name='{DataTransferHandler.playerName}' level={DataTransferHandler.playerLevel} " +
                  $"token={(AuthTokenStore.Instance.HasToken ? "yes" : "no")} lobbyPath='{DataTransferHandler.lobbyPath}'");
    }

    // Inspector fallback in the Editor only; a build keeps the current value.
    static string EditorOnly(string debugValue, string current) =>
        Application.isEditor && !string.IsNullOrEmpty(debugValue) ? debugValue : current;
}

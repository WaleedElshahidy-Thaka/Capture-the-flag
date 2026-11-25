using UnityEngine;

// Hands control back to the 01Skills lobby: relaunches it with the token, the player's robot and skin
// (quoted, so the player keeps their choice) and the games manifest, then quits once it's drawn.
// Shared by PlayerHandler.ReturnToLobby and LobbyBridge.ReturnToLobby.
public static class LobbyReturn
{
    public static bool HasLobby => !string.IsNullOrEmpty(DataTransferHandler.lobbyPath);

    public static string BuildArguments()
    {
        // --launcher=false: already authenticated, so the lobby skips its intro.
        string arguments = $"--launcher=false --token={AuthTokenStore.Instance.Token}";

        // The lobby rebuilds itself on this path: anything not passed is gone, and the player would
        // find their robot reset to the default.
        if (!string.IsNullOrEmpty(DataTransferHandler.selectedBotName))
            arguments += $" --bot=\"{DataTransferHandler.selectedBotName}\"";
        if (!string.IsNullOrEmpty(DataTransferHandler.selectedSkinName))
            arguments += $" --skin=\"{DataTransferHandler.selectedSkinName}\"";
        if (!string.IsNullOrEmpty(DataTransferHandler.gamesManifestPath))
            arguments += $" --gamesManifest=\"{DataTransferHandler.gamesManifestPath}\"";

        return arguments;
    }

    // True when the lobby was launched (this app quits by itself once the lobby has drawn).
    // False when there's no lobby path or it failed to start - the caller decides what to do.
    public static bool TrySwitch()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        if (!HasLobby)
        {
            Debug.LogWarning("[LobbyReturn] No lobby path set (--lobbyPath= or editorDebugLobbyPath).");
            return false;
        }

        if (AppHandoff.SwitchTo(DataTransferHandler.lobbyPath, BuildArguments(), out string error))
            return true;

        Debug.LogError($"[LobbyReturn] Failed to launch lobby at '{DataTransferHandler.lobbyPath}': {error}");
#endif
        return false;
    }
}

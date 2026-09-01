using UnityEngine;

public class DataTransferHandler : MonoBehaviour
{
    // Path or URL of the lobby launcher to hand control back to on ReturnToLobby(). Set from --lobbyPath=.
    public static string lobbyPath = "";
    // Path of the games manifest the launcher wrote, set from --gamesManifest=. We never read
    // it ourselves; we only hold it so ReturnToLobby() can hand it straight back, otherwise
    // the lobby would come back up unable to tell which games are installed.
    public static string gamesManifestPath = "";
}
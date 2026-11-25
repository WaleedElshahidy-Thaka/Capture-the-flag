using UnityEngine;

public class DataTransferHandler : MonoBehaviour
{
    public static string selectedBotName = "Gravy";
    // Skin the player chose for that robot in the lobby, as named in the skin library.
    // Set from --skin=. Empty means the player never reached the skin screen, in which
    // case the robot keeps whatever skin its material was authored with.
    public static string selectedSkinName = "";
    // Path or URL of the lobby launcher to hand control back to on ReturnToLobby(). Set from --lobbyPath=.
    public static string lobbyPath = "";
    // Path of the games manifest the launcher wrote, set from --gamesManifest=. We never read
    // it ourselves; we only hold it so ReturnToLobby() can hand it straight back, otherwise
    // the lobby would come back up unable to tell which games are installed.
    public static string gamesManifestPath = "";
    // Lobby player's display name, set from --playerName=. Empty when launched without the lobby.
    public static string playerName = "";
    // Lobby player's level, set from --level=. 0 means unknown (not passed).
    public static int playerLevel = 0;
}
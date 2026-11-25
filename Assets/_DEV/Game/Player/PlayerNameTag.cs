using TMPro;
using UnityEngine;

// World-space label above a car: the player's name, plus READY while readied up in the lobby.
// Reads replicated state from PlayerMatchState, so it needs no networking of its own.
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text readyLabel;
    [Tooltip("Optional: the player's lobby level (PlayerLobbyProfile, networked).")]
    [SerializeField] TMP_Text levelLabel;
    [SerializeField] PlayerMatchState matchState;

    bool readyShown;
    int shownLevel = -1;

    void LateUpdate()
    {
        if (matchState == null || !matchState.HasState) return;

        // Comparing NetworkString to string doesn't allocate; ToString only on change.
        if (nameLabel != null && matchState.DisplayName != nameLabel.text)
            nameLabel.text = matchState.DisplayName.ToString();

        int level = PlayerLobbyProfile.DisplayLevel(PlayerLobbyProfile.LevelOf(matchState));
        if (levelLabel != null && level != shownLevel)
        {
            shownLevel = level;
            levelLabel.text = PlayerLobbyProfile.LevelLabel(level);
            if (!levelLabel.gameObject.activeSelf) levelLabel.gameObject.SetActive(true);
        }

        bool ready = matchState.IsReady && matchState.IsSearching && !matchState.CanMove;
        if (readyLabel != null && ready != readyShown)
        {
            readyShown = ready;
            readyLabel.gameObject.SetActive(ready);
        }

        CameraFacing.FaceUpright(transform);
    }
}

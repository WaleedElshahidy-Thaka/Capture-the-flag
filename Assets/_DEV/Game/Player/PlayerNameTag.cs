using TMPro;
using UnityEngine;

// World-space label above a car: the player's name, plus READY while readied up in the lobby.
// Reads replicated state from PlayerMatchState, so it needs no networking of its own.
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text readyLabel;
    [SerializeField] PlayerMatchState matchState;

    bool readyShown;

    void LateUpdate()
    {
        if (matchState == null || !matchState.HasState) return;

        // Comparing NetworkString to string doesn't allocate; ToString only on change.
        if (nameLabel != null && matchState.DisplayName != nameLabel.text)
            nameLabel.text = matchState.DisplayName.ToString();

        bool ready = matchState.IsReady && matchState.IsSearching && !matchState.CanMove;
        if (readyLabel != null && ready != readyShown)
        {
            readyShown = ready;
            readyLabel.gameObject.SetActive(ready);
        }

        CameraFacing.FaceUpright(transform);
    }
}

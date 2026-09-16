using TMPro;
using UnityEngine;

// World-space label above each car: the player's name, plus a READY line while they've
// readied up. Turned to face the camera every frame. Lives on a child of PlayerCar.prefab
// (built by GameSceneSetup); reads the networked state from the parent, so it shows the same
// thing on every peer without any networking of its own.
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text readyLabel;
    [SerializeField] PlayerMatchState matchState;

    bool readyShown;

    void LateUpdate()
    {
        if (matchState == null || !matchState.HasState) return;

        // NetworkString compares against a string without allocating; ToString only on change.
        if (nameLabel != null && matchState.DisplayName != nameLabel.text)
            nameLabel.text = matchState.DisplayName.ToString();

        // READY is a lobby fact; it comes off the moment the match starts (CanMove releases).
        bool ready = matchState.IsReady && matchState.IsSearching && !matchState.CanMove;
        if (readyLabel != null && ready != readyShown)
        {
            readyShown = ready;
            readyLabel.gameObject.SetActive(ready);
        }

        var cam = Camera.main;
        if (cam == null) return;

        // Face the camera, upright - rotate about world up only so the tag never tilts with the
        // car conforming to a slope.
        Vector3 toCamera = transform.position - cam.transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
    }
}

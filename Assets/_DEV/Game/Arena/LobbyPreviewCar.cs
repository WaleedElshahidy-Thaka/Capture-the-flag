using TMPro;
using UnityEngine;

// The robot you see before a match is found: the art in seat 0 with your name over it, purely
// local - no runner, no NetworkObject. It stands in for your car through Idle, connecting,
// searching and the "Player found" countdown; the moment you're in the lobby it goes away and
// the real networked cars take over (PlayerLobbyVisibility shows them at the same moment, and
// PlayerCamera's fade covers the swap). Built into the scene by GameSceneSetup.
public class LobbyPreviewCar : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flow;
    [SerializeField] GameObject visual;
    [SerializeField] GameObject nameTag;
    [SerializeField] TMP_Text nameLabel;

    bool shown = true;

    void Start()
    {
        if (nameLabel != null) nameLabel.text = PlayerIdentity.LocalDisplayName;
        SettleOnGround();
    }

    // Seat positions are spawn heights (the networked cars drop onto their suspension); this
    // one has no physics, so it's placed with its lowest point on the floor.
    void SettleOnGround()
    {
        if (visual == null) return;

        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Vector3 origin = bounds.center + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            transform.position += Vector3.up * (hit.point.y - bounds.min.y);
    }

    void LateUpdate()
    {
        bool show = flow == null || flow.IsSoloView;
        if (show != shown)
        {
            shown = show;
            if (visual != null) visual.SetActive(show);
            if (nameTag != null) nameTag.SetActive(show);
        }

        if (show && nameTag != null) PlayerNameTag.FaceCamera(nameTag.transform);
    }
}

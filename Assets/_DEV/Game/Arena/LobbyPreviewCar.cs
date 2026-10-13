using TMPro;
using UnityEngine;

// Local stand-in for your car before a match is found (no networking): the robot art in seat 0
// with your name over it. Hidden once you enter the lobby and the real networked cars appear.
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

    // Seats are at spawn height (real cars drop onto their suspension); this one has no physics.
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

        if (show && nameTag != null) CameraFacing.FaceUpright(nameTag.transform);
    }
}

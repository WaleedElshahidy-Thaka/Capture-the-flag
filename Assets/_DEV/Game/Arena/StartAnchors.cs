using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The arena's six start anchors (FD-06), authored in the scene by the level designer.
// One child Transform per seat, in seat order: the first child is seat 0, the next seat 1, and so on.
// Each anchor's position is the floor point under the car, and its blue arrow (forward) is the way
// the car faces. LobbyLayout reads these; with no StartAnchors in the scene it falls back to the
// old placeholder row.
//
// Every peer loads the same scene, so every peer agrees on the seats without any networking.
public class StartAnchors : MonoBehaviour
{
    [Tooltip("Optional. Where the lobby camera sits for the 'everyone in their seat' shot. " +
             "Leave empty to frame the anchors automatically.")]
    [SerializeField] private Transform lobbyCamera;

    [Tooltip("Cars spawn this high above the anchor and drop onto the floor.")]
    [SerializeField, Min(0f)] private float spawnHeight = 1f;

    [Header("Gizmos")]
    [Tooltip("Rough car footprint, for spotting anchors that overlap or clip a wall.")]
    [SerializeField] private Vector3 carSize = new Vector3(1.6f, 1f, 2.4f);

    public static StartAnchors Instance { get; private set; }

    public int Count => transform.childCount;
    public Transform LobbyCamera => lobbyCamera;
    public float SpawnHeight => spawnHeight;

    public Transform Anchor(int slot) =>
        slot >= 0 && slot < transform.childCount ? transform.GetChild(slot) : null;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnValidate()
    {
        if (transform.childCount != MatchmakingConfig.MaxPlayers)
            Debug.LogWarning($"[StartAnchors] {transform.childCount} anchors, but MaxPlayers is " +
                             $"{MatchmakingConfig.MaxPlayers}. Missing seats fall back to the placeholder row.", this);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < transform.childCount; i++) centre += transform.GetChild(i).position;
        if (transform.childCount > 0) centre /= transform.childCount;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform anchor = transform.GetChild(i);
            Vector3 carCentre = anchor.position + Vector3.up * (carSize.y * 0.5f);

            Gizmos.color = OverlapsAnother(i) ? Color.red : new Color(0.2f, 1f, 0.4f);
            Gizmos.matrix = Matrix4x4.TRS(carCentre, anchor.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, carSize);
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(carCentre, anchor.forward * carSize.z);

            // Seat number and distance to the anchors' centre - FD-06 wants nobody advantaged,
            // so these distances should be roughly equal.
            Handles.Label(carCentre + Vector3.up * 1.2f,
                $"Seat {i}\n{Vector3.Distance(Flat(anchor.position), Flat(centre)):0.0} m from centre");
        }

        if (lobbyCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lobbyCamera.position, 0.4f);
            Gizmos.DrawRay(lobbyCamera.position, lobbyCamera.forward * 3f);
        }
    }

    bool OverlapsAnother(int index)
    {
        float minGap = Mathf.Max(carSize.x, carSize.z);
        Vector3 p = transform.GetChild(index).position;
        for (int i = 0; i < transform.childCount; i++)
        {
            if (i == index) continue;
            if (Vector3.Distance(p, transform.GetChild(i).position) < minGap) return true;
        }
        return false;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
#endif
}

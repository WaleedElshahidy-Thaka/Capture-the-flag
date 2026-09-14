using UnityEngine;

// Serialized arena dimensions, so the placeholder arena can be resized in the inspector without
// touching code - and so GameSceneSetup regenerating the scene keeps whatever size was set
// rather than resetting to a hardcoded one.
//
// Placeholder until the art team's arena arrives. When it does, this component moves onto the
// real arena and keeps serving the same role: the one place that answers "how big is the space",
// for anything that needs to know.
//
// Glowtag FD-06's sizing requirement is "crossable in roughly 4 to 6 seconds at Arena top
// speed", which is a relationship between this value and the handling profile's top_speed rather
// than a fixed number - hence CrossingSeconds below, which reports it rather than enforcing it.
public class ArenaBounds : MonoBehaviour
{
    [Tooltip("Distance from arena centre to each wall, in metres. The playable square is twice this on each side.")]
    [SerializeField] float halfExtent = 45f;

    [Tooltip("Wall height in metres.")]
    [SerializeField] float wallHeight = 3f;

    [Tooltip("Wall thickness in metres.")]
    [SerializeField] float wallThickness = 1f;

    public float HalfExtent => halfExtent;
    public float WallHeight => wallHeight;
    public float WallThickness => wallThickness;

    public float FloorSize => halfExtent * 2f;

    // FD-06 wants 4-6 seconds corner to corner at Arena top speed. Reported rather than
    // enforced - the handling profile owns top speed, and the two get tuned against each other.
    public float CrossingSeconds(float topSpeed) => topSpeed > 0f ? FloorSize / topSpeed : 0f;
}

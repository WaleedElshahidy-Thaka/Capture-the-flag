using UnityEngine;

// Arena dimensions, editable in the inspector. GameSceneSetup builds the placeholder floor and
// walls from these. When the real arena arrives this component moves onto it.
public class ArenaBounds : MonoBehaviour
{
    [Tooltip("Distance from arena centre to each wall, in metres.")]
    [SerializeField] float halfExtent = 45f;

    [Tooltip("Wall height in metres.")]
    [SerializeField] float wallHeight = 3f;

    [Tooltip("Wall thickness in metres.")]
    [SerializeField] float wallThickness = 1f;

    public float HalfExtent => halfExtent;
    public float WallHeight => wallHeight;
    public float WallThickness => wallThickness;

    public float FloorSize => halfExtent * 2f;

    // Design target (FD-06): crossable in about 4-6 s at top speed. Reported, not enforced.
    public float CrossingSeconds(float topSpeed) => topSpeed > 0f ? FloorSize / topSpeed : 0f;
}

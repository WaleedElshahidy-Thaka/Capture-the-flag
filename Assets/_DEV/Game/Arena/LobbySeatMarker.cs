using UnityEngine;

// Scene-view gizmo for one lobby seat. Visual only: seat positions come from LobbyLayout and
// nothing reads these transforms at runtime. Rebuilt by GameSceneSetup.
public class LobbySeatMarker : MonoBehaviour
{
    [SerializeField] int slot;

    public void SetSlot(int value) => slot = value;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.85f, 0.4f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 1.4f));
        Gizmos.DrawLine(new Vector3(0f, 0.35f, 0.7f), new Vector3(0f, 0.35f, 1.3f)); // nose direction
        Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Seat {slot}");
#endif
    }
}

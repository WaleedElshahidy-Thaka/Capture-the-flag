using UnityEditor;
using UnityEngine;

// Game > Create Start Anchors: adds a StartAnchors object with one anchor per seat, placed exactly
// where the placeholder row puts the cars today - so nothing moves until the designer drags them.
static class StartAnchorsBuilder
{
    [MenuItem("Game/Create Start Anchors")]
    static void Create()
    {
        var existing = Object.FindFirstObjectByType<StartAnchors>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.LogWarning("[StartAnchorsBuilder] The scene already has StartAnchors - selected it.");
            return;
        }

        // Read the placeholder row before StartAnchors exists, so the new anchors copy it exactly.
        int count = LobbyLayout.SlotCount;
        var positions = new Vector3[count];
        var rotations = new Quaternion[count];
        for (int slot = 0; slot < count; slot++)
        {
            positions[slot] = LobbyLayout.SlotPosition(slot) - Vector3.up * LobbyLayout.SpawnHeight;
            rotations[slot] = LobbyLayout.SlotRotation(slot);
        }

        var root = new GameObject("StartAnchors", typeof(StartAnchors));
        Undo.RegisterCreatedObjectUndo(root, "Create Start Anchors");

        for (int slot = 0; slot < count; slot++)
        {
            var anchor = new GameObject($"Seat_{slot}");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.SetPositionAndRotation(positions[slot], rotations[slot]);
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[StartAnchorsBuilder] Created {count} start anchors at the placeholder row. " +
                  "Move them onto the arena floor; keep the children in seat order.");
    }
}

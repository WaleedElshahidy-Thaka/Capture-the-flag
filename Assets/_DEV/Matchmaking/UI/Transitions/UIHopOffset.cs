using UnityEngine;
using UnityEngine.UI;

// Shifts a UI graphic's drawing up by Offset pixels without moving its RectTransform, so layout groups
// leave it alone. Added and driven by UIHopWave - no need to add it by hand.
[RequireComponent(typeof(Graphic))]
public class UIHopOffset : BaseMeshEffect
{
    float offset;

    // Whether this graphic should hop at all. LobbyUI turns it off for icons of players who've been found.
    public bool Hop { get; set; } = true;

    public void SetOffset(float value)
    {
        if (Mathf.Approximately(value, offset)) return;
        offset = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Mathf.Approximately(offset, 0f)) return;

        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            vertex.position.y += offset;
            vh.SetUIVertex(vertex, i);
        }
    }
}

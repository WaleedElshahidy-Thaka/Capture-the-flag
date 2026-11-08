using UnityEngine;
using UnityEngine.UI;

// Tints parts of the car with its seat colour from SeatPalette - the same colour its row has in the
// lobby and on the scoreboard. Reads the networked seat, so it needs no networking of its own.
//
// Works with all three kinds of car visuals:
//   Graphics         - UI Images on a world-space canvas (the name tag frame, the pointer, the tick)
//   Sprite Renderers - sprites placed directly in the scene
//   Renderers        - 3D meshes (the ring under the car); tinted with a property block, so cars
//                      keep sharing one material
// The original alpha of each image/sprite is kept, so semi-transparent art stays semi-transparent.
// Tint multiplies, so draw the art WHITE (or light grey) to get the exact seat colour.
public class SeatColorTint : MonoBehaviour
{
    [SerializeField] PlayerMatchState matchState;

    [Tooltip("UI Images on the car's world-space canvas: name tag frame, pointer, tick.")]
    [SerializeField] Graphic[] graphics;
    [Tooltip("Sprite Renderers (if the pointer or frame is a sprite rather than a UI Image).")]
    [SerializeField] SpriteRenderer[] sprites;
    [Tooltip("3D renderers, e.g. the ring under the car.")]
    [SerializeField] Renderer[] renderers;

    [Header("3D renderers")]
    [Tooltip("Also drive the emission colour, for glowing parts. The material needs Emission enabled.")]
    [SerializeField] bool tintEmission = true;
    [SerializeField, Min(0f)] float emissionIntensity = 2f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // URP
    static readonly int LegacyColor = Shader.PropertyToID("_Color");     // built-in
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock block;
    int appliedSlot = -1;

    void Reset() => matchState = GetComponentInParent<PlayerMatchState>();

    void LateUpdate()
    {
        if (matchState == null || !matchState.HasState) return;
        if (matchState.Slot == appliedSlot) return; // only when the seat is first known (or changes)

        appliedSlot = matchState.Slot;
        Apply(SeatPalette.Get(appliedSlot));
    }

    void Apply(Color color)
    {
        foreach (var g in graphics)
            if (g != null) g.color = WithAlpha(color, g.color.a);

        foreach (var s in sprites)
            if (s != null) s.color = WithAlpha(color, s.color.a);

        block ??= new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            if (tintEmission) block.SetColor(EmissionColor, color * emissionIntensity);
            r.SetPropertyBlock(block);
        }
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
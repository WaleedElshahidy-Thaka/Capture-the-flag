using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One of the six lobby stand-in robots - one per seat, all local (no networking). Replaces the single
// LobbyPreviewCar so the lobby always looks full, as in the mockup:
//
//   Ring / glow  - tinted with this seat's colour from SeatPalette (blue, yellow, pink, green,
//                  purple, orange), the same colour the seat has everywhere else
//   Name         - YOUR seat shows your name; a seat a player has joined shows their name;
//                  every other seat shows "BOT" until someone takes it
//   Hand-over    - once that player's real networked car is visible in the lobby (you're both in
//                  the lobby), this stand-in hides and the real car takes its place, with the same
//                  name, ring colour and seat. When the match starts, all stand-ins hide (bots fill
//                  the empty seats then).
//
// It never moves the car or touches the camera: place each robot yourself.
public class LobbySeatPreview : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flow;
    [Tooltip("Seat 0..5. Sets the ring colour and which player this stand-in represents.")]
    [SerializeField, Range(0, 5)] int seat;

    [Header("Parts")]
    [SerializeField] GameObject visual;
    [SerializeField] GameObject nameTag;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] string botName = "BOT";

    [Header("Seat colour (the ring, frames, pointer)")]
    [SerializeField] Renderer[] tintRenderers;
    [SerializeField] Graphic[] tintGraphics;
    [SerializeField] SpriteRenderer[] tintSprites;
    [SerializeField] bool tintEmission = true;
    [SerializeField, Min(0f)] float emissionIntensity = 2f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    bool shown = true;
    string shownName;

    void Start()
    {
        if (flow == null) flow = FindFirstObjectByType<MatchmakingFlowController>();
        ApplySeatColor(SeatPalette.Get(seat));
    }

    void LateUpdate()
    {
        PlayerMatchState occupant = Occupant();
        bool show = ShouldShow(occupant);

        if (show != shown)
        {
            shown = show;
            if (visual != null) visual.SetActive(show);
            if (nameTag != null) nameTag.SetActive(show);
        }

        if (!show) return;

        string name = occupant != null ? NameOf(occupant)
                    : seat == LocalSeat() ? PlayerIdentity.LocalDisplayName
                    : botName;
        if (nameLabel != null && name != shownName)
        {
            nameLabel.text = name;
            shownName = name;
        }

        if (nameTag != null) CameraFacing.FaceUpright(nameTag.transform);
    }

    // ─── Who sits here ───────────────────────────────────────────────────

    // The player whose car holds this seat in the current session (searching), if any.
    PlayerMatchState Occupant()
    {
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car != null && car.HasState && car.Slot == seat && !car.IsBot && car.IsSearching) return car;
        }
        return null;
    }

    // Your seat: your car's seat once you have one, seat 0 before (offline / connecting).
    static int LocalSeat()
    {
        var local = PlayerRoster.Local;
        return local != null && local.HasState ? local.Slot : 0;
    }

    bool ShouldShow(PlayerMatchState occupant)
    {
        // Lobby only: the match (and its bots) take over when it starts.
        if (flow != null && !flow.Phase.IsPreMatch()) return false;
        var session = MatchmakingSessionState.Local;
        if (session != null && session.MatchStarting) return false;

        // The real car is visible here (same rule as PlayerLobbyVisibility): step aside for it.
        var own = PlayerRoster.Local;
        bool realCarVisible = occupant != null && own != null && own.InLobby && occupant.InLobby;
        return !realCarVisible;
    }

    static string NameOf(PlayerMatchState car)
    {
        string name = car.DisplayName.ToString();
        return string.IsNullOrEmpty(name) ? "PLAYER" : name;
    }

    // ─── Looks ───────────────────────────────────────────────────────────

    void ApplySeatColor(Color color)
    {
        if (tintGraphics != null)
            foreach (var g in tintGraphics) if (g != null) g.color = WithAlpha(color, g.color.a);
        if (tintSprites != null)
            foreach (var s in tintSprites) if (s != null) s.color = WithAlpha(color, s.color.a);

        if (tintRenderers == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var r in tintRenderers)
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

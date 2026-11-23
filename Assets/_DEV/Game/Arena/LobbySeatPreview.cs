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

    [Header("Ready status (on the name tag)")]
    [Tooltip("The icon on the name tag: a tick when this player has voted READY, a spinner while not.")]
    [SerializeField] Image statusIcon;
    [SerializeField] Sprite readySprite;    // the tick
    [SerializeField] Sprite waitingSprite;  // the spinner ring
    [SerializeField] float spinnerDegreesPerSecond = 270f;
    [Tooltip("Tint the icon with the seat colour (like the lobby list). Off = keep the sprite's own colour.")]
    [SerializeField] bool tintStatusIcon = true;
    [Tooltip("Hide this stand-in when that player's real car appears in the lobby. Off (new flow): real cars " +
             "wait at their match start spots, so the stand-ins stay up for the whole lobby.")]
    [SerializeField] bool handOverToRealCars = false;
    [Tooltip("Off (default): an empty seat shows nothing - only you and the players who have joined the lobby " +
             "appear, each popping in as they join. On: empty seats show a robot named BOT.")]
    [SerializeField] bool showEmptySeatsAsBots = false;
    [Tooltip("How long a robot takes to pop in when its player joins (seconds). 0 = appears at once.")]
    [SerializeField, Min(0f)] float popInSeconds = 0.3f;

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
            if (show) shownAt = Time.unscaledTime; // pop in
        }

        if (!show) return;
        PopIn();

        string name = occupant != null ? NameOf(occupant)
                    : seat == LocalSeat() ? PlayerIdentity.LocalDisplayName
                    : botName;
        if (nameLabel != null && name != shownName)
        {
            nameLabel.text = name;
            shownName = name;
        }

        RefreshStatus(occupant);

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
    // Your seat once the session has given you one; before that (not searching yet, or connecting)
    // your robot stands in seat 0. Seats are handed out from 0 in join order, so when you start your
    // own lobby you stay in seat 0 - no jump. Joining someone else's lobby moves you to the seat you get.
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

        // Who's actually here: you, or a player who has finished joining the lobby (same rule as the
        // lobby list). An empty seat stays empty unless Show Empty Seats As Bots is on.
        var me = PlayerRoster.Local;
        bool mySeat = seat == LocalSeat();
        bool joined = occupant != null && (occupant == me || occupant.InLobby);
        if (!showEmptySeatsAsBots && !mySeat && !joined) return false;

        if (!handOverToRealCars) return true;

        // The real car is visible here (same rule as PlayerLobbyVisibility): step aside for it.
        var own = PlayerRoster.Local;
        bool realCarVisible = occupant != null && own != null && own.InLobby && occupant.InLobby;
        return !realCarVisible;
    }

    // Grows from nothing to full size with a little overshoot when the robot appears.
    float shownAt = -999f;
    Vector3 visualScale = Vector3.zero;

    void PopIn()
    {
        if (visual == null || popInSeconds <= 0f) return;
        if (visualScale == Vector3.zero) visualScale = visual.transform.localScale;

        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / popInSeconds);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float k = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); // ease out, small bounce
        visual.transform.localScale = visualScale * Mathf.Max(0f, k);
    }

    // Ready -> tick. Not ready -> spinning ring. No player in the session yet (before searching) -> no icon.
    // The same READY vote the lobby list shows, read from the player's replicated state.
    void RefreshStatus(PlayerMatchState occupant)
    {
        if (statusIcon == null) return;

        bool inSession = occupant != null;
        if (statusIcon.gameObject.activeSelf != inSession) statusIcon.gameObject.SetActive(inSession);
        if (!inSession) return;

        bool ready = occupant.IsReady;
        Sprite sprite = ready ? readySprite : waitingSprite;
        if (sprite != null && statusIcon.sprite != sprite) statusIcon.sprite = sprite;
        if (tintStatusIcon) statusIcon.color = WithAlpha(SeatPalette.Get(seat), statusIcon.color.a);

        if (ready) statusIcon.rectTransform.localRotation = Quaternion.identity;
        else statusIcon.rectTransform.Rotate(0f, 0f, -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
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
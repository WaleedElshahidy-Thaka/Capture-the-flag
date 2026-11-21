using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

// Markers for every other car - players and bots - so you always know where everyone is (FD-02:
// off-screen Crown arrow, rear-threat markers while holding).
//
//   On screen  - a marker floats over the car, drawn over everything, so it shows through walls
//   Off screen - the marker slides to the screen edge with an arrow pointing at the car,
//                including cars behind you
//   Crown      - the holder's marker is bigger, wears the crown badge and shows its distance
//
// Your own car gets no marker. Colours come from SeatPalette, so a marker matches the car's ring and
// its scoreboard row. Visible only while a match is running.
// Put it on a full-screen RectTransform under InGameUI (stretch to all four corners).
[RequireComponent(typeof(RectTransform))]
public class PlayerIndicatorsUI : MonoBehaviour
{
    [SerializeField] PlayerIndicatorView markerPrefab;
    [Tooltip("Leave empty to use Camera.main.")]
    [SerializeField] Camera worldCamera;

    [Header("Placement")]
    [Tooltip("Marker height above the car, in metres - above the name tag and the Crown.")]
    [SerializeField] float heightAboveCar = 3f;
    [Tooltip("How far from the screen edge edge-markers sit, in pixels.")]
    [SerializeField] float edgePadding = 60f;

    [Header("What to show")]
    [Tooltip("Off: on-screen cars rely on their own pointer and only off-screen cars get an edge marker.")]
    [SerializeField] bool showOnScreenMarkers = true;
    [Tooltip("Show the distance on the Crown holder's marker.")]
    [SerializeField] bool showCrownHolderDistance = true;
    [Tooltip("Show the distance on everyone's marker.")]
    [SerializeField] bool showEveryoneDistance = false;

    [Header("Free Crown (before anyone holds it)")]
    [Tooltip("Show a marker on the Crown while nobody holds it - while it's spawning (telegraph) and waiting " +
             "to be picked up. Hidden as soon as someone takes it (the holder's marker wears the crown then).")]
    [SerializeField] bool showFreeCrown = true;
    [Tooltip("Marker for the free Crown. Empty = the player Marker Prefab (its crown badge is switched on).")]
    [SerializeField] PlayerIndicatorView crownMarkerPrefab;
    [SerializeField] Color crownMarkerColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Marker height above the Crown, in metres.")]
    [SerializeField] float heightAboveCrown = 1.5f;
    [SerializeField] bool showFreeCrownDistance = true;
    [Tooltip("While the Crown is still spawning (the telegraph), the marker pulses.")]
    [SerializeField, Min(0f)] float telegraphPulseSpeed = 6f;

    RectTransform area;
    Canvas canvas;
    readonly Dictionary<NetworkId, PlayerIndicatorView> markers = new Dictionary<NetworkId, PlayerIndicatorView>();
    readonly HashSet<NetworkId> seen = new HashSet<NetworkId>();
    readonly List<NetworkId> stale = new List<NetworkId>();

    void Awake()
    {
        area = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
    }

    void LateUpdate()
    {
        var timer = MatchTimerState.Instance;
        bool running = timer != null && timer.Object != null && timer.Object.IsValid &&
                       (timer.InCountdown || timer.IsPlaying) && !timer.Finished;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (!running || cam == null || markerPrefab == null)
        {
            HideAll();
            return;
        }

        var local = PlayerRoster.Local;
        var crown = CrownMatchState.Instance;
        Vector3 me = local != null ? local.transform.position : cam.transform.position;

        seen.Clear();
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car == null || !car.HasState || car == local) continue;

            NetworkId id = car.Object.Id;
            seen.Add(id);

            if (!markers.TryGetValue(id, out var marker))
            {
                marker = Instantiate(markerPrefab, area);
                markers[id] = marker;
            }

            bool holder = crown != null && crown.IsHolder(car.Object);
            Place(marker, car, cam, holder, Vector3.Distance(me, car.transform.position));
        }

        PlaceFreeCrown(cam, crown, me);

        // Cars that left: drop their markers.
        stale.Clear();
        foreach (var pair in markers) if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
        foreach (var id in stale)
        {
            if (markers[id] != null) Destroy(markers[id].gameObject);
            markers.Remove(id);
        }
    }

    void Place(PlayerIndicatorView marker, PlayerMatchState car, Camera cam, bool holder, float distance)
    {
        bool showDistance = showEveryoneDistance || (holder && showCrownHolderDistance);
        PlaceAt(marker, VisualPosition(car) + Vector3.up * heightAboveCar, cam,
                SeatPalette.Get(car.Slot), holder, distance, showDistance);
    }

    // ─── Free Crown ───────────────────────────────────────────────────────

    PlayerIndicatorView crownMarker;
    CanvasGroup crownMarkerGroup;
    DataCrown dataCrown;
    float nextCrownLookup;

    // The Crown before anyone has it: spawning (telegraph) or lying free. Same edge arrows as the cars,
    // gold, with its distance - so everyone can race for it from anywhere in the arena.
    void PlaceFreeCrown(Camera cam, CrownMatchState crown, Vector3 me)
    {
        if (Time.unscaledTime >= nextCrownLookup || dataCrown == null)
        {
            nextCrownLookup = Time.unscaledTime + 0.5f;
            dataCrown = FindFirstObjectByType<DataCrown>();
        }

        bool free = showFreeCrown && dataCrown != null && dataCrown.Object != null && dataCrown.Object.IsValid &&
                    (crown == null || !crown.HasHolder) &&
                    (dataCrown.IsTelegraphing || dataCrown.IsPickupAvailable);

        if (!free)
        {
            if (crownMarker != null) SetVisible(crownMarker, false);
            return;
        }

        if (crownMarker == null)
        {
            var prefab = crownMarkerPrefab != null ? crownMarkerPrefab : markerPrefab;
            if (prefab == null) return;
            crownMarker = Instantiate(prefab, area);
            crownMarker.name = "FreeCrownMarker";
            crownMarkerGroup = crownMarker.GetComponent<CanvasGroup>();
            if (crownMarkerGroup == null) crownMarkerGroup = crownMarker.gameObject.AddComponent<CanvasGroup>();
            crownMarkerGroup.blocksRaycasts = false;
        }

        Vector3 position = dataCrown.transform.position;
        PlaceAt(crownMarker, position + Vector3.up * heightAboveCrown, cam, crownMarkerColor,
                true, Vector3.Distance(me, position), showFreeCrownDistance);

        // Spawning: pulse. Ready to grab: solid.
        crownMarkerGroup.alpha = dataCrown.IsTelegraphing
            ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * telegraphPulseSpeed))
            : 1f;
    }

    void PlaceAt(PlayerIndicatorView marker, Vector3 world, Camera cam, Color color, bool crownBadge,
                 float distance, bool showDistance)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);

        // Behind the camera, the projection comes out mirrored: flip it so the arrow points the
        // right way, and always treat it as off screen.
        bool behind = screen.z < 0f;
        if (behind) screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);

        bool onScreen = !behind &&
                        screen.x >= edgePadding && screen.x <= Screen.width - edgePadding &&
                        screen.y >= edgePadding && screen.y <= Screen.height - edgePadding;

        if (onScreen && !showOnScreenMarkers)
        {
            SetVisible(marker, false);
            return;
        }
        SetVisible(marker, true);

        float angle = 0f;
        if (!onScreen)
        {
            // Push the point out from the screen centre until it meets the padded edge.
            Vector2 centre = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 dir = (Vector2)screen - centre;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down; // dead centre behind you: point down
            Vector2 half = centre - Vector2.one * edgePadding;
            float scale = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                                    half.y / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            screen = centre + dir * scale;
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, uiCam, out Vector2 local);

        // Local point is relative to the area's pivot; markers are anchored the same way.
        marker.Rect.anchorMin = marker.Rect.anchorMax = area.pivot;
        marker.Show(local, !onScreen, angle, color, crownBadge, distance, showDistance);
    }

    // The render-interpolated child, so markers glide smoothly between network ticks.
    static Vector3 VisualPosition(PlayerMatchState car)
    {
        if (car.TryGetComponent(out NetworkRigidbody nrb) && nrb.InterpolationTarget != null)
            return nrb.InterpolationTarget.position;
        return car.transform.position;
    }

    void HideAll()
    {
        foreach (var pair in markers) if (pair.Value != null) SetVisible(pair.Value, false);
        if (crownMarker != null) SetVisible(crownMarker, false);
    }

    static void SetVisible(PlayerIndicatorView marker, bool visible)
    {
        if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
    }
}
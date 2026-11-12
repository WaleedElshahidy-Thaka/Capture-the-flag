using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;

// The event feed (bottom left) and the centre banner ("CROWN CAPTURED!") from the in-game mockup.
// FD-04 event feed: "worth a lot at 5-6 players where much happens off-camera".
//
// Read-only: every peer watches the replicated match state and notices the changes itself - the Crown
// appearing, changing hands, someone taking the lead - so nothing extra is sent over the network.
//
//   Feed lines      "The Crown has spawned!" / "<name> has captured / stolen / recaptured the crown" /
//                   "<name> took the lead!" - names in their seat colour; old lines fade out
//   Banner (yours)  "CROWN CAPTURED!" when you take it, "CROWN STOLEN!" when you lose it,
//                   "YOU TOOK THE LEAD!" - only about you, so it never shouts about other people
public class MatchEventsUI : MonoBehaviour
{
    [Header("Event feed")]
    [Tooltip("Has a VerticalLayoutGroup; newest line goes at the bottom.")]
    [SerializeField] Transform feedContainer;
    [Tooltip("A single TextMeshPro line with Rich Text on.")]
    [SerializeField] TMP_Text feedLinePrefab;
    [SerializeField, Min(1)] int maxLines = 6;
    [SerializeField, Min(0.5f)] float lineSeconds = 6f;
    [Tooltip("The last part of a line's life is a fade-out.")]
    [SerializeField, Min(0f)] float lineFadeSeconds = 2f;

    [Header("Feed text ({0} = name)")]
    [SerializeField] string crownSpawned = "The Crown has spawned!";
    [SerializeField] string captured = "{0} has captured the crown";
    [SerializeField] string stolen = "{0} has stolen the crown";
    [SerializeField] string recaptured = "{0} has recaptured the crown";
    [SerializeField] string tookLead = "{0} took the lead!";
    [Tooltip("Also list Oil Slick drops and Repulse Pulses.")]
    [SerializeField] bool showActivations = true;
    [SerializeField] string droppedOil = "{0} dropped an Oil Slick";
    [SerializeField] string firedRepulse = "{0} fired a Repulse Pulse";

    [Header("Centre banner")]
    [Tooltip("The banner panel. Give it a UITransition to slide or pop in.")]
    [SerializeField] GameObject banner;
    [SerializeField] TMP_Text bannerText;
    [SerializeField, Min(0.2f)] float bannerSeconds = 1.6f;
    [SerializeField] string youCaptured = "CROWN CAPTURED!";
    [SerializeField] string youLost = "CROWN STOLEN!";
    [SerializeField] string youLead = "YOU TOOK THE LEAD!";

    struct Line { public TMP_Text text; public float born; }
    readonly List<Line> lines = new List<Line>();

    bool running;
    bool crownWasPresent;
    float nextCrownCheck;
    NetworkId lastHolder;
    NetworkId holderBefore; // who held it before lastHolder - for "recaptured"
    NetworkId lastLeader;
    float bannerHideAt = -1f;

    void OnEnable()
    {
        OilPatch.Dropped += OnOilDropped;
        RepulsePulse.Fired += OnRepulseFired;
        SetBanner(false, animate: false);
    }

    void OnDisable()
    {
        OilPatch.Dropped -= OnOilDropped;
        RepulsePulse.Fired -= OnRepulseFired;
    }

    void Update()
    {
        var timer = MatchTimerState.Instance;
        bool nowRunning = timer != null && timer.Object != null && timer.Object.IsValid &&
                          (timer.InCountdown || timer.IsPlaying) && !timer.Finished;

        if (!nowRunning)
        {
            if (running) ResetForNextMatch();
            running = false;
            return;
        }
        running = true;

        WatchCrownSpawn();
        WatchHolder();
        WatchLeader();
        AgeLines();

        if (bannerHideAt > 0f && Time.unscaledTime >= bannerHideAt)
        {
            bannerHideAt = -1f;
            SetBanner(false, animate: true);
        }
    }

    // ─── Watching the match ──────────────────────────────────────────────

    void WatchCrownSpawn()
    {
        if (Time.unscaledTime < nextCrownCheck) return;
        nextCrownCheck = Time.unscaledTime + 0.25f;

        bool present = FindFirstObjectByType<DataCrown>() != null;
        if (present && !crownWasPresent) Push(crownSpawned);
        crownWasPresent = present;
    }

    void WatchHolder()
    {
        var crown = CrownMatchState.Instance;
        NetworkId holder = crown != null && crown.HasHolder ? crown.HolderId : default;
        if (holder == lastHolder) return;

        var local = PlayerRoster.Local;
        PlayerMatchState newHolder = FindCar(holder);

        if (newHolder != null)
        {
            string format = lastHolder == default ? captured
                          : holder == holderBefore ? recaptured
                          : stolen;
            Push(format, newHolder);

            if (newHolder == local) ShowBanner(youCaptured);
        }

        // You had it and someone else took it.
        if (local != null && lastHolder == local.Object.Id && newHolder != null && newHolder != local)
            ShowBanner(youLost);

        holderBefore = lastHolder;
        lastHolder = holder;
    }

    // Leader = strictly the highest score. A tie isn't a lead, so the feed doesn't flicker.
    void WatchLeader()
    {
        PlayerMatchState best = null;
        int bestScore = 0, second = 0;
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car == null || !car.HasState) continue;
            if (car.Score > bestScore) { second = bestScore; bestScore = car.Score; best = car; }
            else if (car.Score > second) second = car.Score;
        }

        if (best == null || bestScore <= second) return;
        NetworkId id = best.Object.Id;
        if (id == lastLeader) return;
        lastLeader = id;

        Push(tookLead, best);
        if (best == PlayerRoster.Local) ShowBanner(youLead);
    }

    void OnOilDropped(OilPatch patch)
    {
        if (!running || !showActivations || patch == null) return;
        var car = FindCar(patch.DropperId);
        if (car != null) Push(droppedOil, car);
    }

    void OnRepulseFired(RepulsePulse pulse)
    {
        if (!running || !showActivations || pulse == null) return;
        if (pulse.TryGetComponent(out PlayerMatchState car)) Push(firedRepulse, car);
    }

    // ─── Feed ────────────────────────────────────────────────────────────

    void Push(string format, PlayerMatchState car)
    {
        string name = car.DisplayName.ToString();
        if (string.IsNullOrEmpty(name)) name = "Player";
        string hex = ColorUtility.ToHtmlStringRGB(SeatPalette.Get(car.Slot));
        Push((format ?? "{0}").Replace("{0}", $"<color=#{hex}>{name}</color>"));
    }

    void Push(string message)
    {
        if (feedContainer == null || feedLinePrefab == null || string.IsNullOrEmpty(message)) return;

        var text = Instantiate(feedLinePrefab, feedContainer);
        text.text = message;
        text.transform.SetAsLastSibling(); // newest at the bottom
        lines.Add(new Line { text = text, born = Time.unscaledTime });

        while (lines.Count > maxLines) RemoveLine(0);
    }

    void AgeLines()
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.text == null) { lines.RemoveAt(i); continue; }

            float age = Time.unscaledTime - line.born;
            if (age >= lineSeconds) { RemoveLine(i); continue; }

            float fadeStart = lineSeconds - lineFadeSeconds;
            line.text.alpha = age <= fadeStart ? 1f : 1f - (age - fadeStart) / Mathf.Max(0.01f, lineFadeSeconds);
        }
    }

    void RemoveLine(int index)
    {
        if (lines[index].text != null) Destroy(lines[index].text.gameObject);
        lines.RemoveAt(index);
    }

    // ─── Banner ──────────────────────────────────────────────────────────

    void ShowBanner(string message)
    {
        if (banner == null || string.IsNullOrEmpty(message)) return;
        if (bannerText != null) bannerText.text = message;

        bool alreadyShown = banner.activeSelf && bannerHideAt > 0f;
        SetBanner(true, animate: true);
        if (alreadyShown && banner.TryGetComponent(out UITransition t)) t.Punch(); // a new message on top
        bannerHideAt = Time.unscaledTime + bannerSeconds;
    }

    void SetBanner(bool show, bool animate)
    {
        if (banner == null) return;
        if (banner.TryGetComponent(out UITransition transition))
        {
            if (!animate) transition.SetVisibleImmediate(show);
            else if (show) transition.Show();
            else transition.Hide();
            return;
        }
        if (banner.activeSelf != show) banner.SetActive(show);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    void ResetForNextMatch()
    {
        for (int i = lines.Count - 1; i >= 0; i--) RemoveLine(i);
        crownWasPresent = false;
        lastHolder = holderBefore = lastLeader = default;
        bannerHideAt = -1f;
        SetBanner(false, animate: false);
    }

    static PlayerMatchState FindCar(NetworkId id)
    {
        if (id == default) return null;
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
            if (cars[i] != null && cars[i].HasState && cars[i].Object.Id == id) return cars[i];
        return null;
    }
}

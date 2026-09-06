using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Per-client, purely local seat placement: this client's own PlayerLobbyState (the one with
// Object.HasInputAuthority) always goes to the center anchor; everyone else fills the
// remaining anchors in a stable order. Every client runs this same logic independently
// against the same replicated PlayerLobbyState.Active roster and reaches a *different*
// answer for "who's centered" - that's the whole point, not a bug (see the plan's
// "no one has authority" table).
public class LobbySeatAssigner : MonoBehaviour
{
    [SerializeField] LobbySeatAnchors anchors;
    [SerializeField] LobbySlotView slotViewPrefab;

    readonly Dictionary<PlayerLobbyState, LobbySlotView> activeViews = new Dictionary<PlayerLobbyState, LobbySlotView>();

    void OnEnable()
    {
        PlayerLobbyState.RosterChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        PlayerLobbyState.RosterChanged -= Rebuild;
    }

    void Update()
    {
        // Roster membership only changes on join/leave (Rebuild handles that), but a ready
        // flag can flip at any time without the roster itself changing, so it's synced here
        // every frame rather than threaded through another event.
        foreach (var kvp in activeViews)
            if (kvp.Value != null) kvp.Value.SetReady(kvp.Key.IsReady);
    }

    void Rebuild()
    {
        foreach (var view in activeViews.Values)
            if (view != null) Destroy(view.gameObject);
        activeViews.Clear();

        var self = PlayerLobbyState.Active.FirstOrDefault(p => p.Object.HasInputAuthority);
        if (self != null)
            Place(self, anchors.CenterAnchor, isSelf: true);

        var others = PlayerLobbyState.Active
            .Where(p => p != self)
            .OrderBy(p => p.Object.InputAuthority.PlayerId)
            .ToList();

        for (int i = 0; i < others.Count && i < anchors.OtherAnchors.Length; i++)
            Place(others[i], anchors.OtherAnchors[i], isSelf: false);
    }

    void Place(PlayerLobbyState state, Transform anchor, bool isSelf)
    {
        if (anchor == null || slotViewPrefab == null) return;

        var view = Instantiate(slotViewPrefab, anchor.position, anchor.rotation);
        view.Bind($"Player {state.Object.InputAuthority.PlayerId}", isSelf);
        view.SetReady(state.IsReady);
        activeViews[state] = view;
    }
}

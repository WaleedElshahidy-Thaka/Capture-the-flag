using System;
using System.Collections.Generic;
using Fusion;

// Registry of every spawned car's PlayerMatchState on this peer, plus the roster queries the
// matchmaking code needs. PlayerMatchState registers itself; everyone else only reads.
// Index-based loops on purpose: some of these run every host tick and must not allocate.
public static class PlayerRoster
{
    static readonly List<PlayerMatchState> active = new List<PlayerMatchState>();

    public static IReadOnlyList<PlayerMatchState> Active => active;

    // This peer's own car (the one we have Input Authority over), or null.
    public static PlayerMatchState Local { get; private set; }

    // Raised when a car spawns, despawns or changes owner.
    public static event Action Changed;

    // Register/Unregister don't raise Changed; the caller does once its own state is settled.
    internal static void Register(PlayerMatchState player) => active.Add(player);

    internal static void Unregister(PlayerMatchState player)
    {
        active.Remove(player);
        if (Local == player) Local = null;
    }

    internal static void SetOwnership(PlayerMatchState player, bool isMine)
    {
        if (isMine) Local = player;
        else if (Local == player) Local = null;
    }

    internal static void NotifyChanged() => Changed?.Invoke();

    public static int CountSearching()
    {
        int count = 0;
        for (int i = 0; i < active.Count; i++)
            if (active[i].IsSearching) count++;
        return count;
    }

    public static int CountInLobby()
    {
        int count = 0;
        for (int i = 0; i < active.Count; i++)
            if (active[i].InLobby) count++;
        return count;
    }

    public static bool AllSearchingReady()
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].IsSearching && !active[i].IsReady) return false;
        return true;
    }

    // The shared lobby timer: the highest search time among players already in the lobby.
    public static float SharedElapsedSeconds()
    {
        float highest = 0f;
        for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].InLobby) continue;
            float elapsed = active[i].SearchElapsedSeconds;
            if (elapsed > highest) highest = elapsed;
        }
        return highest;
    }

    public static PlayerMatchState FindWithInputAuthority()
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].Object.HasInputAuthority) return active[i];
        return null;
    }

    public static PlayerMatchState FindOwnedBy(PlayerRef player)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].Object.InputAuthority == player) return active[i];
        return null;
    }

    // A bot-driven car still stamped with this install token: its player can take it back.
    public static PlayerMatchState FindReclaimable(string ownerToken)
    {
        if (string.IsNullOrEmpty(ownerToken)) return null;
        for (int i = 0; i < active.Count; i++)
            if (active[i].IsBot && active[i].OwnerToken == ownerToken) return active[i];
        return null;
    }

    // True while some car is bot-driven but still waiting for its owner to reconnect.
    public static bool AnyOwnerMissing()
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].IsBot && active[i].OwnerToken.Length > 0) return true;
        return false;
    }
}

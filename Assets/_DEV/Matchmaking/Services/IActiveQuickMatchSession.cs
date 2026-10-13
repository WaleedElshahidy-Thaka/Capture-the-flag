using System;

// The Quick Match session this client is connected to, as the flow controller sees it.
// Implemented by the Fusion backend and by the offline fake, so the flow never touches Fusion.
public interface IActiveQuickMatchSession
{
    int PlayerCount { get; }
    int MaxPlayers { get; }
    bool IsHost { get; }

    // True once our own car is spawned; the searching request waits for it.
    bool IsLocalPlayerSpawned { get; }

    // alreadyElapsedSeconds back-dates the session timer to the Quick Match click.
    bool IsSearching { get; }
    void SetSearching(bool searching, float alreadyElapsedSeconds = 0f);

    // > 0 only during the "Player found! Joining lobby in N" countdown.
    float LobbyJoinSecondsRemaining { get; }
    bool InLobby { get; }

    // Players whose countdown has finished: visible to each other and sharing one timer.
    int LobbyCount { get; }

    // Own timer before the lobby; the highest timer in the lobby once inside.
    float ElapsedSeconds { get; }
    bool BotOptionUnlocked { get; }

    // After a host migration. ResumeSecondsRemaining is negative while still waiting for players.
    bool IsFrozen { get; }
    float ResumeSecondsRemaining { get; }

    bool IsReady { get; }
    void SetReady(bool ready);

    // Solo only: start now, bots filling every other seat.
    void RequestStartWithBots();

    // Decided once by the authority; BotCount never changes afterwards.
    bool MatchStarting { get; }
    int BotCount { get; }

    event Action Changed;

    void Leave();
}

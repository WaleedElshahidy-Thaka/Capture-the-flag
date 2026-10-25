
using System.Collections.Generic;

public static class PowerUpCatchUpCalculator
{
    /// <summary>
    /// Returns true if the participant's score is below
    /// the highest score among active participants.
    /// Bots are included.
    /// </summary>
    public static bool IsTrailing(PlayerMatchState participant)
    {
        if (participant == null)
            return false;

        IReadOnlyList<PlayerMatchState> players =
            PlayerRoster.Active;

        if (players == null || players.Count == 0)
            return false;

        int highestScore = int.MinValue;
        bool foundPlayer = false;

        foreach (PlayerMatchState player in players)
        {
            if (player == null || !player.HasState)
                continue;

            if (player.Score > highestScore)
                highestScore = player.Score;

            foundPlayer = true;
        }

        if (!foundPlayer)
            return false;

        return participant.Score < highestScore;
    }
}

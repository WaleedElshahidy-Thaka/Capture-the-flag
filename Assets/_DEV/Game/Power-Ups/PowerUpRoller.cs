
using Fusion;
using UnityEngine;

/// <summary>
/// Deterministic, authority-side power-up selection.
/// Weights are provisional tuning values, not final FD-04 balance.
/// </summary>
public static class PowerUpRoller
{
    // Provisional base weights. Repulse stays rare.
    private const int OverdriveWeight = 30;
    private const int GrappleWeight = 25;
    private const int DamperWeight = 25;
    private const int OilWeight = 18;
    private const int RepulseWeight = 2;

    // Provisional catch-up adjustment.
    // This boosts helpful items without increasing Repulse's weight.
    private const int TrailingBonus = 10;

    public static PowerUpType Roll(int tick, PlayerRef participant, bool isCrownHolder, bool isTrailing)
    {
        int overdrive = OverdriveWeight;
        int grapple = isCrownHolder ? 0 : GrappleWeight;
        int damper = DamperWeight;
        int oil = OilWeight;
        int repulse = RepulseWeight;

        if (isTrailing)
        {
            overdrive += TrailingBonus;
            grapple += isCrownHolder ? 0 : TrailingBonus;
            damper += TrailingBonus;
            oil += TrailingBonus;

            // FD-04: trailing bias must never increase Repulse.
        }

        int totalWeight =
            overdrive + grapple + damper + oil + repulse;

        if (totalWeight <= 0)
            return PowerUpType.None;

        uint randomValue = Hash(tick, participant.RawEncoded);

        int roll = (int)(randomValue % (uint)totalWeight);

        if ((roll -= overdrive) < 0)
            return PowerUpType.Overdrive;

        if ((roll -= grapple) < 0)
            return PowerUpType.GrappleField;

        if ((roll -= damper) < 0)
            return PowerUpType.DamperField;

        if ((roll -= oil) < 0)
            return PowerUpType.OilSlick;

        return PowerUpType.RepulsePulse;
    }

    private static uint Hash(int tick, int participantId)
    {
        unchecked
        {
            uint hash = (uint)tick;
            hash ^= (uint)participantId + 0x9E3779B9u
                + (hash << 6) + (hash >> 2);

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;

            return hash;
        }
    }
}

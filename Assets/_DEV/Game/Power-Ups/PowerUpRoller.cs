
using Fusion;
using UnityEngine;

/// <summary>
/// Deterministic, authority-side power-up selection.
/// Uses the car NetworkId to distinguish bots that share PlayerRef.None.
/// Weights are provisional tuning values.
/// </summary>
public static class PowerUpRoller
{
    private const int OverdriveWeight = 30;
    private const int GrappleWeight = 25;
    private const int DamperWeight = 25;
    private const int OilWeight = 18;
    private const int RepulseWeight = 2;

    private const int TrailingBonus = 10;

    public static PowerUpType Roll(
        int tick,
        PlayerRef participant,
        NetworkId carId,
        bool isCrownHolder,
        bool isTrailing)
    {
        int overdrive = OverdriveWeight;
        int grapple = isCrownHolder ? 0 : GrappleWeight;
        int damper = isCrownHolder? DamperWeight : 0;
        int oil = OilWeight;
        int repulse = RepulseWeight;

        if (isTrailing)
        {
            overdrive += TrailingBonus;

            if (!isCrownHolder)
                grapple += TrailingBonus;

            damper += TrailingBonus;
            oil += TrailingBonus;
        }

        int totalWeight =
            overdrive + grapple + damper + oil + repulse;

        if (totalWeight <= 0)
            return PowerUpType.None;

        uint randomValue = Hash(
            tick,
            participant.RawEncoded,
            carId.Raw);

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

    private static uint Hash(
        int tick,
        int participantId,
        uint carId)
    {
        unchecked
        {
            uint hash = (uint)tick;

            hash ^= (uint)participantId
                + 0x9E3779B9u
                + (hash << 6)
                + (hash >> 2);

            hash ^= carId
                + 0x9E3779B9u
                + (hash << 6)
                + (hash >> 2);

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;

            return hash;
        }
    }
}

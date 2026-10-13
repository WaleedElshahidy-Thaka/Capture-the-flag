// Drive-model state that carries from one tick to the next. PlayerMovement stores every field
// as [Networked] (so it is replicated and restored on rollback) and the simulation works on a copy.
public struct DriveState
{
    public float YawRate;              // deg/s
    public float BrakeHoldSeconds;     // how long brake has been held at rest (reverse delay)
    public int SpinoutTicksRemaining;
    public int ImmunityTicksRemaining;
    public float SettleHeading;        // world yaw (deg) the spin-out ends facing
    public float SpinDegrees;          // signed rotation the spin-out covers

    public bool IsSpinningOut => SpinoutTicksRemaining > 0;
    public bool HasContactImmunity => ImmunityTicksRemaining > 0;
}

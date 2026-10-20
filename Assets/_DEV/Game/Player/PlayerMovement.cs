using Fusion;
using UnityEngine;

// Network adapter for one car. Owns the replicated drive state, chooses where this tick's input
// comes from (player, bot, or nothing yet) and hands the physics to VehicleDriveModel.
// Runs before RunnerSimulatePhysics (order 0) so the velocity written is integrated this tick.
//
// Layer C (drift & boost) is stepped here once per tick, BEFORE the drive model, so the drive model
// sees this tick's grip / yaw / boost-target modifiers.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] HandlingValues handling = new HandlingValues();
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Probe geometry (baked from the art by GameSceneSetup - don't hand-edit)")]
    [Tooltip("Local wheel positions: front-left, front-right, rear-left, rear-right.")]
    [SerializeField] Vector3[] probeOffsets = new Vector3[4];
    [Tooltip("Measured from the wheel mesh. Also the suspension's rest height.")]
    [SerializeField] float wheelRadius = 0.3f;

    [Header("Tire visuals (cosmetic only)")]
    [SerializeField] Transform visual; // parent of the tires - leaned, never the Rigidbody
    [SerializeField] Transform frontLeftTire;
    [SerializeField] Transform frontRightTire;
    [SerializeField] Transform rearLeftTire;
    [SerializeField] Transform rearRightTire;
    [SerializeField] float maxTireSteerAngle = 30f;

    [Header("Cosmetic body lean (visual only)")]
    [SerializeField] float maxLeanAngle = 12f;
    [SerializeField] float leanSpring = 8f;

    // Drive state, replicated so it is restored on rollback (mirrors DriveState).
    // Declaration order is the network layout - keep it stable (new fields go at the end).
    [Networked] float YawRate { get; set; }
    [Networked] float BrakeHoldSeconds { get; set; }
    [Networked] float SteerInput { get; set; } // so every peer can turn this car's front wheels
    [Networked] int SpinoutTicksRemaining { get; set; }
    [Networked] int ImmunityTicksRemaining { get; set; }
    [Networked] float SettleHeading { get; set; }
    [Networked] float SpinDegrees { get; set; }

    // Drift & boost state (mirrors DriftBoostState). Appended after the drive state.
    [Networked] int DriftPhaseValue { get; set; }          // DriftPhase as int
    [Networked] int DriftPhaseTicks { get; set; }
    [Networked] int DriftDirection { get; set; }
    [Networked] float DriftReserve { get; set; }
    [Networked] NetworkBool DriftHopHeld { get; set; }
    [Networked] int BoostTicksRemaining { get; set; }
    [Networked] float BoostMultiplier { get; set; }
    [Networked] float BoostBaseSpeed { get; set; }
    [Networked] int ChainGraceTicksRemaining { get; set; }
    [Networked] int ChainLinks { get; set; }
    [Networked] int LastBoostStartTick { get; set; }

    public bool IsSpinningOut => SpinoutTicksRemaining > 0;
    public bool HasContactImmunity => ImmunityTicksRemaining > 0;
    public bool IsGrounded => driveModel.IsGrounded;
    public float ForwardSpeed => driveModel.ForwardSpeed;

    // Drift & boost, read by contact code, UI and VFX.
    public DriftPhase CurrentDriftPhase => (DriftPhase)DriftPhaseValue;
    public bool IsSliding => ReadDriftState().IsSliding;
    public bool IsBoosting => ReadDriftState().IsBoosting;
    public bool IsChained => ReadDriftState().IsChained;
    public int DriftStage => DriftBoost.CurrentStage(ReadDriftState(), handling);
    public float DriftReserveValue => DriftReserve;

    // Multiplies the arcade bonus component of a contact when this car is the aggressor (1.6x while
    // Boosting or Chained). The collision code owns the rest of the impulse.
    public float BumpMultiplier => DriftBoost.BumpMultiplier(ReadDriftState(), handling);

    // Cosmetic/audio only: what the last simulated tick did. Never feed back into the simulation.
    public DriftBoostEvents LastDriftEvents { get; private set; }

    Rigidbody body;
    PlayerMatchState matchState;
    VehicleDriveModel driveModel;
    VehicleVisuals visuals;

    // Per-car runtime copy of the handling values. The drive model reads this one, so Layer C can
    // modulate grip, yaw authority and the boost target each tick without touching the serialized profile.
    HandlingValues driveHandling;

    // Set from physics callbacks (wall / car contacts only, not the ground), consumed next tick.
    bool contactSinceLastTick;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        matchState = GetComponent<PlayerMatchState>();
        ChassisPhysicsSetup.Apply(body);
        BuildModels();
    }

    // Lets inspector tweaks during Play Mode take effect immediately.
    void OnValidate()
    {
        if (Application.isPlaying && body != null) BuildModels();
    }

    void BuildModels()
    {
        driveHandling = JsonUtility.FromJson<HandlingValues>(JsonUtility.ToJson(handling));
        driveModel = new VehicleDriveModel(body, driveHandling, probeOffsets, wheelRadius, groundMask);
        visuals = new VehicleVisuals(new VehicleVisuals.Rig
        {
            Body = visual,
            FrontLeft = frontLeftTire,
            FrontRight = frontRightTire,
            RearLeft = rearLeftTire,
            RearRight = rearRightTire,
            WheelRadius = wheelRadius,
            MaxTireSteerAngle = maxTireSteerAngle,
            MaxLeanAngle = maxLeanAngle,
            LeanSpring = leanSpring,
        });
    }

    public override void FixedUpdateNetwork()
    {
        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
        {
            // Held still after a host migration until the shared resume tick.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        if (!TryGetDriveInput(out var input)) return;

        // Before the match starts the car still simulates (settles on its suspension) with neutral input.
        SimulateTick(matchState.CanMove && MatchTimerState.AllowsDriving ? input : default, Runner.DeltaTime);
    }

    // A player's input arrives via GetInput; a car with no player is driven by BotDriver on the host.
    // Clients receive bot-driven cars by replication like any other proxy.
    bool TryGetDriveInput(out PlayerNetInput input)
    {
        if (GetInput(out input)) return true;
        if (!Object.HasStateAuthority || !matchState.IsBot) return false;

        input = BotDriver.Think(this);
        return true;
    }

    // Public so any input source (player or bot) goes through the same simulation.
    public void SimulateTick(PlayerNetInput input, float deltaTime)
    {
        // 1. Layer C first: decide this tick's drift/boost state and the modifiers it implies.
        DriftBoostState drift = ReadDriftState();
        var driftInput = new DriftBoostInput
        {
            Hop = input.Hop,                 // Space (keyboard) / A-Cross (gamepad), HELD
            Boost = input.Boost,             // Left Shift / right shoulder, HELD
            Steer = input.SteerAxis,
            ForwardSpeed = driveModel.ForwardSpeed,
            LateralSpeed = body.transform.InverseTransformDirection(body.linearVelocity).x,
            Grounded = driveModel.IsGrounded,
            Collided = contactSinceLastTick || driveModel.TouchingWall,
            SurfaceChargeFactor = 1f,        // TODO: read the surface's drift-charge channel when surfaces exist
            Tick = Runner.Tick.Raw,
        };
        contactSinceLastTick = false;

        LastDriftEvents = DriftBoost.Tick(ref drift, driftInput, handling);

        if (LastDriftEvents.HopStarted)
            body.AddForce(Vector3.up * handling.HopVelocity, ForceMode.VelocityChange);

        WriteDriftState(drift);

        // 2. Hand the modifiers to the drive model. Grip and yaw are applied automatically while
        //    Sliding (not a button). Boost raises the target speed with no clamp; the drive model's
        //    overspeed_decay brings the car back down afterwards.
        driveHandling.GripRate = handling.GripRate * DriftBoost.GripMultiplier(drift, handling);
        driveHandling.MaxYawRate = handling.MaxYawRate * DriftBoost.YawAuthority(drift, handling);
        driveHandling.BoostMultiplier = drift.IsBoosting
            ? DriftBoost.BoostTargetSpeed(drift) / Mathf.Max(0.01f, handling.TopSpeed)
            : handling.BoostMultiplier;

        // The drive model's Boost input now means "Layer C says boosting", not "Left Shift is down".
        // The old Left Ctrl grip button is not in the model, so it is never passed through.
        input.Boost = drift.IsBoosting;
        input.Drift = false;

        // 3. The normal drive tick.
        DriveState state = ReadDriveState();
        SteerInput = driveModel.Simulate(input, ref state, deltaTime);
        WriteDriveState(state);
    }

    // Overdrive power-up: fires a boost directly through the same system - no drift, no window, no
    // press timing. Stage 2 strength (provisional), enters Boosting so it carries the bump multiplier
    // and can extend a chain. Call from the power-up activation code (host / state authority).
    public void GrantOverdrive()
    {
        DriftBoostState drift = ReadDriftState();
        DriftBoost.StartBoost(ref drift, 2, driveModel.ForwardSpeed, handling, Runner.Tick.Raw);
        WriteDriftState(drift);
    }

    // Respawn clears both reserve and chain. Call from the respawn code.
    public void ResetDrift()
    {
        DriftBoostState drift = ReadDriftState();
        DriftBoost.Reset(ref drift);
        WriteDriftState(drift);
        contactSinceLastTick = false;
    }

    // Entered by a hard wall impact (inside the tick) or, later, by a slam from another car.
    public void EnterSpinout(Vector3 travelDirection, float spinSign)
    {
        DriveState state = ReadDriveState();
        driveModel.EnterSpinout(ref state, travelDirection, spinSign);
        WriteDriveState(state);
    }

    // Contact must never cost a player their charge: it just opens the release window next tick.
    // Ground contacts are ignored - only walls and other cars count.
    void OnCollisionEnter(Collision c) => NoteContact(c);
    void OnCollisionStay(Collision c) => NoteContact(c);

    void NoteContact(Collision c)
    {
        for (int k = 0; k < c.contactCount; k++)
        {
            if (c.GetContact(k).normal.y < 0.5f)
            {
                contactSinceLastTick = true;
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (!matchState.HasState) return; // networked SteerInput isn't readable without state
        visuals.Animate(ForwardSpeed, SteerInput, driveModel.Probes, Time.deltaTime);
    }

    DriveState ReadDriveState() => new DriveState
    {
        YawRate = YawRate,
        BrakeHoldSeconds = BrakeHoldSeconds,
        SpinoutTicksRemaining = SpinoutTicksRemaining,
        ImmunityTicksRemaining = ImmunityTicksRemaining,
        SettleHeading = SettleHeading,
        SpinDegrees = SpinDegrees,
    };

    void WriteDriveState(in DriveState state)
    {
        YawRate = state.YawRate;
        BrakeHoldSeconds = state.BrakeHoldSeconds;
        SpinoutTicksRemaining = state.SpinoutTicksRemaining;
        ImmunityTicksRemaining = state.ImmunityTicksRemaining;
        SettleHeading = state.SettleHeading;
        SpinDegrees = state.SpinDegrees;
    }

    DriftBoostState ReadDriftState() => new DriftBoostState
    {
        Phase = (DriftPhase)DriftPhaseValue,
        PhaseTicks = DriftPhaseTicks,
        Direction = DriftDirection,
        Reserve = DriftReserve,
        HopHeldLastTick = DriftHopHeld,
        BoostTicksRemaining = BoostTicksRemaining,
        BoostMultiplier = BoostMultiplier,
        BoostBaseSpeed = BoostBaseSpeed,
        ChainGraceTicksRemaining = ChainGraceTicksRemaining,
        ChainLinks = ChainLinks,
        LastBoostStartTick = LastBoostStartTick,
    };

    void WriteDriftState(in DriftBoostState s)
    {
        DriftPhaseValue = (int)s.Phase;
        DriftPhaseTicks = s.PhaseTicks;
        DriftDirection = s.Direction;
        DriftReserve = s.Reserve;
        DriftHopHeld = s.HopHeldLastTick;
        BoostTicksRemaining = s.BoostTicksRemaining;
        BoostMultiplier = s.BoostMultiplier;
        BoostBaseSpeed = s.BoostBaseSpeed;
        ChainGraceTicksRemaining = s.ChainGraceTicksRemaining;
        ChainLinks = s.ChainLinks;
        LastBoostStartTick = s.LastBoostStartTick;
    }
}
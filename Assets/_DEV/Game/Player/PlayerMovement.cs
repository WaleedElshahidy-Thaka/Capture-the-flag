using UnityEngine;
using Fusion;

// Real dynamic Rigidbody driven by real forces - collisions with walls and other cars are
// resolved by PhysX itself, not by hand-written math. Rotation is locked to yaw-only (frozen
// X/Z) so the car can't tip over.
//
// Networked: input is gathered once per tick by PlayerInputSampler via QuickMatchFusionService's
// OnInputAction (Fusion's input model requires that, not a direct Keyboard.current read here)
// and applied in FixedUpdateNetwork, which runs on Runner.DeltaTime instead of Unity's own
// FixedUpdate. A NetworkTransform sibling component (Forecast Physics mode) replicates the
// resulting Rigidbody motion - this component only ever applies forces, it doesn't network
// anything itself.
//
// Exists (and is positioned/visible) from the moment its player connects, not only once a
// match starts - PlayerMatchState.CanMove (a separate sibling component; matchmaking state
// isn't this class's concern) is what actually gates whether input gets turned into force, set
// by MatchStarter once the match begins.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] float engineForce = 2000f;
    [SerializeField] float turnSpeed = 150f; // degrees/sec, hard cap - not torque, so it can never spin out of control
    [SerializeField] float maxSpeed = 22f; // m/s

    [Header("Boost - hold Left Shift")]
    [SerializeField] float boostMultiplier = 1.8f; // applied to both engineForce and maxSpeed while held

    [Header("Drift - hold Left Ctrl while turning")]
    [SerializeField] float normalFriction = 1f;
    [SerializeField] float driftFriction = 0.15f; // lower = more sideways slide

    [Header("Tire visuals (cosmetic only - purely for looking right)")]
    [SerializeField] Transform frontLeftTire;
    [SerializeField] Transform frontRightTire;
    [SerializeField] Transform rearLeftTire;
    [SerializeField] Transform rearRightTire;
    [SerializeField] float tireRadius = 0.3f;
    [SerializeField] float maxTireSteerAngle = 30f; // degrees

    Rigidbody body;
    PlayerMatchState matchState;
    PhysicsMaterial frictionMaterial;
    float steerInput; // cached from the last simulated tick's input, purely for LateUpdate's cosmetic tire yaw
    float tireRollAngle;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        matchState = GetComponent<PlayerMatchState>();
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // A per-car material instance (not a shared asset) so drift only affects this car's
        // own grip, never another car's collider that happens to reference the same asset.
        frictionMaterial = new PhysicsMaterial("CarFriction")
        {
            dynamicFriction = normalFriction,
            staticFriction = normalFriction,
            frictionCombine = PhysicsMaterialCombine.Multiply,
        };
        GetComponent<Collider>().material = frictionMaterial;
    }

    // In Shared Mode, whichever peer calls Runner.Spawn becomes State Authority for the result -
    // and PlayerLobbySpawner only ever spawns a car for its OWN local player's join, so
    // HasStateAuthority here always means "this is my own car" (never a remote player's). Input
    // Authority is still a separate thing Fusion won't assign on its own, and GetInput only
    // returns input for whoever holds it, so FixedUpdateNetwork below would silently get none
    // without this. Camera wiring rides along for the same reason: only the local player's own
    // car should be followed, and this is the one place that's known for certain.
    public override void Spawned()
    {
        if (Object.HasStateAuthority == false) return;

        Object.AssignInputAuthority(Runner.LocalPlayer);

        var cameraGO = GameObject.Find("Main Camera");
        if (cameraGO != null && cameraGO.TryGetComponent(out PlayerCamera cam))
            cam.SetTarget(body);
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerNetInput input) == false) return;
        if (matchState.CanMove == false) return;

        float throttleInput = input.ThrottleAxis;
        steerInput = input.SteerAxis;
        bool boostInput = input.Boost;
        bool driftInput = input.Drift;

        float currentEngineForce = boostInput ? engineForce * boostMultiplier : engineForce;
        float currentMaxSpeed = boostInput ? maxSpeed * boostMultiplier : maxSpeed;

        if (throttleInput != 0f && body.linearVelocity.magnitude < currentMaxSpeed)
            body.AddForce(transform.forward * throttleInput * currentEngineForce);

        // Set directly rather than AddTorque: torque's actual turn rate depends on the
        // Rigidbody's mass/inertia, which is easy to get wrong and can spin the car up far
        // faster than intended (that's what was happening - not a tuning issue, a runaway
        // one). Setting angularVelocity directly makes turnSpeed a hard, predictable cap no
        // matter what mass or collider size this ends up with.
        //
        // Only overrides it while actually steering - previously this ran unconditionally
        // and zeroed angularVelocity every tick even when not steering, which also erased any
        // spin a collision had just imparted before it was ever visible. With no override,
        // physics (including hits) governs rotation, decaying naturally via angularDamping.
        if (steerInput != 0f)
            body.angularVelocity = new Vector3(0f, steerInput * turnSpeed * Mathf.Deg2Rad, 0f);

        // Drift: real physics, not hand-written slide math - lowering the collider's own
        // friction is what actually lets the car slide sideways through a turn instead of
        // gripping and turning on a dime.
        float targetFriction = driftInput ? driftFriction : normalFriction;
        frictionMaterial.dynamicFriction = targetFriction;
        frictionMaterial.staticFriction = targetFriction;
    }

    // Purely cosmetic - doesn't feed back into movement at all. Rolls all 4 tires around their
    // own local X axis based on actual forward speed (so it looks right even if engineForce/
    // maxSpeed get retuned later), and yaws the front two on Y to mimic steered wheels. Runs
    // on the render frame (not FixedUpdate) since it's just visual polish.
    void LateUpdate()
    {
        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        tireRollAngle += (forwardSpeed / tireRadius) * Mathf.Rad2Deg * Time.deltaTime;
        tireRollAngle %= 360f;

        float steerAngle = steerInput * maxTireSteerAngle;

        SetTire(frontLeftTire, steerAngle);
        SetTire(frontRightTire, steerAngle);
        SetTire(rearLeftTire, 0f);
        SetTire(rearRightTire, 0f);
    }

    void SetTire(Transform tire, float steerAngle)
    {
        if (tire == null) return;
        tire.localRotation = Quaternion.Euler(tireRollAngle, steerAngle, 0f);
    }
}

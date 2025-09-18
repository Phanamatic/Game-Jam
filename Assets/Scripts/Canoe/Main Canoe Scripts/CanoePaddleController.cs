using UnityEngine;
using UnityEngine.InputSystem;

/* CanoePaddleController (improved hydrodynamics)
 * Replaces discrete impulses with continuous drag-based force:
 *   F = 0.5 * rho * Cd * A_eff * |v_rel|^2 in the plane of water, applied at blade tip.
 * Smooth catch/release, submersion-based area, optional yaw bias for steering feel.
 * Exposes tip speed and wet state for hit scaling.
 */
[RequireComponent(typeof(Rigidbody))]
public class CanoePaddleController : MonoBehaviour
{
    /* ─── Scene refs ─── */
    [Header("Scene References")]
    [SerializeField] Transform paddle;          // full paddle transform
    [SerializeField] Transform starboardPivot;  // right-hand grip socket
    [SerializeField] Transform portPivot;       // left-hand  grip socket
    [SerializeField] Transform bladeTip;        // tip point; if null we approximate
    [SerializeField] Transform playerVisual;    // optional lean visual root

    [Header("Paddle Hitbox")]
    [Tooltip("Trigger collider on the blade. Added automatically if missing.")]
    [SerializeField] PaddleHitbox hitbox;

    /* ─── Water & grip ─── */
    [Header("Water + Grip")]
    [SerializeField] float waterLevel   = 0f;    // world Y of water surface
    [SerializeField] float handleHeight = 0.25f; // vertical offset for hands

    /* ─── Motion aiming ─── */
    [Header("Aiming")]
    [SerializeField] float submergePitch = -45f; // when pressed
    [SerializeField, Range(0,90)] float verticalDeadZone = 35f; // keep from stabbing down

    /* ─── Hydrodynamics ─── */
    [Header("Hydrodynamics")]
    [SerializeField] float waterDensity     = 1000f; // kg/m^3
    [SerializeField] float bladeArea        = 0.045f; // m^2 (typical canoe blade)
    [SerializeField] float CdBase           = 0.7f;   // face not square to flow
    [SerializeField] float CdMax            = 1.25f;  // face square to flow
    [SerializeField] float yawBias          = 0.45f;  // extra yaw torque from lateral force
    [SerializeField] float forceGain        = 1.00f;  // global gain knob
    [SerializeField] float fullSubmergeDepth= 0.20f;  // depth for A_eff = bladeArea
    [SerializeField] float minCatchSpeed    = 0.35f;  // m/s to start making useful force
    [SerializeField] float catchRiseTime    = 0.08f;  // s ramp-in
    [SerializeField] float releaseFallTime  = 0.06f;  // s ramp-out
    [SerializeField] float forceSmoothing   = 12f;    // lerp-exp rate
    [SerializeField] float antiTeleportDist = 0.65f;  // m/frame cap for tip delta
    [SerializeField] float maxTipSpeed      = 4.5f;   // clamp relative speed for stability
    [SerializeField] float maxLinearAccel   = 5.5f;   // m/s^2 cap to keep canoe controllable
    [SerializeField] float maxYawTorque     = 120f;   // N·m clamp for steering bias
    [Tooltip("Local blade face normal. Default assumes +X is blade face.")]
    [SerializeField] Vector3 bladeNormalLocal = Vector3.right;

    /* ─── Lean tuning ─── */
    const float leanAngle = 8f;  // avatar lean
    const float leanLerp  = 6f;

    /* ─── Internals ─── */
    Rigidbody   rb;
    Vector3     lastTip;
    Vector3     smoothedForce;
    float       catchBlend;      // 0..1 gate for catch/release
    bool        isLeftSide, prevLeftSide;
    bool        prevBladeWet;
    InputAction click;

    WaterEffectsManager waterEffects;

    // Public telemetry for hit scaling
    public float LastTipSpeed { get; private set; }
    public bool  BladeWet     { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!paddle)
        {
            Debug.LogError("CanoePaddleController: assign 'paddle' Transform.");
            enabled = false; return;
        }

        if (!hitbox)
        {
            hitbox = paddle.GetComponent<PaddleHitbox>();
            if (!hitbox) hitbox = paddle.gameObject.AddComponent<PaddleHitbox>();
        }
        hitbox.BindOwner(this);

        // Ignore paddle ↔ hull collisions
        foreach (var pc in paddle.GetComponentsInChildren<Collider>())
            foreach (var cc in GetComponentsInChildren<Collider>())
                if (pc != cc) Physics.IgnoreCollision(pc, cc);

        // Input
        click = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        click.Enable();

        waterEffects = Object.FindFirstObjectByType<WaterEffectsManager>();

        lastTip = PaddleTip();
    }

    void Update()
    {
        // Mouse aims the paddle around screen center
        var mouse = Mouse.current;
        Vector2 centre = new(Screen.width * .5f, Screen.height * .5f);
        Vector2 dir2D  = mouse != null ? mouse.position.ReadValue() - centre : Vector2.right;
        float   yawDeg = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg * -1f;

        isLeftSide = dir2D.x < 0f;
        Transform pivot   = isLeftSide ? portPivot : starboardPivot;
        if (!pivot) pivot = transform; // safety fallback
        Vector3   gripPos = pivot.position + Vector3.up * handleHeight;

        float pitch = Mathf.Lerp(0f, submergePitch, click.IsPressed() ? 1f : 0f);
        paddle.SetPositionAndRotation(
            gripPos,
            transform.rotation * Quaternion.Euler(pitch, yawDeg, 0f));

        // Cosmetic lean
        if (playerVisual)
        {
            float targetLean = (isLeftSide ? -leanAngle : leanAngle);
            Vector3 e = playerVisual.localEulerAngles;
            float zSrc = (e.z > 180 ? e.z - 360 : e.z);
            float newZ = Mathf.Lerp(zSrc, targetLean, leanLerp * Time.deltaTime);
            playerVisual.localRotation = Quaternion.Euler(e.x, e.y, newZ);
        }
    }

    void FixedUpdate()
    {
        Vector3 tip = PaddleTip();
        float dt = Time.fixedDeltaTime;

        // Reset tip history on side swap to avoid spikes
        if (isLeftSide != prevLeftSide)
        {
            lastTip = tip;
            prevLeftSide = isLeftSide;
        }

        // Tip velocity from transform motion (not Rigidbody)
        Vector3 rawDelta = tip - lastTip;
        if (rawDelta.magnitude > antiTeleportDist) rawDelta = rawDelta.normalized * antiTeleportDist;
        Vector3 tipVel = rawDelta / Mathf.Max(1e-5f, dt);
        LastTipSpeed = tipVel.magnitude;

        // Wet check with small depth threshold
        float depth = Mathf.Max(0f, waterLevel - tip.y);
        float submFactor = Mathf.Clamp01(depth / Mathf.Max(0.001f, fullSubmergeDepth));
        BladeWet = click.IsPressed() && depth > 0.02f;

        // Catch/release blend
        float targetCatch = (BladeWet && LastTipSpeed > minCatchSpeed) ? 1f : 0f;
        float tau = targetCatch > catchBlend ? catchRiseTime : releaseFallTime;
        catchBlend = LerpExp(catchBlend, targetCatch, tau, dt);

        // Hydrodynamic force when wet
        Vector3 hydroForce = Vector3.zero;
        if (catchBlend > 0f)
        {
            // Project velocity onto water plane to avoid unrealistic vertical thrust
            Vector3 vPlane = Vector3.ProjectOnPlane(tipVel, Vector3.up);
            float   vMag   = vPlane.magnitude;

            if (vMag > maxTipSpeed)
            {
                vPlane = vPlane.normalized * maxTipSpeed;
                vMag = maxTipSpeed;
            }

            if (vMag > 1e-3f)
            {
                // Angle of attack based on blade face vs motion
                Vector3 bladeNormal = paddle.TransformDirection(bladeNormalLocal).normalized;
                float aoa = Vector3.Angle(bladeNormal, vPlane.normalized); // 0..180
                float aoa01 = Mathf.Clamp01(Mathf.Sin(aoa * Mathf.Deg2Rad)); // 0 at 0°, 1 at 90°

                float Cd = Mathf.Lerp(CdBase, CdMax, aoa01); // more face-on -> higher drag
                float areaEff = bladeArea * submFactor;

                float forceMag = 0.5f * waterDensity * Cd * areaEff * vMag * vMag * forceGain * catchBlend;
                Vector3 dragDir = -vPlane.normalized; // resist motion
                hydroForce = dragDir * forceMag;

                float maxForce = Mathf.Max(0f, rb.mass * maxLinearAccel);
                if (maxForce > 0f && hydroForce.sqrMagnitude > maxForce * maxForce)
                    hydroForce = hydroForce.normalized * maxForce;

                // Optional yaw bias: convert lateral component into extra yaw torque
                float lateral = Vector3.Dot(hydroForce, transform.right);
                float yawMag = Mathf.Clamp(lateral * yawBias, -maxYawTorque, maxYawTorque);
                Vector3 yawTorque = Vector3.up * yawMag;
                rb.AddTorque(yawTorque, ForceMode.Force);
            }
        }

        // Smooth the applied force to remove jitter
        smoothedForce = Vector3.Lerp(smoothedForce, hydroForce, 1f - Mathf.Exp(-forceSmoothing * dt));

        // Apply at the tip for realistic torque
        if (smoothedForce.sqrMagnitude > 0f)
            rb.AddForceAtPosition(smoothedForce, tip, ForceMode.Force);

        // Water effects
        if (BladeWet && !prevBladeWet && waterEffects != null)
            waterEffects.OnWaterCollision(tip, 1.25f); // catch splash

        if (BladeWet && waterEffects != null && LastTipSpeed > minCatchSpeed * 1.25f)
        {
            float rip = Mathf.Clamp01(LastTipSpeed / 3f) * submFactor * 0.7f;
            if (rip > 0.05f) waterEffects.OnWaterCollision(tip, rip);
        }

        prevBladeWet = BladeWet;
        lastTip = tip;
    }

    Vector3 PaddleTip()
    {
        if (bladeTip) return bladeTip.position;
        // Fallback: estimate tip along +forward at ~blade length of 1.5 m
        return paddle.position + paddle.forward * 1.5f;
    }

    static float LerpExp(float current, float target, float timeConstant, float dt)
    {
        if (timeConstant <= 0f) return target;
        float k = 1f - Mathf.Exp(-dt / timeConstant);
        return current + (target - current) * k;
    }

    public bool PaddleLeftSide => isLeftSide;
}

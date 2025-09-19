using UnityEngine;
using UnityEngine.InputSystem;

/* CanoePaddleController (transform-delta tip velocity, capped)
 * Exposes LastTipSpeed and BladeWet for PaddleHitbox.
 */
[RequireComponent(typeof(Rigidbody))]
public class CanoePaddleController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Transform paddle;
    [SerializeField] Transform starboardPivot;
    [SerializeField] Transform portPivot;
    [SerializeField] Transform bladeTip;
    [SerializeField] Transform playerVisual;
    [SerializeField] PaddleHitbox hitbox;

    [Header("Water + Grip")]
    [SerializeField] float waterLevel = 0f;
    [SerializeField] float handleHeight = 0.25f;

    [Header("Aiming")]
    [SerializeField] float submergePitch = -45f;
    [SerializeField, Range(0,90)] float verticalDeadZone = 45f;

    [Header("Hydrodynamics")]
    [SerializeField] float waterDensity = 1000f;
    [SerializeField] float bladeArea = 0.055f;
    [SerializeField] float CdBase = 0.7f;
    [SerializeField] float CdMax  = 1.35f;
    [SerializeField] float fullSubmergeDepth = 0.22f;
    [SerializeField] float minCatchSpeed = 0.3f;
    [SerializeField] float catchRiseTime = 0.07f;
    [SerializeField] float releaseFallTime = 0.06f;
    [SerializeField] float forceSmoothRate = 14f;
    [SerializeField] float yawBias = 0.45f;
    [SerializeField] float forceMultiplier = 1.35f;

    [Header("Safety Caps")]
    [SerializeField] float maxForceN = 320f;
    [SerializeField] float maxTorqueNm = 420f;
    [SerializeField] float maxLeverArm = 1.35f;

    [Header("Numerics")]
    [SerializeField] float antiTeleportDist = 1.6f;
    [SerializeField] Vector3 bladeNormalLocal = Vector3.right;

    const float leanAngle = 8f, leanLerp = 6f;

    Rigidbody rb;
    InputAction click;
    WaterEffectsManager waterFx;

    Vector3 lastTipPos, smoothedForce;
    float   catchBlend;
    bool    isLeftSide, prevLeftSide, prevBladeWet;

    public float LastTipSpeed { get; private set; }
    public bool  BladeWet     { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!paddle) { Debug.LogError("Assign 'paddle'"); enabled = false; return; }

        if (!hitbox) hitbox = paddle.GetComponent<PaddleHitbox>() ?? paddle.gameObject.AddComponent<PaddleHitbox>();
        hitbox.BindOwner(this);

        foreach (var pc in paddle.GetComponentsInChildren<Collider>())
            foreach (var cc in GetComponentsInChildren<Collider>())
                if (pc != cc) Physics.IgnoreCollision(pc, cc);

        click = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        click.Enable();

        waterFx = Object.FindFirstObjectByType<WaterEffectsManager>();
    }

    void OnEnable()
    {
        lastTipPos = GetTipWorld();
        smoothedForce = Vector3.zero;
        catchBlend = 0f;
        prevBladeWet = false;
        prevLeftSide = isLeftSide;
    }

    void Update()
    {
        Vector2 centre = new(Screen.width * .5f, Screen.height * .5f);
        var m = Mouse.current;
        Vector2 dir2D  = m != null ? m.position.ReadValue() - centre : Vector2.right;
        float   yawDeg = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg * -1f;

        isLeftSide = dir2D.x < 0f;
        Transform pivot = isLeftSide ? portPivot : starboardPivot;
        if (!pivot) pivot = transform;

        Vector3 gripPos = pivot.position + Vector3.up * handleHeight;
        float pitch = Mathf.Lerp(0f, submergePitch, click.IsPressed() ? 1f : 0f);
        paddle.SetPositionAndRotation(gripPos, transform.rotation * Quaternion.Euler(pitch, yawDeg, 0f));

        if (playerVisual)
        {
            float targetLean = isLeftSide ? -leanAngle : leanAngle;
            Vector3 e = playerVisual.localEulerAngles;
            float z = (e.z > 180 ? e.z - 360 : e.z);
            float newZ = Mathf.Lerp(z, targetLean, leanLerp * Time.deltaTime);
            playerVisual.localRotation = Quaternion.Euler(e.x, e.y, newZ);
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 tip = GetTipWorld();

        if (isLeftSide != prevLeftSide) { prevLeftSide = isLeftSide; lastTipPos = tip; smoothedForce = Vector3.zero; }

        Vector3 delta = tip - lastTipPos;
        float maxDelta = antiTeleportDist;
        if (delta.magnitude > maxDelta) delta = delta.normalized * maxDelta;
        Vector3 tipVel = delta / Mathf.Max(1e-5f, dt);
        LastTipSpeed = tipVel.magnitude;

        float depth = Mathf.Max(0f, waterLevel - tip.y);
        float subm  = Mathf.Clamp01(depth / Mathf.Max(0.001f, fullSubmergeDepth));

        bool press = click.IsPressed();
        float angleFromHoriz = Vector3.Angle(tipVel, Vector3.ProjectOnPlane(tipVel, Vector3.up));
        bool horizontalEnough = angleFromHoriz < verticalDeadZone;
        BladeWet = press && depth > 0.02f && LastTipSpeed > 0.01f && horizontalEnough;

        float targetCatch = (BladeWet && LastTipSpeed > minCatchSpeed) ? 1f : 0f;
        float tau = targetCatch > catchBlend ? catchRiseTime : releaseFallTime;
        catchBlend = LerpExp(catchBlend, targetCatch, tau, dt);

        Vector3 force = Vector3.zero;
        if (catchBlend > 0f && subm > 0f)
        {
            Vector3 vPlane = Vector3.ProjectOnPlane(tipVel, Vector3.up);
            float v = vPlane.magnitude;
            if (v > 1e-3f)
            {
                Vector3 faceN = paddle.TransformDirection(bladeNormalLocal).normalized;
                float aoa = Vector3.Angle(faceN, vPlane.normalized);
                float aoa01 = Mathf.Clamp01(Mathf.Sin(aoa * Mathf.Deg2Rad));
                float Cd = Mathf.Lerp(CdBase, CdMax, aoa01);

                float F = 0.5f * waterDensity * Cd * bladeArea * subm * v * v * catchBlend;
                force = -vPlane.normalized * F * forceMultiplier;

                if (force.magnitude > maxForceN) force = force.normalized * maxForceN;
            }
        }

        smoothedForce = Vector3.Lerp(smoothedForce, force, 1f - Mathf.Exp(-forceSmoothRate * dt));

        if (smoothedForce.sqrMagnitude > 0f)
        {
            Vector3 r = tip - rb.worldCenterOfMass;
            if (r.magnitude > maxLeverArm) r = r.normalized * maxLeverArm;

            Vector3 torque = Vector3.Cross(r, smoothedForce);
            float tMag = torque.magnitude;
            if (tMag > maxTorqueNm)
            {
                float scale = maxTorqueNm / Mathf.Max(1e-5f, tMag);
                smoothedForce *= scale; torque *= scale;
            }

            rb.AddForceAtPosition(smoothedForce, rb.worldCenterOfMass + r, ForceMode.Force);

            float lateral = Vector3.Dot(smoothedForce, transform.right);
            if (Mathf.Abs(lateral) > 0.01f)
                rb.AddTorque(Vector3.up * (lateral * yawBias), ForceMode.Force);
        }

        if (BladeWet && !prevBladeWet && waterFx) waterFx.OnWaterCollision(tip, 1.1f);
        if (BladeWet && waterFx && LastTipSpeed > minCatchSpeed * 1.25f)
        {
            float rip = Mathf.Clamp01(LastTipSpeed / 3f) * subm * 0.6f;
            if (rip > 0.04f) waterFx.OnWaterCollision(tip, rip);
        }

        prevBladeWet = BladeWet;
        lastTipPos = tip;
    }

    Vector3 GetTipWorld(){ return bladeTip ? bladeTip.position : paddle.position + paddle.forward * 1.5f; }
    static float LerpExp(float current, float target, float timeConstant, float dt){ if (timeConstant<=0f) return target; float k=1f-Mathf.Exp(-dt/timeConstant); return current + (target-current)*k; }
    public bool PaddleLeftSide => isLeftSide;
}

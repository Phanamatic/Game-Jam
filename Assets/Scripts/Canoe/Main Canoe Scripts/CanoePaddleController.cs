using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/*
 * CanoePaddleController — Silly Arcade Edition (with PaddleLeftSide)
 *
 * Fun adds:
 * - Turbo Stroke crits, Paddle-Slap Hop, Spin Dash, Sine Wobble, Banana Slip.
 * Kept:
 * - Bow/Stern axis, blade force at tip, directional drag + mild righting.
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

    [Header("Canoe Context Points")]
    [SerializeField] Transform bowPoint;
    [SerializeField] Transform sternPoint;

    [Header("Water/Blade")]
    [SerializeField] float handleHeight = 0.25f;
    [SerializeField] float bladeArea = 0.06f;
    [SerializeField] float submergePitch = -45f;

    [Header("Stroke Power (base)")]
    [SerializeField] float catchRamp = 0.08f;
    [SerializeField] float releaseRamp = 0.06f;
    [SerializeField] float baseThrust = 38f;
    [SerializeField] float forwardBoost = 1.9f;
    [SerializeField, Range(0f,1f)] float lateralFraction = 0.10f;
    [SerializeField] float maxForce = 1200f;
    [SerializeField] float maxTorque = 1400f;

    [Header("Silly Spice")]
    [SerializeField, Range(0f,1f)] float turboCritChance = 0.22f;
    [SerializeField] float turboCritMult = 1.8f;
    [SerializeField] float wobbleAmp = 0.6f;
    [SerializeField] float wobbleFreq = 6.0f;
    [SerializeField, Range(0f,1f)] float bananaSlipChance = 0.08f;
    [SerializeField] float bananaSlipStrength = 0.6f;

    [Header("Tricks")]
    [SerializeField] float spinDashTorque = 420f;
    [SerializeField] float spinDashUpKick = 1.2f;
    [SerializeField] float doubleClickWindow = 0.22f;
    [SerializeField] float slapHopImpulse = 2.8f;
    [SerializeField] float slapSpeedThresh = 2.5f;

    [Header("Hull Directional Drag + Righting")]
    [SerializeField] float forwardDragCoeff = 0.3f;
    [SerializeField] float lateralDragCoeff = 2.8f;
    [SerializeField] float yawQuadDamp = 90f;
    [SerializeField] float rollRighting = 40f;
    [SerializeField] float maxRollDeg = 35f;

    [Header("FX Hooks (optional)")]
    public UnityEvent onTurboStroke;
    public UnityEvent onSpinDash;
    public UnityEvent onSlapHop;

    const float leanAngle = 10f, leanLerp = 7f;

    Rigidbody rb;
    SimpleWaterRuntime water;
    InputAction click;

    bool isLeftSide, prevLeftSide, inStroke, didCrit;
    float rho = 1000f;
    float strokeGain, strokePhase;
    Vector3 lastTip, strokeStart;
    float lastPressTime, lastReleaseSpeed;

    // Exposed for other scripts (e.g., hand IK targets)
    public bool PaddleLeftSide => isLeftSide;

    Vector3 CanoeForward => (bowPoint && sternPoint) ? (bowPoint.position - sternPoint.position).normalized : transform.forward;
    Vector3 CanoeRight   { get { var f = CanoeForward; var r = Vector3.Cross(Vector3.up, f); return r.sqrMagnitude < 1e-6f ? transform.right : r.normalized; } }
    Vector3 CanoeUp      => Vector3.Cross(CanoeRight, CanoeForward);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = Mathf.Max(0.1f, rb.linearDamping);
        rb.angularDamping = Mathf.Max(0.25f, rb.angularDamping);
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        click = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        click.Enable();

        water = SimpleWaterRuntime.Instance;
        if (water) rho = water.density;

        bladeArea = Mathf.Clamp(bladeArea, 0.02f, 0.10f);
    }

    void Update()
    {
        Vector2 centre = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir2D  = Mouse.current.position.ReadValue() - centre;
        float yawDeg   = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg * -1f;

        isLeftSide = dir2D.x < 0f;
        Transform pivot = isLeftSide ? portPivot : starboardPivot;
        Vector3 gripPos = pivot ? pivot.position + Vector3.up * handleHeight : transform.position;

        float pitch = Mathf.Lerp(0f, submergePitch, click.IsPressed() ? 1f : 0f);
        if (paddle) paddle.SetPositionAndRotation(gripPos, transform.rotation * Quaternion.Euler(pitch, yawDeg, 0f));

        if (playerVisual)
        {
            float targetLean = isLeftSide ? -leanAngle : leanAngle;
            Vector3 e = playerVisual.localEulerAngles;
            float z = Mathf.LerpAngle((e.z > 180 ? e.z - 360 : e.z), targetLean, leanLerp * Time.deltaTime);
            playerVisual.localRotation = Quaternion.Euler(e.x, e.y, z);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            float now = Time.time;
            if (now - lastPressTime <= doubleClickWindow) TrySpinDash();
            lastPressTime = now;
        }
    }

    void FixedUpdate()
    {
        if (!paddle) return;

        Vector3 tip = PaddleTip();

        if (isLeftSide != prevLeftSide)
        { lastTip = tip; prevLeftSide = isLeftSide; inStroke = false; didCrit = false; }

        if (click.IsPressed())
        {
            strokeGain = Mathf.MoveTowards(strokeGain, 1f, Time.fixedDeltaTime / Mathf.Max(0.01f, catchRamp));
            if (!inStroke) { inStroke = true; strokeStart = tip; strokePhase = 0f; didCrit = false; }
        }
        else
        {
            if (inStroke && lastReleaseSpeed > slapSpeedThresh) DoSlapHop();
            strokeGain = Mathf.MoveTowards(strokeGain, 0f, Time.fixedDeltaTime / Mathf.Max(0.01f, releaseRamp));
            inStroke = false;
        }

        ApplyDirectionalDragAndRighting();

        float waterY = water ? water.HeightAt(tip) : 0f;
        bool submerged = tip.y <= waterY;

        if (submerged)
        {
            Vector3 fwd = CanoeForward;
            Vector3 right = CanoeRight;

            Vector3 vBoatAtTip = rb.GetPointVelocity(tip);
            Vector3 vPaddle    = (tip - lastTip) / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            float paddleSpeed  = vPaddle.magnitude;
            lastReleaseSpeed   = paddleSpeed;

            bool stroking = paddleSpeed > 0.4f && click.IsPressed();

            if (stroking)
            {
                float travel = (tip - strokeStart).magnitude;
                strokePhase  = Mathf.Clamp01(travel / 1.7f);

                Vector3 motionDir = vPaddle.normalized;
                Vector3 raw = -motionDir;

                Vector3 longi  = Vector3.Project(raw, fwd) * forwardBoost;
                Vector3 lateral= (raw - Vector3.Project(raw, fwd)) * lateralFraction;

                if (!didCrit && Random.value < bananaSlipChance)
                    lateral += (isLeftSide ? -right : right) * bananaSlipStrength;

                float wobble = Mathf.Sin(Time.time * wobbleFreq) * wobbleAmp;
                lateral += (isLeftSide ? right : -right) * wobble * 0.1f;

                Vector3 strokeVec = longi + lateral;

                float turbo = 1f;
                if (!didCrit && Random.value < turboCritChance)
                {
                    turbo = turboCritMult;
                    didCrit = true;
                    onTurboStroke?.Invoke();
                }

                float area = bladeArea * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((waterY - tip.y) / 0.22f));
                float q    = 0.5f * rho * area * paddleSpeed * paddleSpeed;
                Vector3 force = strokeVec.sqrMagnitude > 1e-8f ? strokeVec.normalized : fwd;
                force *= Mathf.Min(q * baseThrust * turbo * strokeGain, maxForce);

                rb.AddForceAtPosition(force, tip, ForceMode.Force);

                float yawSign = isLeftSide ? 1f : -1f;
                float yawAmt  = Vector3.Dot(force, fwd) * 0.08f * yawSign;
                Vector3 torque = Vector3.up * yawAmt;
                if (torque.magnitude > maxTorque) torque = torque.normalized * maxTorque;
                rb.AddTorque(torque, ForceMode.Force);

                Debug.DrawRay(tip, force * 0.01f, Color.red);
            }
        }

        lastTip = tip;
    }

    // Tricks
    void TrySpinDash()
    {
        float sign = isLeftSide ? 1f : -1f;
        rb.AddTorque(Vector3.up * spinDashTorque * sign, ForceMode.Impulse);
        rb.AddForce(Vector3.up * spinDashUpKick, ForceMode.Impulse);
        onSpinDash?.Invoke();
    }

    void DoSlapHop()
    {
        rb.AddForce(Vector3.up * slapHopImpulse, ForceMode.Impulse);
        onSlapHop?.Invoke();
    }

    // Helpers
    void ApplyDirectionalDragAndRighting()
    {
        Vector3 v   = rb.linearVelocity;
        Vector3 fwd = CanoeForward;
        Vector3 right = CanoeRight;

        float vF = Vector3.Dot(v, fwd);
        float vL = Vector3.Dot(v, right);

        Vector3 dragF = -fwd  * vF * Mathf.Abs(vF) * forwardDragCoeff;
        Vector3 dragL = -right* vL * Mathf.Abs(vL) * lateralDragCoeff;
        rb.AddForce(dragF + dragL, ForceMode.Force);

        float yaw = rb.angularVelocity.y;
        rb.AddTorque(Vector3.up * -Mathf.Sign(yaw) * yaw * yaw * yawQuadDamp, ForceMode.Force);

        float roll = SignedRollDeg(transform, fwd);
        float excess = Mathf.Abs(roll) - maxRollDeg;
        if (excess > 0f)
        {
            float dir = Mathf.Sign(roll);
            rb.AddTorque(-fwd * dir * excess * rollRighting, ForceMode.Force);
        }
    }

    static float SignedRollDeg(Transform t, Vector3 fwdAxis)
    {
        Vector3 upProj = Vector3.ProjectOnPlane(t.up, fwdAxis).normalized;
        return Vector3.SignedAngle(Vector3.up, upProj, fwdAxis);
    }

    Vector3 PaddleTip() =>
        bladeTip ? bladeTip.position
                 : (paddle ? paddle.position + paddle.forward * 1.5f : transform.position);

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            if (bowPoint && sternPoint)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(sternPoint.position, bowPoint.position);
                Gizmos.DrawSphere(bowPoint.position, 0.05f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(sternPoint.position, 0.05f);
            }
            return;
        }
        if (rb)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(rb.worldCenterOfMass, rb.linearVelocity * 0.2f);
        }
    }
}

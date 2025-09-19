// CanoeStabilizer.cs
// Frequency-based PD (ζ, ωn) with dead-zone and anti-windup clamp.
// Works with upside-down imports via invertUp.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CanoeStabilizer : MonoBehaviour
{
    [Header("Target Up")]
    [SerializeField] bool invertUp = false;

    [Header("Control (roll/pitch same)")]
    [Tooltip("Natural frequency in Hz. 0.8–1.5 is calm. Higher = snappier but can jitter.")]
    [SerializeField] float freqHz = 1.0f;
    [Tooltip("Damping ratio. 1.0 = critically damped. 0.7 gives a little bounce.")]
    [SerializeField] float damping = 1.0f;
    [Tooltip("Ignore tiny heel to prevent micro-jitter.")]
    [SerializeField] float deadZoneDeg = 1.2f;

    [Header("Limits")]
    [SerializeField] float maxTorque = 1800f;

    [Header("Adaptive Angular Drag")]
    [SerializeField] float angDragLow = 0.2f;
    [SerializeField] float angDragHigh = 1.4f;
    [SerializeField] float heelForMaxDragDeg = 18f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;              // reduce visual jitter
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        if (rb.angularDamping < angDragLow) rb.angularDamping = angDragLow;
    }

    void FixedUpdate()
    {
        Vector3 desiredUp = invertUp ? Vector3.down : Vector3.up;

        // Local axes
        Vector3 fwd = transform.forward; // roll axis
        Vector3 right = transform.right; // pitch axis

        // Errors (deg)
        float rollDeg  = SignedAngleAroundAxis(transform.up, desiredUp, fwd);
        float pitchDeg = SignedAngleAroundAxis(transform.up, desiredUp, right);

        // Dead-zone
        if (Mathf.Abs(rollDeg)  < deadZoneDeg)  rollDeg  = 0f;
        if (Mathf.Abs(pitchDeg) < deadZoneDeg) pitchDeg = 0f;

        // Rates (deg/s) in local axes
        Vector3 wLocal = transform.InverseTransformDirection(rb.angularVelocity);
        float rollRateDeg  =  wLocal.z * Mathf.Rad2Deg;
        float pitchRateDeg =  wLocal.x * Mathf.Rad2Deg;

        // Compute PD gains from frequency + damping
        float wn = Mathf.Max(0.1f, freqHz) * (2f * Mathf.PI);   // rad/s
        float kp = wn * wn;                                     // per deg -> convert below
        float kd = 2f * damping * wn;

        // Convert deg errors to rad for torque scale consistency
        float rollErrRad  = rollDeg  * Mathf.Deg2Rad;
        float pitchErrRad = pitchDeg * Mathf.Deg2Rad;
        float rollRateRad = rollRateDeg  * Mathf.Deg2Rad;
        float pitchRateRad= pitchRateDeg * Mathf.Deg2Rad;

        // PD torques
        float rollT  = Mathf.Clamp((-kp * rollErrRad)  + (-kd * rollRateRad),  -maxTorque, maxTorque);
        float pitchT = Mathf.Clamp((-kp * pitchErrRad) + (-kd * pitchRateRad), -maxTorque, maxTorque);

        rb.AddTorque(fwd * rollT + right * pitchT, ForceMode.Force);

        // Adaptive angular drag by heel
        float heel = Mathf.Abs(rollDeg);
        float t = Mathf.Clamp01(heel / Mathf.Max(1f, heelForMaxDragDeg));
        rb.angularDamping = Mathf.Lerp(angDragLow, angDragHigh, t);
    }

    static float SignedAngleAroundAxis(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 a = Vector3.ProjectOnPlane(from, axis);
        Vector3 b = Vector3.ProjectOnPlane(to,   axis);
        return Vector3.SignedAngle(a, b, axis);
    }
}

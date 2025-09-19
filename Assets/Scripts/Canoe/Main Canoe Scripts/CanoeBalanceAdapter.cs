using UnityEngine;
using UnityEngine.InputSystem;

/* CanoeBalanceAdapter
 * Bridges BalanceMinigame ↔ physics.
 * - Reads roll, updates the minigame UI.
 * - Applies PD + player torque scaled by Authority.
 * - Kills player on capsize.
 * Add to canoe root with a Rigidbody.
 */
[RequireComponent(typeof(Rigidbody))]
public class CanoeBalanceAdapter : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] BalanceMinigame ui;   // optional; auto-find if null
    [SerializeField] PlayerHealth health;  // optional; auto-find if null
    [SerializeField] float waterLevel = 0f; // optional visual; not required

    [Header("Capsize")]
    [SerializeField] float capsizeAngle = 48f; // deg roll threshold to die

    [Header("PD Righting")]
    [SerializeField] float kp = 32f; // Nm/deg
    [SerializeField] float kd = 6f;  // Nm/(deg/s)

    [Header("Player Control")]
    [SerializeField] float inputAccel = 165f;   // Nm/s from minigame lean
    [SerializeField] float maxBalanceAccel = 320f; // Nm/s clamp
    [SerializeField, Range(0f,1f)] float minInputAuthority = 0.2f;
    [SerializeField] float inputAuthorityPower = 0.65f;

    [Header("Stability Response")]
    [SerializeField, Range(0f,1f)] float minPdAuthority = 0.06f;
    [SerializeField] float pdAuthorityPower = 0.7f;
    [SerializeField] float destabilizeBiasAccel = 60f;
    [SerializeField] float destabilizeOffsetAccel = 75f;

    [Header("Wobble")]
    [SerializeField] float wobbleAccel = 14f;  // Nm/s
    [SerializeField] float wobbleHz = 0.42f;
    [SerializeField] float wobbleChaos = 0.22f;
    [SerializeField] float wobbleSlew = 1.6f;

    Rigidbody rb;
    float wobblePhase, wobbleEnvelope;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!ui)      ui = FindFirstObjectByType<BalanceMinigame>();
        if (!health)  health = GetComponent<PlayerHealth>();
        if (rb.angularDamping < 0.08f) rb.angularDamping = 0.08f;
    }

    void OnEnable()
    {
        wobblePhase = Random.value * Mathf.PI * 2f;
        wobbleEnvelope = 0f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Roll (deg) and roll rate (deg/s) about local Z
        float rollDeg  = GetSignedRollDeg();
        float rollRate = Vector3.Dot(rb.angularVelocity, transform.forward) * Mathf.Rad2Deg;

        // Update UI (normalized bias = roll / capsizeAngle, clamped)
        if (ui)
        {
            float norm = Mathf.Clamp(rollDeg / Mathf.Max(1f, capsizeAngle), -1f, 1f);
            ui.SetRollState(norm, rollDeg);
        }

        // Player input from UI
        float authority = ui ? ui.Authority : 1f;           // 0..1
        float lean      = ui ? ui.ManualLean : 0f;          // -1..1
        float instability = ui ? ui.Instability : 0f;
        float instability01 = Mathf.Clamp01(instability);
        float instabilityCurve = Mathf.SmoothStep(0f, 1f, instability01);

        float authorityClamped = Mathf.Clamp01(authority);
        float bias = ui ? ui.RollBias : Mathf.Clamp(rollDeg / Mathf.Max(1f, capsizeAngle), -1f, 1f);
        float targetOffset = ui ? Mathf.Clamp(ui.TargetOffset, -1f, 1f) : 0f;

        // PD toward upright, heavily reduced when out of balance
        float pdAuthority = Mathf.Lerp(minPdAuthority, 1f, Mathf.Pow(authorityClamped, pdAuthorityPower));
        pdAuthority *= Mathf.Lerp(1f, 0.35f, instabilityCurve);
        float pdTorque  = -(kp * rollDeg + kd * rollRate) * pdAuthority;

        // Player torque scaled by authority but never fully zeroed
        float inputAuthority = Mathf.Lerp(minInputAuthority, 1f, Mathf.Pow(authorityClamped, inputAuthorityPower));
        float playerTorque = -lean * inputAccel * inputAuthority;

        // Smooth wobble signal without harsh jitter
        wobblePhase += Mathf.Max(0.05f, wobbleHz) * Mathf.PI * 2f * dt;
        wobblePhase = Mathf.Repeat(wobblePhase, Mathf.PI * 2f);
        float wobbleNoise = Mathf.PerlinNoise(Time.time * wobbleChaos, 0.37f) * 2f - 1f;
        float wobbleLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, wobbleSlew) * dt);
        wobbleEnvelope = Mathf.Lerp(wobbleEnvelope, wobbleNoise, wobbleLerp);
        float wobble = Mathf.Sin(wobblePhase) * wobbleAccel * (0.55f + 0.45f * Mathf.Abs(wobbleEnvelope));
        wobble *= Mathf.Lerp(0.6f, 1.8f, instabilityCurve);

        // Push the canoe over when the player fails the minigame
        float failureTorque = (bias * destabilizeBiasAccel + targetOffset * destabilizeOffsetAccel) * instabilityCurve;

        // Sum + clamp
        float totalNmPerSec = Mathf.Clamp(pdTorque + playerTorque + wobble + failureTorque, -maxBalanceAccel, maxBalanceAccel);
        rb.AddRelativeTorque(Vector3.forward * totalNmPerSec, ForceMode.Acceleration);

        // Capsize check
        if (Mathf.Abs(rollDeg) >= capsizeAngle)
        {
            health?.Kill();
            rb.AddRelativeTorque(transform.forward * Mathf.Sign(rollDeg) * 40f, ForceMode.Impulse);
        }
    }

    float GetSignedRollDeg()
    {
        Quaternion yawOnly = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion local = Quaternion.Inverse(yawOnly) * transform.rotation;
        float z = local.eulerAngles.z; if (z > 180f) z -= 360f; return z;
    }
}

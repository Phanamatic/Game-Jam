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
    [SerializeField] float inputAccel = 120f;   // Nm/s from minigame lean
    [SerializeField] float maxBalanceAccel = 220f; // Nm/s clamp

    [Header("Wobble")]
    [SerializeField] float wobbleAccel = 10f;  // Nm/s
    [SerializeField] float wobbleHz = 0.35f;
    [SerializeField] float wobbleChaos = 0.22f;
    [SerializeField] float wobbleAuthorityBoost = 2.2f;

    [Header("Instability Punishment")]
    [SerializeField] float imbalanceTorqueAccel = 140f;
    [SerializeField] float imbalanceCurve = 1.45f;
    [SerializeField] float imbalanceSmooth = 6f;

    Rigidbody rb;
    float wobbleFiltered;
    float imbalanceState;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!ui)      ui = FindFirstObjectByType<BalanceMinigame>();
        if (!health)  health = GetComponent<PlayerHealth>();
        if (rb.angularDamping < 0.08f) rb.angularDamping = 0.08f;
    }

    void OnEnable()
    {
        wobbleFiltered = 0f;
        imbalanceState = 0f;
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
        float authority   = ui ? ui.Authority : 1f;           // 0..1
        float lean        = ui ? ui.ManualLean : 0f;          // -1..1
        float balanceErr  = ui ? ui.BalanceErrorNormalized : 0f;

        // PD toward upright, partially reduced when out of window
        float pdScale   = Mathf.Lerp(0.08f, 1f, authority); // never zero
        float pdTorque  = -(kp * rollDeg + kd * rollRate) * pdScale;

        // Player torque scaled by authority
        float playerTorque = -lean * inputAccel * authority;

        // Small wobble
        float t = Time.time;
        float wobSin = Mathf.Sin(t * wobbleHz * Mathf.PI * 2f);
        float wobPer = Mathf.PerlinNoise(t * wobbleHz, t * wobbleChaos) * 2f - 1f;
        float wobSample = (wobSin + wobPer) * 0.5f;
        wobbleFiltered = Mathf.Lerp(wobbleFiltered, wobSample, 1f - Mathf.Exp(-3f * dt));
        float wobbleStrength = Mathf.Lerp(1f, wobbleAuthorityBoost, Mathf.Clamp01(1f - authority));
        float wobble = wobbleFiltered * wobbleAccel * wobbleStrength;

        // Punish being outside the window with escalating torque
        float errMagnitude = Mathf.Clamp01(Mathf.Abs(balanceErr));
        float errSign = Mathf.Sign(balanceErr);
        float imbalanceTarget = errSign * Mathf.Pow(errMagnitude, imbalanceCurve);
        imbalanceState = Mathf.Lerp(imbalanceState, imbalanceTarget, 1f - Mathf.Exp(-imbalanceSmooth * dt));
        float punishTorque = imbalanceState * imbalanceTorqueAccel;

        // Sum + clamp
        float totalNmPerSec = Mathf.Clamp(pdTorque + playerTorque + wobble + punishTorque, -maxBalanceAccel, maxBalanceAccel);
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

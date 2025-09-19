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

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!ui)      ui = FindFirstObjectByType<BalanceMinigame>();
        if (!health)  health = GetComponent<PlayerHealth>();
        if (rb.angularDamping < 0.08f) rb.angularDamping = 0.08f;
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

        // PD toward upright, partially reduced when out of window
        float pdScale   = Mathf.Lerp(0.25f, 1f, authority); // never zero
        float pdTorque  = -(kp * rollDeg + kd * rollRate) * pdScale;

        // Player torque scaled by authority
        float playerTorque = -lean * inputAccel * authority;

        // Small wobble
        float t = Time.time;
        float wobSin = Mathf.Sin(t * wobbleHz * Mathf.PI * 2f);
        float wobPer = Mathf.PerlinNoise(t * wobbleHz, t * wobbleChaos) * 2f - 1f;
        float wobble = (wobSin * (1f + 0.5f * wobPer)) * wobbleAccel;

        // Sum + clamp
        float totalNmPerSec = Mathf.Clamp(pdTorque + playerTorque + wobble, -maxBalanceAccel, maxBalanceAccel);
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

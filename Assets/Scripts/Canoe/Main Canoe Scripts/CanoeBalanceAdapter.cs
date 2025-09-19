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
    [SerializeField] float inputAccel = 140f;   // Nm/s from minigame lean
    [SerializeField] float maxBalanceAccel = 320f; // Nm/s clamp

    [Header("Wobble")]
    [SerializeField] float wobbleAccel = 12f;  // Nm/s
    [SerializeField] float wobbleHz = 0.3f;
    [SerializeField] float wobbleChaos = 0.18f;
    [SerializeField] float wobbleInstabilityBonus = 45f;

    [Header("Instability")]
    [SerializeField] float instabilityGrowth = 1.8f;
    [SerializeField] float instabilityDecay = 0.9f;
    [SerializeField] float authorityInstability = 1.4f;
    [SerializeField] float instabilityTorque = 65f;

    Rigidbody rb;
    float wobblePhase;
    float instability;
    bool insideWindow = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!ui)      ui = FindFirstObjectByType<BalanceMinigame>();
        SubscribeUI();
        if (!health)  health = GetComponent<PlayerHealth>();
        if (rb.angularDamping < 0.08f) rb.angularDamping = 0.08f;
    }

    void OnValidate()
    {
        if (maxBalanceAccel < 1f) maxBalanceAccel = 1f;
        if (instabilityGrowth < 0f) instabilityGrowth = 0f;
        if (instabilityDecay < 0f) instabilityDecay = 0f;
    }

    void OnEnable(){ instability = 0f; wobblePhase = 0f; SubscribeUI(); }
    void OnDisable(){ UnsubscribeUI(); }
    void OnDestroy(){ UnsubscribeUI(); }

    void SubscribeUI()
    {
        if (!ui)
            ui = FindFirstObjectByType<BalanceMinigame>();

        if (!ui)
        {
            insideWindow = true;
            return;
        }

        ui.OnInsideWindowChanged -= HandleInsideWindowChanged;
        ui.OnInsideWindowChanged += HandleInsideWindowChanged;
        insideWindow = ui.InsideWindow;
    }

    void UnsubscribeUI()
    {
        if (ui)
            ui.OnInsideWindowChanged -= HandleInsideWindowChanged;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (!ui)
            SubscribeUI();

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
        float pdScale   = Mathf.Lerp(0.08f, 1f, Mathf.Pow(authority, 1.35f));
        float pdTorque  = -(kp * rollDeg + kd * rollRate) * pdScale;

        // Player torque scaled by authority
        float playerTorque = -lean * inputAccel * Mathf.Lerp(0.15f, 1f, authority);

        // Instability from poor balance
        float dtAuthority = Mathf.Clamp01(1f - authority);
        float instabTarget = insideWindow ? dtAuthority * authorityInstability : Mathf.Max(instability, 1f + dtAuthority);
        instability = Mathf.MoveTowards(instability, instabTarget, instabilityGrowth * dt);
        if (insideWindow)
            instability = Mathf.MoveTowards(instability, 0f, instabilityDecay * dt);
        instability = Mathf.Clamp(instability, 0f, 1.5f);

        // Smooth wobble that strengthens with instability
        wobblePhase += dt * (wobbleHz + instability * wobbleChaos) * Mathf.PI * 2f;
        float wobble = Mathf.Sin(wobblePhase) * (wobbleAccel + instability * wobbleInstabilityBonus);
        float destDir = Mathf.Sign(rollDeg + rollRate * 0.18f);
        float destabilize = destDir * instability * instabilityTorque;

        // Sum + clamp
        float totalNmPerSec = Mathf.Clamp(pdTorque + playerTorque + wobble + destabilize, -maxBalanceAccel, maxBalanceAccel);
        rb.AddRelativeTorque(Vector3.forward * totalNmPerSec, ForceMode.Acceleration);

        // Capsize check
        if (Mathf.Abs(rollDeg) >= capsizeAngle)
        {
            health?.Kill();
            rb.AddRelativeTorque(transform.forward * Mathf.Sign(rollDeg) * 40f, ForceMode.Impulse);
        }
    }

    void HandleInsideWindowChanged(bool inside)
    {
        insideWindow = inside;
    }

    float GetSignedRollDeg()
    {
        Quaternion yawOnly = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion local = Quaternion.Inverse(yawOnly) * transform.rotation;
        float z = local.eulerAngles.z; if (z > 180f) z -= 360f; return z;
    }
}

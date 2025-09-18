using UnityEngine;
using UnityEngine.InputSystem;

/* CanoeBalance
 * Keeps the canoe upright by translating A/D input into counter-torque while
 * water wobble tries to tip it. A BalanceMinigame UI mediates how much control
 * the player has; slipping out of the target window amplifies wobble and weakens
 * recovery. Paddle hits temporarily make balancing harder. Capsize calls
 * PlayerHealth.Kill().
 */
[RequireComponent(typeof(Rigidbody))]
public class CanoeBalance : MonoBehaviour
{
    [Header("Balance Window")]
    [Tooltip("Roll limit in degrees before capsize.")]
    [SerializeField] float capsizeAngle = 45f;
    [Tooltip("Player counter-torque accel from A/D.")]
    [SerializeField] float inputTorqueAccel = 140f;
    [Tooltip("Gentle self-righting accel toward roll = 0.")]
    [SerializeField] float autoRightingAccel = 20f;

    [Header("Water Wobble")]
    [SerializeField] float wobbleAccel = 30f;     // torque accel
    [SerializeField] float wobbleHz    = 0.6f;    // sine base
    [SerializeField] float wobbleChaos = 0.35f;   // perlin mod
    [SerializeField, Range(0f, 2f)] float wobbleFailBonus = 0.85f; // extra wobble when UI missed

    [Header("Hit Disruption")]
    [SerializeField] float hitKickDegrees   = 8f;   // roll kick impulse
    [SerializeField] float harderMultOnHit  = 1.8f; // >1 = harder
    [SerializeField] float harderDuration   = 2.5f; // seconds
    [SerializeField] float harderDecayPerSec= 1.2f; // smooth recovery

    [Header("Balance Mini-game")]
    [SerializeField] bool autoCreateMinigame   = true;
    [SerializeField, Range(0f, 1f)] float minTorqueFactor   = 0.2f;
    [SerializeField, Range(0f, 1f)] float minAutoRighting   = 0.35f;

    Rigidbody rb;
    PlayerHealth health;
    BalanceMinigame minigame;

    float harderMult = 1f;
    float harderTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<PlayerHealth>();
        if (rb.angularDamping < 0.05f) rb.angularDamping = 0.05f; // small damping keeps it lively

        if (autoCreateMinigame)
            minigame = GetComponent<BalanceMinigame>() ?? gameObject.AddComponent<BalanceMinigame>();
        else
            minigame = GetComponent<BalanceMinigame>();
    }

    void FixedUpdate()
    {
        float roll = GetSignedRollDeg();
        float capAngle = Mathf.Max(1f, Mathf.Abs(capsizeAngle));
        float rollNorm = Mathf.Clamp(roll / capAngle, -1f, 1f);

        float authority = 1f;
        float input = 0f;

        if (minigame && minigame.isActiveAndEnabled)
        {
            minigame.SetRollBias(rollNorm);
            authority = Mathf.Clamp01(minigame.Authority);
            input = minigame.ManualLean;
        }
        else
        {
            var kbd = Keyboard.current;
            if (kbd != null)
            {
                if (kbd.aKey.isPressed) input -= 1f;
                if (kbd.dKey.isPressed) input += 1f;
            }
        }

        float torqueFactor = Mathf.Lerp(minTorqueFactor, 1f, authority);
        float autoFactor = Mathf.Lerp(minAutoRighting, 1f, authority);
        float wobbleFactor = 1f + (1f - authority) * wobbleFailBonus;

        // Water wobble around local Z (roll)
        float t = Time.time;
        float wobSin = Mathf.Sin(t * wobbleHz * Mathf.PI * 2f);
        float wobPer = Mathf.PerlinNoise(t * wobbleHz, t * wobbleChaos) * 2f - 1f;
        float wobble = wobSin * (1f + 0.6f * wobPer);
        rb.AddRelativeTorque(Vector3.forward * (wobbleAccel * wobble * harderMult * wobbleFactor), ForceMode.Acceleration);

        // Player input: derived from balancing mini-game or fallback keyboard.
        rb.AddRelativeTorque(Vector3.forward * (-inputTorqueAccel * input * torqueFactor / Mathf.Max(1f, harderMult)), ForceMode.Acceleration);

        // Auto-righting proportional to roll fraction
        float righting = Mathf.Sign(roll) * Mathf.Min(Mathf.Abs(roll) / capAngle, 1f);
        rb.AddRelativeTorque(Vector3.forward * (-autoRightingAccel * righting * autoFactor), ForceMode.Acceleration);

        // Capsize check
        if (Mathf.Abs(roll) >= capAngle)
        {
            health?.Kill();
            rb.AddRelativeTorque(transform.forward * Mathf.Sign(roll) * 50f, ForceMode.Impulse);
        }

        // Disruption decay
        if (harderTimer > 0f)
        {
            harderTimer -= Time.fixedDeltaTime;
            if (harderTimer <= 0f) harderMult = 1f;
        }
        else if (harderMult > 1f)
        {
            harderMult = Mathf.Max(1f, harderMult - harderDecayPerSec * Time.fixedDeltaTime);
        }
    }

    float GetSignedRollDeg()
    {
        // Measure roll around canoe’s forward axis without yaw contamination
        Quaternion yawOnly = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion local = Quaternion.Inverse(yawOnly) * transform.rotation;
        float z = local.eulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    // Called by PaddleHitbox on the victim canoe
    public void ApplyPaddleHit(Vector3 attackerToVictimDir, float strength01)
    {
        Vector3 localDir = transform.InverseTransformDirection(attackerToVictimDir.normalized);
        float sideSign = Mathf.Sign(localDir.x); // + right, - left

        float kickDeg = hitKickDegrees * Mathf.Clamp01(strength01);
        float impulse = kickDeg * 0.8f; // degrees→impulse mapping (empirical)
        rb.AddRelativeTorque(Vector3.forward * sideSign * impulse, ForceMode.Impulse);

        harderMult = Mathf.Max(harderMult, harderMultOnHit);
        harderTimer = Mathf.Max(harderTimer, harderDuration);
    }
}

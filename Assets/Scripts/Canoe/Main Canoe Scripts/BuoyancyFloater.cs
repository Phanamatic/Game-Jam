using UnityEngine;

/*
  BuoyancyFloater
  - Flat water at y = waterLevelY
  - Per-point buoyancy: F = rho * g * displacedVolume
  - Simple water drag and angular drag when submerged
  - Classic Rigidbody only. Forces in FixedUpdate.
  - No direct writes to velocity or rotation.
*/

[RequireComponent(typeof(Rigidbody))]
public class BuoyancyFloater : MonoBehaviour
{
    [Header("Water")]
    [SerializeField] float waterLevelY = 0f;
    [SerializeField] float waterDensity = 1000f; // kg/m^3
    [SerializeField] float gravity = 9.81f;

    [Header("Hull Sampling")]
    [Tooltip("Points around the hull used to approximate submerged volume.")]
    [SerializeField] Transform[] floatPoints;

    [Tooltip("Total displaced volume at full submersion (m^3). Tune to craft size/mass.")]
    [SerializeField] float hullVolume = 0.11f;

    [Header("Water Damping")]
    [SerializeField] float waterLinearDrag = 1.8f;
    [SerializeField] float waterAngularDrag = 1.2f;

    [Header("Safety")]
    [Tooltip("Max total force applied this FixedUpdate to avoid spikes.")]
    [SerializeField] float maxStepForce = 4000f;

    [Header("Stabilization")]
    [Tooltip("Applies a gentle torque to keep the craft's local up axis aligned with world up.")]
    [SerializeField] bool stabilizeUpright = true;
    [Tooltip("Local axis that should point up when the canoe is upright.")]
    [SerializeField] Vector3 localUpAxis = Vector3.up;
    [SerializeField] float uprightTorqueStrength = 45f;
    [SerializeField] float uprightTorqueDamping = 8f;
    [SerializeField] float maxUprightTorque = 1500f;

    Rigidbody rb;
    float defaultDrag, defaultAngularDrag;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        defaultDrag = rb.linearDamping;
        defaultAngularDrag = rb.angularDamping;
        EnsureLocalUpAxis();
    }

    void FixedUpdate()
    {
        if (floatPoints == null || floatPoints.Length == 0) return;

        int submergedCount = 0;
        float subFracAccum = 0f;

        // Accumulate total force this step to clamp safely
        Vector3 stepForceSum = Vector3.zero;

        foreach (var p in floatPoints)
        {
            if (!p) continue;

            Vector3 pos = p.position;
            float depth = waterLevelY - pos.y; // positive under surface

            if (depth > 0f)
            {
                submergedCount++;

                // Local submersion fraction at this sample (0..1)
                float pointFrac = Mathf.Clamp01(depth / 0.5f);
                subFracAccum += pointFrac;

                // Displaced volume distributed per point
                float pointVolume = (hullVolume / floatPoints.Length) * pointFrac;

                // Upward buoyancy at this point
                Vector3 Fb = Vector3.up * waterDensity * gravity * pointVolume;

                // Linear water drag opposing local point velocity
                Vector3 vPoint = rb.GetPointVelocity(pos);
                Vector3 Fd = -vPoint * waterLinearDrag * pointFrac;

                // Sum for clamp, then apply at position preserving torque
                Vector3 F = Fb + Fd;
                stepForceSum += F;

                // If we’re over budget, scale down this contribution
                float remaining = Mathf.Max(0f, maxStepForce - stepForceSum.magnitude);
                float fMag = F.magnitude;
                if (fMag > remaining && fMag > 1e-5f)
                {
                    F *= remaining / fMag;
                    // Correct running sum to reflect scaled force
                    stepForceSum = stepForceSum - (Fb + Fd) + F;
                }

                rb.AddForceAtPosition(F, pos, ForceMode.Force);
            }
        }

        // Angular and linear damping scale with submersion
        float subRatio = Mathf.Clamp01(subFracAccum / Mathf.Max(1, floatPoints.Length));
        rb.angularDamping = Mathf.Lerp(defaultAngularDrag, waterAngularDrag, subRatio);
        rb.linearDamping        = Mathf.Lerp(defaultDrag,        waterLinearDrag,  subRatio * 0.5f);

        if (stabilizeUpright)
        {
            ApplyUprightTorque();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Water line box for quick visual
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.6f);
        Vector3 a = new Vector3(-5, waterLevelY, -5);
        Vector3 b = new Vector3( 5, waterLevelY, -5);
        Vector3 c = new Vector3( 5, waterLevelY,  5);
        Vector3 d = new Vector3(-5, waterLevelY,  5);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);

        if (floatPoints != null)
        {
            foreach (var p in floatPoints)
            {
                if (!p) continue;
                Vector3 pos = p.position;
                Gizmos.color = pos.y < waterLevelY ? Color.cyan : Color.gray;
                Gizmos.DrawSphere(pos, 0.04f);
            }
        }
    }

    void OnValidate()
    {
        EnsureLocalUpAxis();
        uprightTorqueStrength = Mathf.Max(0f, uprightTorqueStrength);
        uprightTorqueDamping = Mathf.Max(0f, uprightTorqueDamping);
        maxUprightTorque = Mathf.Max(0f, maxUprightTorque);
    }

    void EnsureLocalUpAxis()
    {
        if (localUpAxis.sqrMagnitude < 1e-6f)
            localUpAxis = Vector3.up;
    }

    void ApplyUprightTorque()
    {
        Vector3 craftUp = transform.TransformDirection(localUpAxis);
        if (craftUp.sqrMagnitude < 1e-6f)
            return;
        craftUp.Normalize();

        Vector3 worldUp = Vector3.up;
        Vector3 torqueAxis = Vector3.Cross(craftUp, worldUp);
        float angle = torqueAxis.magnitude; // ~sin(theta)

        Vector3 corrective = Vector3.zero;
        if (angle > 1e-5f)
        {
            Vector3 torqueDir = torqueAxis.normalized;
            corrective = torqueDir * (angle * uprightTorqueStrength);
        }

        Vector3 angularTilt = Vector3.ProjectOnPlane(rb.angularVelocity, craftUp);
        corrective -= angularTilt * uprightTorqueDamping;

        float mag = corrective.magnitude;
        if (mag > 1e-5f && maxUprightTorque > 0f && mag > maxUprightTorque)
        {
            corrective = corrective / mag * maxUprightTorque;
        }

        if (corrective.sqrMagnitude > 1e-8f)
        {
            rb.AddTorque(corrective, ForceMode.Force);
        }
    }
}

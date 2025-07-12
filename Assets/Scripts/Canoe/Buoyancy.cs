using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    /* ───── Buoyancy Parameters (kept) ───── */
    [Header("Buoyancy Parameters")]
    [SerializeField] private float density = 1000f;          // kg / m³
    [SerializeField] private float volume  = 1f;             // m³ displaced per point
    [SerializeField] private float waterLevel = 0f;          // Y of water surface
    [SerializeField, Range(0f, 1f)]
    private float dragInWater = 0.2f;                        // ↓ NEW default = 0.2
    [SerializeField] private Transform[] buoyancyPoints;     // empties on hull

    /* ───── Internals ───── */
    private Rigidbody rb;
    private float gravity;                                   // |g|

    void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        gravity = Mathf.Abs(Physics.gravity.y);

        if (buoyancyPoints == null || buoyancyPoints.Length == 0)
        {
            Debug.LogWarning(
                "No buoyancy points assigned! Using transform centre as fallback.");
            buoyancyPoints = new Transform[] { transform };
        }
    }

    void FixedUpdate()
    {
        foreach (Transform point in buoyancyPoints)
            ApplyBuoyancyAndDrag(point.position);
    }

    /* ---------- core ---------- */
    private void ApplyBuoyancyAndDrag(Vector3 pointPosition)
    {
        float depth = waterLevel - pointPosition.y;          // >0 when under water
        if (depth <= 0f) return;

        /* 1 ─ Archimedes’ lift (unchanged) */
        float displaced = Mathf.Min(volume, depth);
        Vector3 buoyancy = Vector3.up * (density * gravity * displaced);
        rb.AddForceAtPosition(buoyancy, pointPosition, ForceMode.Force);

        /* 2 ─ Hydrodynamic drag (milder) */
        Vector3 velocity = rb.GetPointVelocity(pointPosition);
        Vector3 drag = -velocity * dragInWater * depth;
        rb.AddForceAtPosition(drag, pointPosition, ForceMode.Force);
    }
}

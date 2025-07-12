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
    
    /* ───── Bouncy Water Effect ───── */
    [Header("Bouncy Water Effect")]
    [SerializeField] private float bouncyForce = 5000f;      // Force multiplier for bouncy effect
    [SerializeField] private float bouncyDuration = 3f;      // How long bouncy effect lasts
    
    /* ───── Internals ───── */
    private Rigidbody rb;
    private float gravity;                                   // |g|
    private bool isBouncyMode = false;
    private float bouncyTimer = 0f;
    private float originalDensity;

    void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        gravity = Mathf.Abs(Physics.gravity.y);
        originalDensity = density;

        if (buoyancyPoints == null || buoyancyPoints.Length == 0)
        {
            Debug.LogWarning(
                "No buoyancy points assigned! Using transform centre as fallback.");
            buoyancyPoints = new Transform[] { transform };
        }
    }

    void FixedUpdate()
    {
        // Handle bouncy timer
        if (isBouncyMode)
        {
            bouncyTimer -= Time.fixedDeltaTime;
            if (bouncyTimer <= 0f)
            {
                EndBouncyMode();
            }
        }
        
        foreach (Transform point in buoyancyPoints)
            ApplyBuoyancyAndDrag(point.position);
    }

    /* ---------- core ---------- */
    private void ApplyBuoyancyAndDrag(Vector3 pointPosition)
    {
        float depth = waterLevel - pointPosition.y;          // >0 when under water
        if (depth <= 0f) return;

        /* 1 ─ Archimedes' lift (with bouncy modification) */
        float displaced = Mathf.Min(volume, depth);
        float effectiveDensity = isBouncyMode ? density * bouncyForce : density;
        Vector3 buoyancy = Vector3.up * (effectiveDensity * gravity * displaced);
        rb.AddForceAtPosition(buoyancy, pointPosition, ForceMode.Force);

        /* 2 ─ Hydrodynamic drag (milder) */
        Vector3 velocity = rb.GetPointVelocity(pointPosition);
        Vector3 drag = -velocity * dragInWater * depth;
        rb.AddForceAtPosition(drag, pointPosition, ForceMode.Force);
    }
    
    /* ───── Public Methods for Bouncy Effect ───── */
    public void StartBouncyMode()
    {
        isBouncyMode = true;
        bouncyTimer = bouncyDuration;
        Debug.Log("BOUNCY WATER ACTIVATED!");
    }
    
    public void EndBouncyMode()
    {
        isBouncyMode = false;
        Debug.Log("Bouncy water ended");
    }
    
    public bool IsBouncyMode => isBouncyMode;
}

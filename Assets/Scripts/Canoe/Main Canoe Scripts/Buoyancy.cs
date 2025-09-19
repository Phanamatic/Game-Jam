using UnityEngine;

/*
 * Buoyancy
 * Multi-point buoyancy. Uses SimpleWaterRuntime if present.
 * If absent, treats water as a flat plane at Y=0.
 */
[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    [Header("Float Points (3–4 keel corners)")]
    [SerializeField] Transform[] points;
    [Tooltip("Displaced volume at full submersion per point (m^3).")]
    [SerializeField] float pointVolume = 0.22f;

    [Header("Spring Model")]
    [SerializeField] float fullDepth = 0.25f; // meters to reach full lift
    [SerializeField] float pointDamp = 500f;  // vertical damping per point

    [Header("Hydrodynamic drag")]
    [SerializeField] float linearDrag = 0.7f;
    [SerializeField] float quadraticDrag = 0.4f;
    [SerializeField] float lateralMultiplier = 1.1f;

    [Header("Fallback water level if no SimpleWaterRuntime")]
    [SerializeField] float flatWaterY = 0f;

    Rigidbody rb;
    SimpleWaterRuntime water;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (points == null || points.Length == 0) points = new Transform[] { transform };
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        water = SimpleWaterRuntime.Instance;
        if (rb.linearDamping < 0.12f) rb.linearDamping = 0.12f;
        if (rb.angularDamping < 0.25f) rb.angularDamping = 0.25f;
    }

    void FixedUpdate()
    {
        float rho = water ? water.density : 1000f;
        float g = 9.81f;
        float kPerPoint = rho * g * (pointVolume / Mathf.Max(0.05f, fullDepth));

        foreach (var p in points)
        {
            Vector3 pos = p.position;
            float surfaceY = water ? water.HeightAt(pos) : flatWaterY;
            float depth = surfaceY - pos.y;
            if (depth <= 0f) continue;

            float clampedDepth = Mathf.Clamp(depth, 0f, fullDepth);
            float FbMag = kPerPoint * clampedDepth;
            Vector3 Fb = Vector3.up * FbMag;

            float vy = Vector3.Dot(rb.GetPointVelocity(pos), Vector3.up);
            Vector3 Fdamp = Vector3.up * (-pointDamp * vy);
            rb.AddForceAtPosition(Fb + Fdamp, pos, ForceMode.Force);

            Vector3 vPoint = rb.GetPointVelocity(pos);
            Vector3 vWater = water ? water.SurfaceVelocityAt(pos) : Vector3.zero;
            Vector3 vRel = vPoint - vWater;

            Vector3 vHoriz = new Vector3(vRel.x, 0f, vRel.z) * lateralMultiplier;
            Vector3 vVert  = new Vector3(0f, vRel.y, 0f);

            Vector3 Flinear = -(vHoriz + vVert) * linearDrag;
            Vector3 Fquad   = vRel.sqrMagnitude > 1e-4f ? -vRel.normalized * vRel.sqrMagnitude * quadraticDrag * 0.2f : Vector3.zero;
            rb.AddForceAtPosition(Flinear + Fquad, pos, ForceMode.Force);
        }
    }
}

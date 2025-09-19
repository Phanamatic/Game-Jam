using UnityEngine;

/*
 * PaddleSystem
 * - 360° mouse-orbit around canoe
 * - Click to dip to constant depth at y=water
 * - Hydrodynamic thrust via quadratic drag on the blade TIP
 * - Stable: no hard teleports, clamped per-step deltas, lateral-only drag
 *
 * Notes:
 * - Classic Rigidbody on canoe. No direct rotation writes.
 * - Blade collider stays trigger; force comes from water drag only.
 * - Camera ray -> water plane (y = waterLevelY).
 */

[DefaultExecutionOrder(10)]
public class PaddleSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform canoe;           // orbit center
    [SerializeField] Rigidbody canoeRb;
    [SerializeField] Transform paddleRoot;      // move/rotate this
    [SerializeField] Transform paddleShaft;     // visual
    [SerializeField] Transform blade;           // visual
    [SerializeField] Transform bladeTip;        // contact point
    [SerializeField] Camera cam;                // aim camera

    [Header("Orbit")]
    [SerializeField] float orbitRadius   = 1.6f;
    [SerializeField] float baseHeight    = 0.45f; // above water when not dipped
    [SerializeField] float orbitPosLerp  = 14f;
    [SerializeField] float orbitRotLerp  = 16f;
    [SerializeField] float maxStepLift   = 0.06f; // vertical correction clamp / frame

    [Header("Water")]
    [SerializeField] float waterLevelY   = 0f;
    [SerializeField] float submergeDepth = 0.18f; // tip depth when dipped
    [SerializeField] float entrySmoothing = 0.10f; // s

    [Header("Hydrodynamics")]
    [SerializeField] float rho      = 1000f;
    [SerializeField] float Cd       = 1.8f;
    [SerializeField] float area     = 0.085f;
    [SerializeField] float maxForce = 600f;      // per step clamp
    [SerializeField] float maxTipSpeed = 6.5f;   // m/s cap to prevent spikes

    [Header("Input")]
    [SerializeField] int dipMouseButton = 0; // LMB

    [Header("Debug")]
    [SerializeField] bool drawGizmos = false;

    Vector3 _targetOnWater;
    float _dipBlend;            // 0..1
    Vector3 _lastTipPos;
    Vector3 _tipVel;            // world m/s, FixedUpdate
    bool _inited;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!canoeRb && canoe) canoeRb = canoe.GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (bladeTip) { _lastTipPos = bladeTip.position; _inited = true; }
    }

    void Update()
    {
        if (!cam || !canoe || !paddleRoot) return;

        // Aim on water plane
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!IntersectRayY(ray, waterLevelY, out _targetOnWater))
        {
            _targetOnWater = ray.origin + ray.direction * 10f;
            _targetOnWater.y = waterLevelY;
        }

        // Azimuth position
        Vector3 to = _targetOnWater - canoe.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-5f) to = Vector3.forward;
        Vector3 desiredXZ = canoe.position + to.normalized * orbitRadius;

        // Vertical base
        float targetY = Mathf.Max(waterLevelY + baseHeight, waterLevelY);
        Vector3 desired = new Vector3(desiredXZ.x, targetY, desiredXZ.z);

        // Smooth position (no teleports)
        paddleRoot.position = Vector3.Lerp(
            paddleRoot.position, desired, 1f - Mathf.Exp(-orbitPosLerp * Time.deltaTime));

        // Face aim with pitch
        Vector3 fwd = (_targetOnWater - paddleRoot.position);
        if (fwd.sqrMagnitude < 1e-6f) fwd = paddleRoot.forward;
        Quaternion look = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        paddleRoot.rotation = Quaternion.Slerp(
            paddleRoot.rotation, look, 1f - Mathf.Exp(-orbitRotLerp * Time.deltaTime));

        // Dip factor
        float want = Input.GetMouseButton(dipMouseButton) ? 1f : 0f;
        float speed = entrySmoothing > 0f ? Time.deltaTime / entrySmoothing : 1f;
        _dipBlend = Mathf.MoveTowards(_dipBlend, want, speed);

        // Bring TIP toward target depth by gently lifting/lowering the whole rig
        if (bladeTip)
        {
            float targetTipY = Mathf.Lerp(waterLevelY + 0.02f, waterLevelY - submergeDepth, _dipBlend);
            float dy = Mathf.Clamp(targetTipY - bladeTip.position.y, -maxStepLift, maxStepLift);
            paddleRoot.position += new Vector3(0f, dy, 0f);
        }
    }

    void FixedUpdate()
    {
        if (!canoeRb || !bladeTip) return;
        if (!_inited) { _lastTipPos = bladeTip.position; _inited = true; return; }

        // Measure real world tip velocity (hand motion + canoe motion)
        Vector3 tip = bladeTip.position;
        _tipVel = (tip - _lastTipPos) / Mathf.Max(Time.fixedDeltaTime, 1e-6f);
        _lastTipPos = tip;

        // Cap numerically noisy spikes
        float spd = _tipVel.magnitude;
        if (spd > maxTipSpeed) _tipVel *= maxTipSpeed / spd;

        // Only when dipped and actually under water
        if (_dipBlend < 0.02f || tip.y >= waterLevelY - 0.002f) return;

        // Lateral component only (no vertical plunging thrust)
        // Use blade's local "broad face" normal to compute lateral direction
        Vector3 bladeNormal = blade ? blade.right : paddleRoot.right; // choose model’s broad normal
        Vector3 v = _tipVel;
        Vector3 vLat = v - Vector3.Project(v, bladeNormal.normalized); // remove component through the blade

        float vLatMag = vLat.magnitude;
        if (vLatMag < 0.02f) return;

        Vector3 vHat = vLat / vLatMag;
        Vector3 F = -0.5f * rho * Cd * area * vLatMag * vLatMag * vHat;

        // Scale with dip for smooth in/out
        F *= _dipBlend;

        // Clamp
        if (F.magnitude > maxForce) F = F.normalized * maxForce;

        canoeRb.AddForceAtPosition(F, tip, ForceMode.Force);
    }

    static bool IntersectRayY(Ray r, float y, out Vector3 hit)
    {
        if (Mathf.Abs(r.direction.y) < 1e-6f) { hit = default; return false; }
        float t = (y - r.origin.y) / r.direction.y;
        if (t < 0f) { hit = default; return false; }
        hit = r.origin + r.direction * t; return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !canoe) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(canoe.position + Vector3.up * 0.01f, orbitRadius);
        if (bladeTip)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(new Vector3(bladeTip.position.x, waterLevelY, bladeTip.position.z), 0.03f);
        }
    }
}

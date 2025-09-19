using UnityEngine;

/*
 * PaddleSystem
 *  - Orbits the paddle 360° around the canoe by mouse azimuth
 *  - On click, dips the blade below water to a constant depth
 *  - Computes blade-tip world velocity and applies water drag to the canoe via AddForceAtPosition
 *  - Uses a simple quadratic drag model: F = 0.5 * rho * Cd * A * |v_rel|^2 * (-v_hat)
 *  - Works with classic Rigidbody. No direct velocity writes. Forces in FixedUpdate only.
 *
 * Setup:
 *  - Place this on an empty "PaddleController" GameObject (or the Paddle Root).
 *  - Assign references in the Inspector.
 *  - Ensure Canoe has a classic Rigidbody with no constraints.
 *  - Water plane is y = 0. You can change via waterLevelY.
 *
 * Layers:
 *  - Put the blade collider on a "Blade" layer.
 *  - Ensure physics matrix ignores Blade vs Canoe layers to prevent collider forces.
 *  - The scripted hydrodynamic force replaces collision pushing.
 */

[DefaultExecutionOrder(10)]
public class PaddleSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform canoe;          // Orbit center (Canoe parent)
    [SerializeField] Rigidbody canoeRb;        // Canoe Rigidbody
    [SerializeField] Transform paddleRoot;     // Your "Paddle Root" (parent of paddle + hands)
    [SerializeField] Transform paddleShaft;    // "Paddle" stick
    [SerializeField] Transform blade;          // "End" (blade object)
    [SerializeField] Transform bladeTip;       // "Blade Tip" empty at blade front tip
    [SerializeField] Camera cam;               // Camera for mouse ray. If null -> Camera.main

    [Header("Orbit")]
    [SerializeField] float orbitRadius = 1.6f; // Distance from canoe center to paddle root
    [SerializeField] float heightFollow = 0.45f; // Base hand height above water when not dipped
    [Tooltip("How quickly the paddle root follows target azimuth (pos).")]
    [SerializeField] float orbitPosLerp = 18f;
    [Tooltip("How quickly the paddle root rotates to face the aim point (rot).")]
    [SerializeField] float orbitRotLerp = 18f;

    [Header("Model Orientation")]
    [Tooltip("Local axis on the paddle root that should face the aim point.")]
    [SerializeField] Vector3 localForwardAxis = Vector3.forward;
    [Tooltip("Local axis treated as up for the paddle root.")]
    [SerializeField] Vector3 localUpAxis = Vector3.up;

    [Header("Water")]
    [SerializeField] float waterLevelY = 0f;     // World Y of flat water
    [SerializeField] float submergeDepth = 0.18f;// Constant tip depth below water when clicked
    [SerializeField] float entrySmoothing = 0.12f; // Seconds. Blend dip to target to avoid pops

    [Header("Hydrodynamics (simple)")]
    [SerializeField] float rho = 1000f;     // kg/m^3
    [SerializeField] float Cd  = 1.8f;      // Drag coefficient for a flat blade in water
    [SerializeField] float area = 0.085f;   // m^2 effective area of blade
    [SerializeField] float maxForce = 850f; // Safety clamp for numerical spikes

    [Header("Input (legacy)")]
    [SerializeField] int dipMouseButton = 0; // 0=LMB

    [Header("Debug")]
    [SerializeField] bool drawGizmos = true;

    // Internal state
    Vector3 _targetWorld;         // Water-plane intersection
    Vector3 _lastTipPos;          // Previous world position of blade tip
    Vector3 _tipVel;              // World velocity of blade tip (per fixed step)
    float _dipBlend;              // 0..1 smooth dip factor
    bool _pressed;                // mouse down state captured in Update
    Quaternion _modelAlignment = Quaternion.identity; // Aligns custom local axes with look rotation

    void Reset()
    {
        cam = Camera.main;
    }

    void OnValidate()
    {
        _modelAlignment = ComputeModelAlignment();
    }

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!canoe && paddleRoot) canoe = paddleRoot; // fallback to self
        if (!canoeRb && canoe) canoeRb = canoe.GetComponentInParent<Rigidbody>();
        if (paddleRoot && canoe)
        {
            if (orbitRadius <= 0.01f)
                orbitRadius = Vector3.Distance(paddleRoot.position, canoe.position);
        }
        if (bladeTip) _lastTipPos = bladeTip.position;

        _modelAlignment = ComputeModelAlignment();
    }

    void Update()
    {
        // 1) Aim point on water plane from mouse
        if (!cam || !canoe || !paddleRoot) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (IntersectRayWithHorizontalPlane(ray, waterLevelY, out Vector3 hit))
        {
            _targetWorld = hit;
        }
        else
        {
            // Fallback: shoot far along ray to keep direction consistent
            _targetWorld = ray.origin + ray.direction * 10f;
            _targetWorld.y = waterLevelY;
        }

        // 2) Orbit target position around canoe at fixed radius
        Vector3 to = _targetWorld - canoe.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) to = Vector3.forward; // avoid NaN
        Vector3 orbitPos = canoe.position + to.normalized * orbitRadius;

        // Vertical base resting height when not dipped
        float targetY = Mathf.Max(waterLevelY + heightFollow, waterLevelY);

        // 3) Handle dip state (mouse)
        bool wantPressed = Input.GetMouseButton(dipMouseButton);
        _pressed = wantPressed;

        // Smooth dip factor for visual and force gating
        float dipTarget = _pressed ? 1f : 0f;
        float dipSpeed = entrySmoothing > 0f ? (Time.deltaTime / entrySmoothing) : 1f;
        _dipBlend = Mathf.MoveTowards(_dipBlend, dipTarget, dipSpeed);

        // 4) Place paddle root (smoothed)
        Vector3 cur = paddleRoot.position;
        Vector3 desired = new Vector3(orbitPos.x, targetY, orbitPos.z);
        paddleRoot.position = Vector3.Lerp(cur, desired, 1f - Mathf.Exp(-orbitPosLerp * Time.deltaTime));

        // 5) Orient paddle to look at the aim point, with pitch toward it
        Vector3 fwd = (_targetWorld - paddleRoot.position);
        if (fwd.sqrMagnitude < 0.0001f) fwd = paddleRoot.forward;
        Quaternion look = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        Quaternion targetRot = look * _modelAlignment;
        paddleRoot.rotation = Quaternion.Slerp(
            paddleRoot.rotation,
            targetRot,
            1f - Mathf.Exp(-orbitRotLerp * Time.deltaTime));

        // 6) Enforce blade tip depth visually while pressed: nudge blade along its local -Y (or shaft normal)
        //    We do not write directly to physics. This is a visual transform edit.
        if (bladeTip)
        {
            // Compute a world-space offset that brings tip to desired depth when dipped
            float targetTipY = Mathf.Lerp(waterLevelY + 0.02f, waterLevelY - submergeDepth, _dipBlend);
            Vector3 tipPos = bladeTip.position;
            float dy = targetTipY - tipPos.y;

            // Move paddleRoot vertically to achieve tip target depth, small and smooth.
            // Keep it subtle to avoid wild arm moves.
            float maxLift = 0.15f; // cap
            dy = Mathf.Clamp(dy, -maxLift, maxLift);
            paddleRoot.position += new Vector3(0f, dy, 0f);
        }
    }

    void FixedUpdate()
    {
        if (!canoeRb || !bladeTip) return;

        // Track tip velocity per physics step
        Vector3 tipPos = bladeTip.position;
        _tipVel = (tipPos - _lastTipPos) / Mathf.Max(Time.fixedDeltaTime, 1e-6f);
        _lastTipPos = tipPos;

        // Only generate force when dipped and actually submerged
        if (_dipBlend > 0.01f && tipPos.y < waterLevelY - 0.005f)
        {
            // Relative velocity blade vs water (water static)
            Vector3 v = _tipVel;

            float speed = v.magnitude;
            if (speed > 0.01f)
            {
                Vector3 vHat = v / speed;
                // Drag acts opposite motion
                Vector3 F = -0.5f * rho * Cd * area * speed * speed * vHat;

                // Blend with dip amount to make entry/exit smooth
                F *= Mathf.Clamp01(_dipBlend);

                // Clamp to avoid spikes
                if (F.magnitude > maxForce)
                    F = F.normalized * maxForce;

                // Apply at the tip position for realistic torque
                canoeRb.AddForceAtPosition(F, tipPos, ForceMode.Force);
            }
        }
    }

    static bool IntersectRayWithHorizontalPlane(Ray ray, float planeY, out Vector3 hit)
    {
        // Plane: y = planeY, normal up
        if (Mathf.Abs(ray.direction.y) < 1e-5f)
        {
            hit = default;
            return false;
        }
        float t = (planeY - ray.origin.y) / ray.direction.y;
        if (t < 0f)
        {
            hit = default;
            return false;
        }
        hit = ray.origin + ray.direction * t;
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (canoe)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(canoe.position + Vector3.up * 0.01f, orbitRadius);
        }
        if (bladeTip)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(new Vector3(bladeTip.position.x, waterLevelY, bladeTip.position.z), 0.025f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(bladeTip.position, bladeTip.position + _tipVel * 0.1f);
        }
    }

    Quaternion ComputeModelAlignment()
    {
        Quaternion modelOrientation = BuildOrientation(localForwardAxis, localUpAxis);
        return Quaternion.Inverse(modelOrientation);
    }

    static Quaternion BuildOrientation(Vector3 forward, Vector3 up)
    {
        forward = SafeNormalize(forward, Vector3.forward);
        up = SafeNormalize(up, Vector3.up);

        // Ensure orthonormal basis even if axes were not perpendicular
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 1e-6f)
        {
            Vector3 fallback = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            right = Vector3.Cross(fallback, forward);
        }
        right = SafeNormalize(right, Vector3.right);
        up = Vector3.Cross(forward, right).normalized;

        return Quaternion.LookRotation(forward, up);
    }

    static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        if (v.sqrMagnitude < 1e-6f)
        {
            if (fallback.sqrMagnitude < 1e-6f)
                fallback = Vector3.forward;
            return fallback.normalized;
        }
        return v.normalized;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CanoePaddleController : MonoBehaviour
{
    /* ─── Scene refs ─── */
    [Header("Scene References")]
    [SerializeField] Transform paddle;
    [SerializeField] Transform starboardPivot;
    [SerializeField] Transform portPivot;
    [SerializeField] Transform bladeTip;          // optional
    [SerializeField] Transform playerVisual;      // avatar root for lean

    /* ─── Water & grip ─── */
    [Header("Water Settings")]
    [SerializeField] float waterLevel   = 0f;
    [SerializeField] float handleHeight = 0.25f;

    /* ─── Stroke tuning ─── */
    [Header("Stroke Tuning")]
    [SerializeField] float impulsePerMetre = 8000f;   // N·s per-m
    [SerializeField] float torqueFactor    = 1.0f;
    [SerializeField] float bladeLength     = 1.5f;
    [SerializeField] float submergePitch   = -45f;
    [Range(0,90)]   [SerializeField] float verticalDeadZone = 35f;

    /* ─── Lean tuning ─── */
    const float leanAngle = 8f;     // degrees avatar leans toward paddle
    const float leanLerp  = 6f;     // how quickly it leans

    /* ─── Internals ─── */
    Rigidbody   rb;
    Vector3     lastTip;
    InputAction click;
    bool        isLeftSide;         // updated every frame
    bool        prevLeftSide;       // track previous side to detect switches
    
    /* ─── Water Effects ─── */
    WaterEffectsManager waterEffects;
    bool prevBladeWet = false;
    

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0.1f;             // small linear drag so canoe coasts

        click = new InputAction(type: InputActionType.Button,
                                binding: "<Mouse>/leftButton");
        click.Enable();

        /* Ignore paddle ↔ hull collisions */
        if (paddle)
        {
            foreach (var pc in paddle.GetComponentsInChildren<Collider>())
                foreach (var cc in GetComponentsInChildren<Collider>())
                    if (pc != cc) Physics.IgnoreCollision(pc, cc);
        }
        
        /* Find water effects manager */
        waterEffects = FindFirstObjectByType<WaterEffectsManager>();
        if (waterEffects == null)
        {
            Debug.LogWarning("CanoePaddleController: No WaterEffectsManager found in scene!");
        }
        
    }

    /* ─── Aim & pitch ─── */
    void Update()
    {
        Vector2 centre = new(Screen.width * .5f, Screen.height * .5f);
        Vector2 dir2D  = Mouse.current.position.ReadValue() - centre;
        float   yawDeg = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg * -1f;

        isLeftSide = dir2D.x < 0f;
        Transform pivot   = isLeftSide ? portPivot : starboardPivot;
        Vector3   gripPos = pivot.position + Vector3.up * handleHeight;

        float pitch = Mathf.Lerp(0f, submergePitch, click.IsPressed() ? 1f : 0f);
        paddle.SetPositionAndRotation(
            gripPos,
            transform.rotation * Quaternion.Euler(pitch, yawDeg, 0f));
        paddle.localScale = Vector3.one;

        /* Smooth torso lean toward paddle side */
        if (playerVisual)
        {
            float targetLean = (isLeftSide ? -leanAngle : leanAngle);
            Vector3 e = playerVisual.localEulerAngles;
            float newZ = Mathf.LerpAngle(
                (e.z > 180 ? e.z - 360 : e.z), targetLean, leanLerp * Time.deltaTime);
            playerVisual.localRotation = Quaternion.Euler(e.x, e.y, newZ);
        }
    }

    /* ─── Stroke physics ─── */
    void FixedUpdate()
    {
        Vector3 tip = PaddleTip();

        // Detect side switch and reset lastTip to prevent snap impulse
        if (isLeftSide != prevLeftSide)
        {
            lastTip = tip;
            prevLeftSide = isLeftSide;
        }

        bool bladeWet = click.IsPressed() && tip.y <= waterLevel;

        if (bladeWet)
        {
            Vector3 delta = tip - lastTip;
            float   dist  = delta.magnitude;
            if (dist > 0.001f)
            {
                float angleFromHoriz =
                    Vector3.Angle(delta, Vector3.ProjectOnPlane(delta, Vector3.up));
                if (angleFromHoriz < verticalDeadZone)
                {
                    Vector3 impulse = -delta.normalized * impulsePerMetre * dist;
                    rb.AddForceAtPosition(impulse, tip, ForceMode.Impulse);

                    Vector3 torque =
                        Vector3.Cross(tip - rb.worldCenterOfMass, impulse) * torqueFactor;
                    rb.AddTorque(torque, ForceMode.Impulse);
                    
                    // Create water ripple effect based on paddle force
                    if (waterEffects != null)
                    {
                        float rippleIntensity = Mathf.Clamp01(dist * 2f); // Scale intensity by movement
                        waterEffects.OnWaterCollision(tip, rippleIntensity);
                    }
                    
                }
            }
        }
        
        // Detect paddle entering/leaving water for splash effects
        if (bladeWet != prevBladeWet && waterEffects != null)
        {
            if (bladeWet) // Paddle just entered water
            {
                waterEffects.OnWaterCollision(tip, 1.5f); // Stronger ripple for initial entry
            }
        }
        prevBladeWet = bladeWet;
        lastTip = tip;
    }

    Vector3 PaddleTip() =>
        bladeTip ? bladeTip.position
                 : paddle.position + paddle.forward * bladeLength;

    /* Utility for other scripts (hands) */
    public bool PaddleLeftSide => isLeftSide;
}
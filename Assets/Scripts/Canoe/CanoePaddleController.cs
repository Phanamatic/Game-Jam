using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CanoePaddleController : MonoBehaviour
{
    [Header("Transforms")]
    [SerializeField] Transform paddle;              // full paddle mesh
    [SerializeField] Transform starboardPivot;      // right-hand socket
    [SerializeField] Transform portPivot;           // left-hand socket
    [SerializeField] float handleHeight = 1.0f;     // keep grip above water

    [Header("Stroke Tuning")]
    [SerializeField] float strokeForce  = 500f;     // impulse per metre
    [SerializeField] float torqueFactor = 0.8f;     // yaw power
    [SerializeField] float bladeLength  = 1.1f;     // tip offset from pivot
    [SerializeField] float submergePitch = -45f;    // degrees blade pitches down

    Rigidbody rb;
    Vector3   lastTip;
    InputAction click;

    void Awake()
    {
        rb    = GetComponent<Rigidbody>();
        click = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        click.Enable();                                                // Input System :contentReference[oaicite:2]{index=2}
    }

    void Update()
    {
        // ----- 1. Aim around screen centre -----
        Vector2 centre   = new(Screen.width * .5f, Screen.height * .5f);
        Vector2 dir2D    = Mouse.current.position.ReadValue() - centre;
        float   yaw      = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg * -1f;

        // pivot & height
        Transform pivot  = dir2D.x >= 0 ? starboardPivot : portPivot;
        Vector3   pivotPos = pivot.position + Vector3.up * handleHeight;

        // ----- 2. Pitch blade into the water while button held -----
        float   tPress = click.IsPressed() ? 1f : 0f;                       // 0->1 instantly; swap for HoldInteraction for a soft ease :contentReference[oaicite:3]{index=3}
        float   pitch  = Mathf.Lerp(0f, submergePitch, tPress);             // 0° (air) → −45° (water)
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);                  // pitch then yaw

        // ----- 3. Apply transform (no scaling) -----
        paddle.SetPositionAndRotation(pivotPos, rot);
        paddle.localScale = Vector3.one;                                    // hard-lock scale every frame :contentReference[oaicite:4]{index=4}
    }

    void FixedUpdate()
    {
        Vector3 tip = PaddleTip();                                          // world-space tip

        bool bladeWet = click.IsPressed() && paddle.localRotation.eulerAngles.x > 180f; // ≈ pitched down
        // Ray-cast option (uncomment if you use a water layer):
        // bladeWet &= Physics.Raycast(tip, Vector3.down, 0.2f, LayerMask.GetMask("Water"));

        if (bladeWet)
        {
            Vector3 delta = tip - lastTip;
            Vector3 local = transform.InverseTransformDirection(delta);
            float   back  = -local.z;                                       // +ve = pulling

            if (back > 0f)
            {
                Vector3 dir   = -transform.forward;
                Vector3 force = dir * strokeForce * back;
                rb.AddForceAtPosition(force, tip, ForceMode.Force);        // AddForceAtPosition :contentReference[oaicite:5]{index=5}

                float side = Mathf.Sign(local.x);
                rb.AddTorque(transform.up * -side * back * strokeForce * torqueFactor);
            }
        }

        lastTip = tip;
    }

    Vector3 PaddleTip() => paddle.position + paddle.forward * bladeLength;
}

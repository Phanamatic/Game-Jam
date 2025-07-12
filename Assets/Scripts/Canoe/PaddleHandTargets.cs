/*  PaddleHandTargets.cs – robust hand-placement helper
 *  15 Jul 2025 – drop-in
 *
 *  Key improvements
 *  ─────────────────
 *  • Explicit reference to the controller (but still auto-finds if left null)
 *  • Configurable shaft and side axes so you can match any paddle model
 *  • Uses LerpUnclamped for snappier side swaps (no “stuck” weight)
 *  • Early-out guard if either hand target is missing (avoids null refs)
 *  • Optional bob toggle
 */

using UnityEngine;

public class PaddleHandTargets : MonoBehaviour
{
    /* ───────── References ───────── */
    [Header("Required references")]
    [Tooltip("Assign your CanoePaddleController here. " +
             "If left empty, the script will search in parents at runtime.")]
    [SerializeField] CanoePaddleController paddleCtrl;

    [SerializeField] Transform leftHandTarget;
    [SerializeField] Transform rightHandTarget;

    /* ───────── Geometry (local units) ───────── */
    [Header("Grip geometry")]
    [SerializeField] float gripDistance   = 0.30f;   // metres from pivot to centre grip
    [SerializeField] float handSeparation = 0.40f;   // distance between hands
    [SerializeField] float sideSpread     = 0.00f;   // side-to-side offset (rarely needed)

    [Tooltip("Axis pointing from paddle pivot towards the *handle*." +
             "Default = –Z (so +Z in the model points at the blade).")]
    [SerializeField] Vector3 shaftAxis = Vector3.back;     // (= –Z)

    [Tooltip("Axis that points to the *right-hand* side of the shaft. " +
             "Default = +X. Swap sign if your model is mirrored.")]
    [SerializeField] Vector3 sideAxis  = Vector3.right;    // (= +X)

    /* ───────── Side-swap smoothing ───────── */
    [Header("Swap smoothing")]
    [SerializeField] float swapLerp = 12f;          // higher = quicker snap

    /* ───────── Cosmetic bob ───────── */
    [Header("Bob (optional)")]
    [SerializeField] bool   enableBob  = true;
    [SerializeField] float  bobAmount  = 0.015f;    // metres
    [SerializeField] float  bobSpeed   = 2.5f;      // Hz

    /* ───────── Internals ───────── */
    float sideWeight;   // 0 = right hand forward, 1 = left hand forward

    void Awake()
    {
        // Find controller automatically if not assigned.
        if (!paddleCtrl)
            paddleCtrl = GetComponentInParent<CanoePaddleController>();
    }

    void LateUpdate()
    {
        if (!paddleCtrl || !leftHandTarget || !rightHandTarget)
            return;  // nothing to do

        /* 0 ↔ 1 blend based on current paddle side */
        float targetWeight = paddleCtrl.PaddleLeftSide ? 1f : 0f;
        sideWeight = Mathf.LerpUnclamped(sideWeight, targetWeight, swapLerp * Time.deltaTime);

        /* Build local-space basis vectors */
        Vector3 shaftDir = transform.TransformDirection(shaftAxis).normalized;   // toward handle
        Vector3 sideDir  = transform.TransformDirection(sideAxis ).normalized;   // right side

        /* Hand offsets along the shaft */
        float halfSep = handSeparation * 0.5f;
        float leftOffset  = Mathf.Lerp(-halfSep, halfSep, sideWeight);  // back→front
        float rightOffset = Mathf.Lerp( halfSep, -halfSep, sideWeight); // front→back

        /* Base grip position (centre of shaft) */
        Vector3 basePos = transform.position + shaftDir * gripDistance;

        /* Optional bob */
        Vector3 bobVec = Vector3.zero;
        if (enableBob && bobAmount > 0f)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobAmount;
            bobVec = transform.up * bob;
        }

        /* Final target positions */
        leftHandTarget .position = basePos + shaftDir * leftOffset  + sideDir *  sideSpread + bobVec;
        rightHandTarget.position = basePos + shaftDir * rightOffset - sideDir *  sideSpread + bobVec;

        /* Match paddle rotation so wrist IK can align easily */
        Quaternion rot = transform.rotation;
        leftHandTarget .rotation = rot;
        rightHandTarget.rotation = rot;
    }
}

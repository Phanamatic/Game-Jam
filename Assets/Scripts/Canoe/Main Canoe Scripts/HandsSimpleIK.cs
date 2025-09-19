using UnityEngine;

/*
 * HandsSimpleIK
 * - Uses explicit grip markers to avoid axis assumptions.
 * - Swaps lead/lag by side of stroke.
 * - Smooth, non-jerky placement.
 *
 * Create two empty children of the shaft:
 *   GripRear  (closer to handle)
 *   GripFront (closer to blade root)
 * Assign them below.
 */

[DefaultExecutionOrder(20)]
public class HandsSimpleIK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform canoe;
    [SerializeField] Transform gripRear;    // marker
    [SerializeField] Transform gripFront;   // marker
    [SerializeField] Transform leftHand;    // visuals
    [SerializeField] Transform rightHand;

    [Header("Offsets")]
    [SerializeField] float wrapOffset = 0.03f; // small offset around shaft
    [SerializeField] float liftOffset = 0.02f; // lift off mesh

    [Header("Smoothing")]
    [SerializeField] float posLerp = 24f;
    [SerializeField] float rotLerp = 24f;

    void LateUpdate()
    {
        if (!gripRear || !gripFront || !leftHand || !rightHand) return;

        // Build local frame using the grip segment
        Vector3 fwd = (gripFront.position - gripRear.position);
        float len = fwd.magnitude; if (len < 1e-4f) return;
        fwd /= len;

        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, fwd).normalized;
        if (right.sqrMagnitude < 1e-6f) { right = Vector3.right; up = Vector3.Cross(fwd, right).normalized; }
        else up = Vector3.Cross(fwd, right).normalized;

        // Offsets so palms wrap the shaft from opposite sides
        Vector3 rearPos  = gripRear.position  + up * liftOffset - right * wrapOffset;
        Vector3 frontPos = gripFront.position + up * liftOffset + right * wrapOffset;

        // Determine which side of canoe the paddle is on
        bool isRightSide = true;
        if (canoe)
        {
            Vector3 toPaddle = (gripFront.position + gripRear.position) * 0.5f - canoe.position;
            isRightSide = Vector3.SignedAngle(canoe.forward, new Vector3(toPaddle.x, 0f, toPaddle.z), Vector3.up) > 0f;
        }

        // Palms roughly face inward to the shaft (look -right, up)
        Quaternion handRot = Quaternion.LookRotation(-right, up);

        Transform lead = isRightSide ? rightHand : leftHand; // front
        Transform lag  = isRightSide ? leftHand  : rightHand; // rear

        SmoothTo(lead, frontPos, handRot, posLerp, rotLerp);
        SmoothTo(lag,  rearPos,  handRot, posLerp, rotLerp);
    }

    static void SmoothTo(Transform t, Vector3 p, Quaternion r, float pl, float rl)
    {
        t.position = Vector3.Lerp(t.position, p, 1f - Mathf.Exp(-pl * Time.deltaTime));
        t.rotation = Quaternion.Slerp(t.rotation, r, 1f - Mathf.Exp(-rl * Time.deltaTime));
    }
}

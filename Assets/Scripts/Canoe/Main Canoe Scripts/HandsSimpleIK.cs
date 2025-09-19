using UnityEngine;

/*
 * HandsSimpleIK
 *  - Drives visual Left/Right Hand transforms to grip two points on the paddle shaft.
 *  - Chooses lead/lag hand based on which side of the canoe the stroke is on.
 *  - No bones or Animator required. Pure transform targets for visuals.
 *
 * Setup:
 *  - Add to the same object as PaddleSystem or another.
 *  - Assign paddleShaft (stick), shaftGripA/B as local 0..1 along shaft,
 *    and the visual hand transforms (Left Hand, Right Hand).
 *  - The script will place hands each frame. It does not add forces.
 */

[DefaultExecutionOrder(20)]
public class HandsSimpleIK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform canoe;        // For side detection
    [SerializeField] Transform paddleShaft;  // The stick mesh/object (with proper local axis)
    [SerializeField] Transform leftHand;     // Visual only
    [SerializeField] Transform rightHand;    // Visual only

    [Header("Grip placement along shaft")]
    [Tooltip("0 = shaft start (handle), 1 = shaft end (near blade root)")]
    [SerializeField, Range(0f, 1f)] float gripA = 0.25f; // rear/handle
    [SerializeField, Range(0f, 1f)] float gripB = 0.70f; // forward

    [Header("Offsets")]
    [Tooltip("Vertical offset to avoid penetrating the shaft visually.")]
    [SerializeField] float handUpOffset = 0.02f;
    [Tooltip("Slight wrap around the shaft")]
    [SerializeField] float aroundOffset = 0.03f;

    [Header("Smoothing")]
    [SerializeField] float posLerp = 24f;
    [SerializeField] float rotLerp = 24f;

    void LateUpdate()
    {
        if (!paddleShaft || !leftHand || !rightHand) return;

        // Compute shaft endpoints in world. Assume local +Z along shaft length.
        Vector3 shaftStart = paddleShaft.TransformPoint(new Vector3(0f, 0f, 0f));
        Vector3 shaftEnd   = paddleShaft.TransformPoint(new Vector3(0f, 0f, 1f)); // requires shaft length normalized to 1 in local Z
        Vector3 shaftDir   = (shaftEnd - shaftStart);
        float len = shaftDir.magnitude;
        if (len < 1e-4f) return;
        Vector3 shaftFwd = shaftDir / len;

        // Grip world positions
        Vector3 gripPosA = Vector3.Lerp(shaftStart, shaftEnd, gripA);
        Vector3 gripPosB = Vector3.Lerp(shaftStart, shaftEnd, gripB);

        // Define a local frame for hand orientation: forward along shaft, up roughly world up
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, shaftFwd).normalized;
        if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
        up = Vector3.Cross(shaftFwd, right).normalized;

        // Slight offsets so hands appear to wrap around
        gripPosA += up * handUpOffset - right * aroundOffset;
        gripPosB += up * handUpOffset + right * aroundOffset;

        // Determine side of paddle relative to canoe to swap lead/lag hands
        bool rightSide = true;
        if (canoe)
        {
            Vector3 toPaddle = paddleShaft.position - canoe.position;
            rightSide = Vector3.SignedAngle(canoe.forward, new Vector3(toPaddle.x, 0f, toPaddle.z), Vector3.up) > 0f;
        }

        // Build orientation so palms roughly face shaft: forward ~ -right to mimic grip twist
        Quaternion handRot = Quaternion.LookRotation(-right, up);

        // Assign with smoothing
        Transform lead = rightSide ? rightHand : leftHand; // forward grip
        Transform lag  = rightSide ? leftHand  : rightHand; // rear grip

        SmoothTo(lead, gripPosB, handRot, posLerp, rotLerp);
        SmoothTo(lag,  gripPosA, handRot, posLerp, rotLerp);
    }

    static void SmoothTo(Transform t, Vector3 pos, Quaternion rot, float pLerp, float rLerp)
    {
        t.position = Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-pLerp * Time.deltaTime));
        t.rotation = Quaternion.Slerp(t.rotation, rot, 1f - Mathf.Exp(-rLerp * Time.deltaTime));
    }
}

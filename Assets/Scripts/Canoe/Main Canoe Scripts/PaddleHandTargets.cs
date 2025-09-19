// PaddleHandTargets.cs
// Keeps as provided, adds minor guard and small recovery offset for natural motion.
using UnityEngine;

public class PaddleHandTargets : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanoePaddleController paddleCtrl;
    [SerializeField] Transform leftHandTarget;
    [SerializeField] Transform rightHandTarget;

    [Header("Grip geometry")]
    [SerializeField] float gripDistance = 0.30f;
    [SerializeField] float handSeparation = 0.40f;
    [SerializeField] float sideSpread = 0.00f;

    [SerializeField] Vector3 shaftAxis = Vector3.back;
    [SerializeField] Vector3 sideAxis = Vector3.right;

    [Header("Swap smoothing")]
    [SerializeField] float swapLerp = 12f;

    [Header("Recovery nuance")]
    [SerializeField] float desync = 0.06f; // seconds offset between hands on recovery
    [SerializeField] float bobAmount = 0.015f;
    [SerializeField] float bobSpeed = 2.5f;

    float sideWeight;
    float phase;

    void Awake()
    {
        if (!paddleCtrl) paddleCtrl = GetComponentInParent<CanoePaddleController>();
    }

    void LateUpdate()
    {
        if (!paddleCtrl || !leftHandTarget || !rightHandTarget) return;

        float target = paddleCtrl.PaddleLeftSide ? 1f : 0f;
        sideWeight = Mathf.LerpUnclamped(sideWeight, target, swapLerp * Time.deltaTime);

        Vector3 shaftDir = transform.TransformDirection(shaftAxis).normalized;
        Vector3 sideDir = transform.TransformDirection(sideAxis).normalized;

        float halfSep = handSeparation * 0.5f;
        float leftOffset = Mathf.Lerp(-halfSep, halfSep, sideWeight);
        float rightOffset = Mathf.Lerp(halfSep, -halfSep, sideWeight);

        Vector3 basePos = transform.position + shaftDir * gripDistance;

        // Subtle phase offset during recovery
        phase += Time.deltaTime * bobSpeed * Mathf.PI * 2f;
        Vector3 bob = transform.up * Mathf.Sin(phase) * bobAmount;
        Vector3 bobR = transform.up * Mathf.Sin(phase + desync * Mathf.PI * 2f) * bobAmount;

        leftHandTarget.position = basePos + shaftDir * leftOffset + sideDir * sideSpread + bob;
        rightHandTarget.position = basePos + shaftDir * rightOffset - sideDir * sideSpread + bobR;
        Quaternion rot = transform.rotation;
        leftHandTarget.rotation = rot;
        rightHandTarget.rotation = rot;
    }
}

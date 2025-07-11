using UnityEngine;

public class PaddleHandTargets : MonoBehaviour
{
    [SerializeField] Transform leftTarget, rightTarget;
    [SerializeField] float gripDistance = 0.4f;      // along shaft from pivot
    [SerializeField] float handSpread   = 0.18f;     // sideways offset

    void LateUpdate()                                // after paddle finished moving
    {
        Vector3 shaftDir = -transform.forward;       // toward handle
        Vector3 sideDir  = transform.right;

        leftTarget .position = transform.position + shaftDir * gripDistance + sideDir *  handSpread;
        rightTarget.position = transform.position + shaftDir * gripDistance - sideDir *  handSpread;

        leftTarget .rotation = transform.rotation;   // keep palms aligned
        rightTarget.rotation = transform.rotation;
    }
}

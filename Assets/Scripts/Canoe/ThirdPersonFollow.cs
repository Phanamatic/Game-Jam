using UnityEngine;

public class ThirdPersonFollow : MonoBehaviour
{
    [Tooltip("Transform to follow (canoe root).")]
    [SerializeField] Transform target;

    [Header("Offsets (local to target)")]
    [SerializeField] float distance = 4f;   // backward
    [SerializeField] float height   = 2f;   // upward

    [Header("Smoothing")]
    [SerializeField] float positionLerp = 10f;  // higher = snappier
    [SerializeField] float rotationLerp = 15f;

    void LateUpdate()                                   // run after all physics
    {
        if (!target) return;

        // 1. Desired camera position (behind + above, in target’s local space)
        Vector3 desired =
            target.position
          - target.forward * distance
          + Vector3.up * height;

        // 2. Smoothly interpolate position & look-at
        transform.position = Vector3.Lerp(
            transform.position, desired, positionLerp * Time.deltaTime);

        Quaternion look = Quaternion.LookRotation(
            target.position - transform.position, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, look, rotationLerp * Time.deltaTime);
    }
}

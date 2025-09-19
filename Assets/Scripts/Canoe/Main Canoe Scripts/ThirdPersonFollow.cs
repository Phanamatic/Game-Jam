// ThirdPersonFollow.cs
// Looser follow with split smoothing, velocity trail, camera collision, and speed-based FOV ease.
using UnityEngine;

public class ThirdPersonFollow : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Offsets")]
    [SerializeField] float distance = 4f;   // Base follow distance behind target forward
    [SerializeField] float height = 2f;     // Vertical offset above target

    [Header("Looseness & Damping")]
    [Tooltip("Global multiplier. Higher = slower response = looser follow.")]
    [SerializeField, Min(0.05f)] float looseness = 1.6f;
    [SerializeField] float horizSmooth = 0.15f; // Base smooth time for XZ (seconds)
    [SerializeField] float vertSmooth  = 0.30f; // Base smooth time for Y  (seconds)
    [SerializeField] float rotLerp     = 12f;   // Degrees per second style slerp factor

    [Header("Velocity Trail (adds elastic lag)")]
    [Tooltip("How much target velocity pulls the camera backward (horizontal only).")]
    [SerializeField] float velocityTrail = 0.35f;
    [Tooltip("Limits the extra backward lag from velocity.")]
    [SerializeField] float maxTrailMeters = 3.0f;

    [Header("Collision")]
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] float camRadius = 0.2f;

    [Header("FOV by speed (optional)")]
    [SerializeField] Camera cam;
    [SerializeField] float baseFov = 60f;
    [SerializeField] float maxFov = 72f;
    [SerializeField] float speedForMaxFov = 6f;
    [SerializeField] float fovLerp = 5f;

    // Internal smoothing state (separate XZ and Y to keep vertical snappier if desired)
    Vector3 velH, velV;

    void LateUpdate()
    {
        if (!target) return;

        // --- Desired rig point before damping ---
        Vector3 desired = target.position - target.forward * distance + Vector3.up * height;

        // Add velocity-based trailing for a looser feel (horizontal only).
        Vector3 targetVel = Vector3.zero;
        if (target.TryGetComponent<Rigidbody>(out var rb)) targetVel = rb.linearVelocity;
        Vector3 horizVel = Vector3.ProjectOnPlane(targetVel, Vector3.up);
        if (horizVel.sqrMagnitude > 1e-4f)
        {
            Vector3 trailOffset = -horizVel.normalized * Mathf.Min(horizVel.magnitude * velocityTrail, maxTrailMeters);
            desired += trailOffset;
        }

        // Camera collision: spherecast from target to desired to keep line-of-sight clear.
        Vector3 to = desired - target.position;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.0001f ? to / dist : Vector3.back;
        if (Physics.SphereCast(target.position, camRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            desired = target.position + dir * Mathf.Max(0f, hit.distance - 0.05f);

        // --- Split smoothing with global looseness multiplier ---
        float hSmooth = Mathf.Max(0.01f, horizSmooth * looseness);
        float vSmooth = Mathf.Max(0.01f,  vertSmooth  * looseness);

        Vector3 cur = transform.position;
        Vector3 smoothXZ = new Vector3(
            Mathf.SmoothDamp(cur.x, desired.x, ref velH.x, hSmooth),
            cur.y,
            Mathf.SmoothDamp(cur.z, desired.z, ref velH.z, hSmooth));
        float smoothY = Mathf.SmoothDamp(cur.y, desired.y, ref velV.y, vSmooth);
        transform.position = new Vector3(smoothXZ.x, smoothY, smoothXZ.z);

        // Rotation: scale responsiveness down by looseness to feel lazier.
        float rotT = Mathf.Clamp01((rotLerp / Mathf.Max(0.01f, looseness)) * Time.deltaTime);
        Quaternion look = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotT);

        // FOV ease by target rigidbody speed (uses rb.velocity for classic Rigidbody)
        if (cam && target.TryGetComponent<Rigidbody>(out rb))
        {
            float t = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.1f, speedForMaxFov));
            float fov = Mathf.Lerp(baseFov, maxFov, t);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, fovLerp * Time.deltaTime);
        }
    }
}

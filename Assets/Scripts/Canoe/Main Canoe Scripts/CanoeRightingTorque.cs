using UnityEngine;

/*
 * CanoeRightingTorque
 * - Adds a gentle upright torque and lowers center of mass for stability.
 * - Uses torque, not rotation writes. Won’t fight legitimate capsizes if strength is moderate.
 */

[RequireComponent(typeof(Rigidbody))]
public class CanoeRightingTorque : MonoBehaviour
{
    [Header("Center of Mass")]
    [Tooltip("Local-space offset to lower COM (e.g., y = -0.15).")]
    [SerializeField] Vector3 comOffset = new Vector3(0f, -0.15f, 0f);

    [Header("Upright")]
    [SerializeField] float uprightStrength = 8.0f;   // proportional
    [SerializeField] float uprightDamping  = 1.6f;   // derivative
    [SerializeField] float maxTorque       = 600f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += comOffset;
    }

    void FixedUpdate()
    {
        Vector3 up = transform.up;
        Vector3 worldUp = Vector3.up;

        // Torque to rotate 'up' toward worldUp
        Vector3 axis = Vector3.Cross(up, worldUp);
        float angle = Mathf.Asin(Mathf.Clamp(axis.magnitude, 0f, 1f));
        if (angle > 1e-3f)
        {
            axis.Normalize();
            Vector3 torqueP = axis * (angle * uprightStrength);

            // Damping on current angular velocity about that axis
            float angVelAlong = Vector3.Dot(rb.angularVelocity, axis);
            Vector3 torqueD = -axis * (angVelAlong * uprightDamping);

            Vector3 T = torqueP + torqueD;
            if (T.magnitude > maxTorque) T = T.normalized * maxTorque;
            rb.AddTorque(T, ForceMode.Force);
        }
    }
}

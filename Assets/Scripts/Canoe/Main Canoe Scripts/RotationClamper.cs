using UnityEngine;

/* RotationClamper
 * Allows enough roll for balance + capsizes while keeping pitch modest.
 */
public class RotationClamper : MonoBehaviour
{
    [SerializeField] float maxPitchX = 8f;   // degrees
    [SerializeField] float maxRollZ  = 70f;  // wider to let balancing breathe

    void LateUpdate()
    {
        Vector3 e = transform.rotation.eulerAngles;
        float clampedX = Clamp(e.x, -maxPitchX, maxPitchX);
        float clampedZ = Clamp(e.z, -maxRollZ,  maxRollZ);
        transform.rotation = Quaternion.Euler(clampedX, e.y, clampedZ);
    }

    float Clamp(float angle, float min, float max)
    {
        if (angle > 180f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}

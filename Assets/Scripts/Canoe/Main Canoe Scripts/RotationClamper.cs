using UnityEngine;

public class RotationClamper : MonoBehaviour
{
    [SerializeField] float maxPitchX = 8f;  // degrees
    [SerializeField] float maxRollZ  = 4f;  // degrees

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

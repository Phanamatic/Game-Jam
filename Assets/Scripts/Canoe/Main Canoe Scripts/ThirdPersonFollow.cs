using UnityEngine;

public class ThirdPersonFollow : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Offsets")]
    [SerializeField] float distance = 4f;
    [SerializeField] float height   = 2f;

    [Header("Damping")]
    [SerializeField] float horizSmooth = 0.15f;     // larger = looser follow
    [SerializeField] float vertSmooth  = 0.3f;      // damp vertical bob
    [SerializeField] float rotLerp     = 12f;

    Vector3 velH, velV;     // SmoothDamp velocity caches

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position
                        - target.forward * distance
                        + Vector3.up * height;

        /* Split smoothing: xz separately from y */
        Vector3 curPos   = transform.position;
        Vector3 smoothXZ = new Vector3(
            Mathf.SmoothDamp(curPos.x, desired.x, ref velH.x, horizSmooth),
            curPos.y,
            Mathf.SmoothDamp(curPos.z, desired.z, ref velH.z, horizSmooth));

        float smoothY = Mathf.SmoothDamp(curPos.y, desired.y, ref velV.y, vertSmooth);

        transform.position = new Vector3(smoothXZ.x, smoothY, smoothXZ.z);

        Quaternion look = Quaternion.LookRotation(
            target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, look, rotLerp * Time.deltaTime);
    }
}

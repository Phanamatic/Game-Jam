using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterCollisionDetector : MonoBehaviour
{
    [Header("Ripples")]
    [SerializeField] float rippleIntensity = 0.7f;
    [SerializeField] float minVelocityForRipple = 0.6f;
    [SerializeField] float rippleCooldown = 0.25f;

    WaterEffectsManager waterEffects;
    Rigidbody rb;
    SimpleWaterRuntime water;
    float lastRippleTime;
    bool wasInWater;

    void Start()
    {
        waterEffects = Object.FindFirstObjectByType<WaterEffectsManager>();
        rb = GetComponent<Rigidbody>();
        water = SimpleWaterRuntime.Instance;
    }

    void FixedUpdate()
    {
        if (!rb || !water || !waterEffects) return;

        float surfaceY = water.HeightAt(transform.position);
        bool isInWater = transform.position.y <= surfaceY;
        float t = Time.time;

        if (isInWater && !wasInWater && t - lastRippleTime > rippleCooldown)
        {
            float v = rb.linearVelocity.magnitude;
            if (v > minVelocityForRipple)
            {
                float intensity = Mathf.Clamp01(v / 5f) * rippleIntensity;
                Vector3 p = new Vector3(transform.position.x, surfaceY, transform.position.z);
                waterEffects.OnWaterCollision(p, intensity);
                lastRippleTime = t;
            }
        }

        if (isInWater && rb.linearVelocity.magnitude > minVelocityForRipple && t - lastRippleTime > rippleCooldown * 2f)
        {
            var col = GetComponent<Collider>();
            Vector3 bow = col.bounds.center + transform.forward * col.bounds.extents.z * 0.8f;
            bow.y = water.HeightAt(bow);
            float intensity = Mathf.Clamp01(rb.linearVelocity.magnitude / 8f) * rippleIntensity * 0.6f;
            waterEffects.OnWaterCollision(bow, intensity);
            lastRippleTime = t;
        }

        wasInWater = isInWater;
    }
}

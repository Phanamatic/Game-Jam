using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterCollisionDetector : MonoBehaviour
{
    [Header("Water Collision Settings")]
    [SerializeField] private float waterLevel = 0f;
    [SerializeField] private float rippleIntensity = 0.8f;
    [SerializeField] private float minVelocityForRipple = 0.5f;
    [SerializeField] private float rippleCooldown = 0.2f;
    
    private WaterEffectsManager waterEffects;
    private Rigidbody rb;
    private float lastRippleTime = 0f;
    private bool wasInWater = false;
    
    void Start()
    {
        waterEffects = FindFirstObjectByType<WaterEffectsManager>();
        rb = GetComponent<Rigidbody>();
        
        if (waterEffects == null)
        {
            Debug.LogWarning($"WaterCollisionDetector on {gameObject.name}: No WaterEffectsManager found!");
        }
        
        // Set water level from water effects manager if available
        if (waterEffects != null)
        {
            waterEffects.SetWaterLevel(waterLevel);
        }
    }
    
    void FixedUpdate()
    {
        CheckWaterCollision();
    }
    
    void CheckWaterCollision()
    {
        if (waterEffects == null || rb == null) return;
        
        bool isInWater = transform.position.y <= waterLevel;
        float currentTime = Time.time;
        
        // Check if hull just entered water
        if (isInWater && !wasInWater)
        {
            if (currentTime - lastRippleTime > rippleCooldown)
            {
                Vector3 impactPoint = GetClosestPointToWaterSurface();
                float velocityMagnitude = rb.linearVelocity.magnitude;
                
                if (velocityMagnitude > minVelocityForRipple)
                {
                    float intensity = Mathf.Clamp01(velocityMagnitude / 5f) * rippleIntensity;
                    waterEffects.OnWaterCollision(impactPoint, intensity);
                    lastRippleTime = currentTime;
                }
            }
        }
        
        // Create continuous ripples while moving in water
        if (isInWater && rb.linearVelocity.magnitude > minVelocityForRipple)
        {
            if (currentTime - lastRippleTime > rippleCooldown * 2f) // Less frequent for continuous movement
            {
                Vector3 bowPosition = GetBowPosition();
                float intensity = Mathf.Clamp01(rb.linearVelocity.magnitude / 8f) * rippleIntensity * 0.6f;
                waterEffects.OnWaterCollision(bowPosition, intensity);
                lastRippleTime = currentTime;
            }
        }
        
        wasInWater = isInWater;
    }
    
    Vector3 GetClosestPointToWaterSurface()
    {
        Vector3 pos = transform.position;
        return new Vector3(pos.x, waterLevel, pos.z);
    }
    
    Vector3 GetBowPosition()
    {
        // Estimate bow position (front of the canoe) based on forward direction
        Vector3 bowOffset = transform.forward * GetComponent<Collider>().bounds.size.z * 0.4f;
        Vector3 bowPos = transform.position + bowOffset;
        return new Vector3(bowPos.x, waterLevel, bowPos.z);
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw water level
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position + Vector3.up * waterLevel, new Vector3(5f, 0.1f, 5f));
        
        // Draw bow position
        Gizmos.color = Color.red;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(GetBowPosition(), 0.2f);
        }
    }
}
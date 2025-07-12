using UnityEngine;
using System.Collections.Generic;

public class WaterEffectsManager : MonoBehaviour
{
    [Header("Water Material")]
    [SerializeField] private Material waterMaterial;
    
    [Header("Foam Settings")]
    [SerializeField] private float foamDepthFade = 2f;
    [SerializeField] private float foamIntensity = 1f;
    [SerializeField] private Color foamColor = Color.white;
    
    [Header("Ripple Settings")]
    [SerializeField] private float rippleStrength = 0.5f;
    [SerializeField] private float rippleFadeTime = 3f;
    [SerializeField] private float rippleRadius = 2f;
    [SerializeField] private int maxRipples = 5;
    
    [Header("Collision Detection")]
    [SerializeField] private LayerMask waterLayer = -1;
    [SerializeField] private float waterLevel = 0f;
    
    // Ripple data structure
    private struct RippleData
    {
        public Vector3 position;
        public float startTime;
        public float strength;
        public bool active;
    }
    
    private RippleData[] ripples;
    private int currentRippleIndex = 0;
    
    // Shader property IDs for performance
    private int foamDepthFadeID;
    private int foamIntensityID;
    private int foamColorID;
    private int ripplePositionsID;
    private int rippleDataID;
    private int rippleCountID;
    
    void Start()
    {
        InitializeShaderProperties();
        InitializeRipples();
        
        if (waterMaterial == null)
        {
            Debug.LogWarning("WaterEffectsManager: No water material assigned!");
        }
    }
    
    void InitializeShaderProperties()
    {
        foamDepthFadeID = Shader.PropertyToID("_FoamDepthFade");
        foamIntensityID = Shader.PropertyToID("_FoamIntensity");
        foamColorID = Shader.PropertyToID("_FoamColor");
        ripplePositionsID = Shader.PropertyToID("_RipplePositions");
        rippleDataID = Shader.PropertyToID("_RippleData");
        rippleCountID = Shader.PropertyToID("_RippleCount");
    }
    
    void InitializeRipples()
    {
        ripples = new RippleData[maxRipples];
        for (int i = 0; i < maxRipples; i++)
        {
            ripples[i] = new RippleData
            {
                position = Vector3.zero,
                startTime = 0f,
                strength = 0f,
                active = false
            };
        }
    }
    
    void Update()
    {
        UpdateRipples();
        UpdateWaterMaterial();
    }
    
    void UpdateRipples()
    {
        float currentTime = Time.time;
        
        for (int i = 0; i < maxRipples; i++)
        {
            if (ripples[i].active)
            {
                float elapsed = currentTime - ripples[i].startTime;
                if (elapsed >= rippleFadeTime)
                {
                    ripples[i].active = false;
                }
            }
        }
    }
    
    void UpdateWaterMaterial()
    {
        if (waterMaterial == null) return;
        
        // Update foam properties
        waterMaterial.SetFloat(foamDepthFadeID, foamDepthFade);
        waterMaterial.SetFloat(foamIntensityID, foamIntensity);
        waterMaterial.SetColor(foamColorID, foamColor);
        
        // Update ripple data
        Vector4[] ripplePositions = new Vector4[maxRipples];
        Vector4[] rippleData = new Vector4[maxRipples];
        int activeRippleCount = 0;
        
        float currentTime = Time.time;
        
        for (int i = 0; i < maxRipples; i++)
        {
            if (ripples[i].active)
            {
                float elapsed = currentTime - ripples[i].startTime;
                float normalizedTime = elapsed / rippleFadeTime;
                float fadeMultiplier = 1f - normalizedTime;
                
                ripplePositions[i] = new Vector4(
                    ripples[i].position.x,
                    ripples[i].position.z,
                    rippleRadius * normalizedTime,
                    ripples[i].strength * fadeMultiplier
                );
                
                rippleData[i] = new Vector4(
                    elapsed,
                    rippleFadeTime,
                    ripples[i].strength,
                    1f
                );
                
                activeRippleCount++;
            }
            else
            {
                ripplePositions[i] = Vector4.zero;
                rippleData[i] = Vector4.zero;
            }
        }
        
        waterMaterial.SetVectorArray(ripplePositionsID, ripplePositions);
        waterMaterial.SetVectorArray(rippleDataID, rippleData);
        waterMaterial.SetInt(rippleCountID, activeRippleCount);
    }
    
    public void CreateRipple(Vector3 worldPosition, float strength = 1f)
    {
        // Only create ripple if position is near water level
        if (Mathf.Abs(worldPosition.y - waterLevel) > 1f) return;
        
        // Project position to water surface
        Vector3 waterSurfacePos = new Vector3(worldPosition.x, waterLevel, worldPosition.z);
        
        // Find next available ripple slot or override oldest
        int targetIndex = -1;
        float oldestTime = float.MaxValue;
        
        for (int i = 0; i < maxRipples; i++)
        {
            if (!ripples[i].active)
            {
                targetIndex = i;
                break;
            }
            else if (ripples[i].startTime < oldestTime)
            {
                oldestTime = ripples[i].startTime;
                targetIndex = i;
            }
        }
        
        if (targetIndex >= 0)
        {
            ripples[targetIndex] = new RippleData
            {
                position = waterSurfacePos,
                startTime = Time.time,
                strength = strength * rippleStrength,
                active = true
            };
        }
    }
    
    // Public method for external scripts to trigger ripples
    public void OnWaterCollision(Vector3 position, float intensity = 1f)
    {
        CreateRipple(position, intensity);
    }
    
    // Method to update water level (useful if water level changes dynamically)
    public void SetWaterLevel(float newWaterLevel)
    {
        waterLevel = newWaterLevel;
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize active ripples in scene view
        Gizmos.color = Color.cyan;
        for (int i = 0; i < maxRipples && ripples != null; i++)
        {
            if (ripples[i].active)
            {
                float elapsed = Time.time - ripples[i].startTime;
                float normalizedTime = elapsed / rippleFadeTime;
                float currentRadius = rippleRadius * normalizedTime;
                
                Gizmos.DrawWireSphere(ripples[i].position, currentRadius);
            }
        }
        
        // Draw water level
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position + Vector3.up * waterLevel, new Vector3(10f, 0.1f, 10f));
    }
}
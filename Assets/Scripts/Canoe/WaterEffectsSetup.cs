using UnityEngine;

[System.Serializable]
public class WaterEffectsSetup : MonoBehaviour
{
    [Header("Setup Instructions")]
    [TextArea(10, 15)]
    [SerializeField] private string setupInstructions = @"WATER EFFECTS SETUP GUIDE:

1. Add WaterEffectsManager to any GameObject in the scene
   - Assign your CartoonWater material to the 'Water Material' field
   - Adjust foam and ripple settings as desired

2. The CanoePaddleController is already updated to work with water effects
   - It will automatically find the WaterEffectsManager
   - Creates ripples when paddling

3. Add WaterCollisionDetector to your canoe hull GameObject
   - Adjust water level to match your scene
   - This creates ripples when the canoe moves through water

4. Your CartoonWater material now supports:
   - _FoamDepthFade: Controls how foam fades with depth
   - _FoamIntensity: Overall foam strength  
   - _FoamColor: Color of the foam
   - _RippleCount: Number of active ripples (set by script)
   - Dynamic ripple positions and data arrays

5. Shader Graph Integration:
   - The material properties are updated automatically
   - You may need to modify your shader graph to use the new properties
   - Add depth-based foam using scene depth and camera depth
   - Add ripple displacement using the ripple position arrays

TROUBLESHOOTING:
- If no ripples appear, check console for WaterEffectsManager warnings
- Ensure water level matches your actual water surface
- Ripples are visible in scene view gizmos when WaterEffectsManager is selected";

    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private Material cartoonWaterMaterial;
    [SerializeField] private Transform canoeTransform;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            AutoSetup();
        }
    }
    
    [ContextMenu("Auto Setup Water Effects")]
    public void AutoSetup()
    {
        // Find or create WaterEffectsManager
        WaterEffectsManager waterManager = FindFirstObjectByType<WaterEffectsManager>();
        if (waterManager == null)
        {
            GameObject waterManagerGO = new GameObject("WaterEffectsManager");
            waterManager = waterManagerGO.AddComponent<WaterEffectsManager>();
            Debug.Log("Created WaterEffectsManager");
        }
        
        // Assign material if provided
        if (cartoonWaterMaterial != null)
        {
            // Use reflection to set the material since WaterEffectsManager fields are private
            var materialField = typeof(WaterEffectsManager).GetField("waterMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (materialField != null)
            {
                materialField.SetValue(waterManager, cartoonWaterMaterial);
                Debug.Log("Assigned CartoonWater material to WaterEffectsManager");
            }
        }
        
        // Add WaterCollisionDetector to canoe if provided
        if (canoeTransform != null)
        {
            WaterCollisionDetector detector = canoeTransform.GetComponent<WaterCollisionDetector>();
            if (detector == null)
            {
                detector = canoeTransform.gameObject.AddComponent<WaterCollisionDetector>();
                Debug.Log("Added WaterCollisionDetector to canoe");
            }
        }
        
        Debug.Log("Water effects setup complete! Check the setup instructions above for shader graph integration.");
    }
    
    void OnValidate()
    {
        // Update instructions in inspector
        if (setupInstructions.Length == 0)
        {
            setupInstructions = "Setup instructions will appear here...";
        }
    }
}
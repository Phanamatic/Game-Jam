using UnityEngine;

[System.Serializable]
public class ShaderGraphUpdateGuide : MonoBehaviour
{
    [Header("Shader Graph Update Instructions")]
    [TextArea(20, 30)]
    [SerializeField] private string instructions = @"CARTOONWATER SHADER GRAPH UPDATE GUIDE

=== ADDING FOAM EFFECTS ===

1. OPEN SHADER GRAPH:
   - Double-click CartoonWater.shadergraph to open in Shader Graph editor

2. ADD FOAM PROPERTIES:
   Right-click in empty space → Create Node → Property
   - Add Property: 'FoamColor' (Color) - Set default to white
   - Add Property: 'FoamDepthFade' (Float) - Set default to 2.0
   - Add Property: 'FoamIntensity' (Float) - Set default to 1.0

3. ADD DEPTH SAMPLING NODES:
   - Add Node: 'Scene Depth' (Input → Scene → Scene Depth)
   - Add Node: 'Screen Position' (Input → Geometry → Screen Position)
   - Set Screen Position mode to 'Raw'

4. CREATE FOAM CALCULATION:
   - Add Node: 'Subtract' 
     * Connect Scene Depth to input A
     * Connect Screen Position.W to input B (this gives fragment depth)
   
   - Add Node: 'Divide'
     * Connect Subtract output to input A
     * Connect FoamDepthFade property to input B
   
   - Add Node: 'Saturate'
     * Connect Divide output to input
   
   - Add Node: 'One Minus'
     * Connect Saturate output to input (inverts so foam appears at edges)

5. APPLY FOAM COLOR:
   - Add Node: 'Multiply'
     * Connect One Minus output to input A
     * Connect FoamIntensity property to input B
   
   - Add Node: 'Multiply' (second one)
     * Connect previous Multiply output to input A
     * Connect FoamColor property to input B

6. BLEND WITH EXISTING COLOR:
   - Find the existing Multiply node that feeds into BaseColor
   - Add Node: 'Lerp'
     * Connect existing  color (current BaseColor input) to input A
     * Connect foam color result to input B
     * Connect foam intensity to input T
   - Connect Lerp output to BaseColor block

=== ADDING DYNAMIC RIPPLE DISPLACEMENT ===

7. ADD RIPPLE PROPERTIES:
   - Add Property: 'RipplePositions' (Vector4 Array) - For ripple centers
   - Add Property: 'RippleData' (Vector4 Array) - For ripple timing/intensity
   - Add Property: 'RippleCount' (Integer) - Number of active ripples

8. CREATE RIPPLE DISPLACEMENT:
   - Add Node: 'Position' (Input → Geometry → Position)
   - Set to 'World' space
   
   - Add Node: 'Custom Function'
     * Set Type to 'File'
     * Create RippleDisplacement.hlsl file (see below)
     * Inputs: Position (Vector3), RipplePositions (Vector4 Array), RippleData (Vector4 Array), RippleCount (Integer)
     * Output: Displacement (Vector3)

9. APPLY DISPLACEMENT:
   - Add Node: 'Add'
     * Connect existing Position input to input A
     * Connect RippleDisplacement output to input B
   - Connect Add output to Position block

=== CUSTOM FUNCTION FILE ===
Create: Assets/Shaders/RippleDisplacement.hlsl

void RippleDisplacement_float(float3 WorldPos, float4 RipplePositions[5], float4 RippleData[5], int RippleCount, out float3 Displacement)
{
    Displacement = float3(0, 0, 0);
    
    for(int i = 0; i < min(RippleCount, 5); i++)
    {
        float2 rippleCenter = RipplePositions[i].xy;
        float rippleRadius = RipplePositions[i].z;
        float rippleStrength = RipplePositions[i].w;
        
        float distance = length(WorldPos.xz - rippleCenter);
        float normalizedDist = saturate(distance / rippleRadius);
        
        float ripple = sin(normalizedDist * 6.28318) * (1.0 - normalizedDist);
        float strength = rippleStrength * ripple;
        
        Displacement.y += strength * 0.1; // Vertical displacement
    }
}

=== TROUBLESHOOTING ===

COMMON ISSUES:
- Foam not visible: Check depth texture is enabled in URP settings
- Ripples not working: Verify Custom Function file path is correct
- Performance issues: Reduce max ripple count in custom function

TESTING:
- Foam should appear where water meets geometry
- Ripples should appear when paddle touches water
- Effects should be subtle and enhance existing animation

FINAL STEPS:
1. Save shader graph (Ctrl+S)
2. Check material in Inspector - new properties should appear
3. Test in play mode with water interaction
4. Adjust property values for desired look

The foam effect uses depth differences to create foam where water meets solid objects. The ripple system adds dynamic vertex displacement based on collision points sent from the scripts.";

    [Header("Required Files")]
    [TextArea(5, 10)]
    [SerializeField] private string requiredFiles = @"FILES TO CREATE:

1. Assets/Shaders/ (folder)
2. Assets/Shaders/RippleDisplacement.hlsl (custom function file)

These files enable the dynamic ripple displacement effect in the shader graph.";

    [ContextMenu("Create Required Shader Files")]
    public void CreateShaderFiles()
    {
        CreateRippleDisplacementFile();
    }

    void CreateRippleDisplacementFile()
    {
        string shaderDir = "Assets/Shaders";
        string filePath = System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), shaderDir, "RippleDisplacement.hlsl");
        
        // Create directory if it doesn't exist
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
        
        string hlslContent = @"void RippleDisplacement_float(float3 WorldPos, float4 RipplePositions[5], float4 RippleData[5], int RippleCount, out float3 Displacement)
{
    Displacement = float3(0, 0, 0);
    
    for(int i = 0; i < min(RippleCount, 5); i++)
    {
        float2 rippleCenter = RipplePositions[i].xy;
        float rippleRadius = RipplePositions[i].z;
        float rippleStrength = RipplePositions[i].w;
        
        if(rippleStrength > 0.001)
        {
            float distance = length(WorldPos.xz - rippleCenter);
            float normalizedDist = saturate(distance / max(rippleRadius, 0.01));
            
            // Create expanding ring ripple
            float ripple = sin(normalizedDist * 12.566) * (1.0 - normalizedDist);
            float strength = rippleStrength * ripple;
            
            // Add both vertical and slight horizontal displacement
            Displacement.y += strength * 0.05; // Vertical displacement
            
            // Add radial displacement for more realistic ripples
            float2 direction = normalize(WorldPos.xz - rippleCenter);
            Displacement.xz += direction * strength * 0.02;
        }
    }
}";
        
        try
        {
            System.IO.File.WriteAllText(filePath, hlslContent);
            Debug.Log($"Created RippleDisplacement.hlsl at: {filePath}");
            
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create shader file: {e.Message}");
        }
    }
}
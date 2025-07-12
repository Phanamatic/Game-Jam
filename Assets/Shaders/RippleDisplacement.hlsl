void RippleDisplacement_float(float3 WorldPos, float4 RipplePositions[5], float4 RippleData[5], int RippleCount, out float3 Displacement)
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
            
            // Create expanding ring ripple with multiple waves
            float ripple = sin(normalizedDist * 12.566) * (1.0 - normalizedDist);
            float strength = rippleStrength * ripple;
            
            // Add both vertical and slight horizontal displacement
            Displacement.y += strength * 0.05; // Vertical displacement
            
            // Add radial displacement for more realistic ripples
            if(distance > 0.001)
            {
                float2 direction = normalize(WorldPos.xz - rippleCenter);
                Displacement.xz += direction * strength * 0.02;
            }
        }
    }
}
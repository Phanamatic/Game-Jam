using UnityEngine;

public class QuickShaderGuide : MonoBehaviour
{
    [Header("QUICK SHADER GRAPH SETUP")]
    [TextArea(15, 20)]
    [SerializeField] private string quickSteps = @"🌊 QUICK SHADER GRAPH FOAM SETUP 🌊

1. 📂 OPEN CartoonWater.shadergraph

2. ➕ ADD NEW PROPERTIES (Right-click → Property):
   • FoamColor (Color) = White
   • FoamDepthFade (Float) = 2.0  
   • FoamIntensity (Float) = 1.0

3. 🔍 ADD DEPTH NODES (Right-click → Create Node):
   • Scene Depth
   • Screen Position (set to Raw mode)

4. 🧮 CREATE FOAM MATH:
   Scene Depth → [Subtract] ← Screen Position.W
   Subtract → [Divide] ← FoamDepthFade
   Divide → [Saturate] → [One Minus]

5. 🎨 APPLY FOAM COLOR:
   One Minus → [Multiply] ← FoamIntensity
   Result → [Multiply] ← FoamColor

6. 🔗 CONNECT TO BASE COLOR:
   Find existing BaseColor connection
   Insert [Lerp] node:
   • A = Current BaseColor
   • B = Foam Color Result  
   • T = One Minus result
   Connect Lerp → BaseColor Block

7. 💾 SAVE (Ctrl+S)

✅ TEST: Foam should appear where water meets objects!

📋 NOTES:
• If foam not visible: Enable Depth Texture in URP settings
• Adjust FoamDepthFade for foam width
• Adjust FoamIntensity for foam opacity
• The ripple displacement requires the custom HLSL file";

    [Header("Property Names for Material")]
    [TextArea(8, 10)]
    [SerializeField] private string propertyNames = @"🏷️ PROPERTY NAMES FOR SCRIPTS:

When you add properties to shader graph, they create these material properties:
• FoamColor → _FoamColor
• FoamDepthFade → _FoamDepthFade  
• FoamIntensity → _FoamIntensity

Our scripts already look for these exact names, so make sure your property names match exactly (case-sensitive).

The WaterEffectsManager script will automatically update these values at runtime.";

    [ContextMenu("Log Current Material Properties")]
    public void LogMaterialProperties()
    {
        Material[] materials = FindObjectsOfType<Renderer>()
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.name.Contains("CartoonWater"))
            .Select(r => r.sharedMaterial)
            .Distinct()
            .ToArray();

        foreach (Material mat in materials)
        {
            Debug.Log($"Material: {mat.name}");
            Debug.Log($"Has _FoamColor: {mat.HasProperty("_FoamColor")}");
            Debug.Log($"Has _FoamDepthFade: {mat.HasProperty("_FoamDepthFade")}");
            Debug.Log($"Has _FoamIntensity: {mat.HasProperty("_FoamIntensity")}");
            Debug.Log("---");
        }
    }
}

// Extension method to make LINQ work
public static class LinqExtensions
{
    public static System.Collections.Generic.IEnumerable<T> Where<T>(this T[] array, System.Func<T, bool> predicate)
    {
        foreach (T item in array)
        {
            if (predicate(item))
                yield return item;
        }
    }
    
    public static System.Collections.Generic.IEnumerable<TResult> Select<TSource, TResult>(this System.Collections.Generic.IEnumerable<TSource> source, System.Func<TSource, TResult> selector)
    {
        foreach (TSource item in source)
        {
            yield return selector(item);
        }
    }
    
    public static System.Collections.Generic.IEnumerable<T> Distinct<T>(this System.Collections.Generic.IEnumerable<T> source)
    {
        var seen = new System.Collections.Generic.HashSet<T>();
        foreach (T item in source)
        {
            if (seen.Add(item))
                yield return item;
        }
    }
    
    public static T[] ToArray<T>(this System.Collections.Generic.IEnumerable<T> source)
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (T item in source)
        {
            list.Add(item);
        }
        return list.ToArray();
    }
}
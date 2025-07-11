using UnityEngine;

public class MaterialCreator : MonoBehaviour
{
    [Header("Material Creation")]
    public bool createMaterials = true;
    
    [Header("Colors")]
    public Color straightColor = Color.blue;
    public Color curveColor = Color.green;
    
    private Material straightMaterial;
    private Material curveMaterial;
    
    void Start()
    {
        if (createMaterials)
        {
            CreateMaterials();
            AssignMaterialsToManager();
        }
    }
    
    void CreateMaterials()
    {
        straightMaterial = new Material(Shader.Find("Standard"));
        straightMaterial.color = straightColor;
        straightMaterial.name = "StraightRiverMaterial";
        
        curveMaterial = new Material(Shader.Find("Standard"));
        curveMaterial.color = curveColor;
        curveMaterial.name = "CurveRiverMaterial";
        
        Debug.Log("Created materials: Straight (Blue) and Curve (Green)");
    }
    
    void AssignMaterialsToManager()
    {
        SimpleRiverManager riverManager = FindObjectOfType<SimpleRiverManager>();
        if (riverManager != null)
        {
            riverManager.straightMaterial = straightMaterial;
            riverManager.curveMaterial = curveMaterial;
            Debug.Log("Assigned materials to SimpleRiverManager");
        }
    }
    
    [ContextMenu("Create Materials")]
    public void CreateMaterialsManually()
    {
        CreateMaterials();
        AssignMaterialsToManager();
    }
}
using UnityEngine;

public enum SegmentType
{
    Straight,
    CurveLeft,
    CurveRight,
    Fork,
    Merge,
    Rapid
}

public class SimpleRiverSegment : MonoBehaviour
{
    [Header("Segment Properties")]
    public SegmentType segmentType = SegmentType.Straight;
    public Transform[] startPoints;
    public Transform[] endPoints;
    public float segmentLength = 2f;
    public float riverWidth = 0.8f;
    
    [Header("Visual")]
    public Material straightMaterial;
    public Material curveMaterial;
    public Material forkMaterial;
    
    private MeshRenderer meshRenderer;
    private GameObject cubeVisual;
    
    void Awake()
    {
        CreateSegmentVisual();
        SetupConnectorPoints();
    }
    
    void CreateSegmentVisual()
    {
        cubeVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeVisual.transform.SetParent(transform);
        cubeVisual.transform.localPosition = Vector3.zero;
        cubeVisual.transform.localScale = new Vector3(1f, 1f, 2f);
        cubeVisual.transform.localRotation = Quaternion.identity;
        
        meshRenderer = cubeVisual.GetComponent<MeshRenderer>();
    }
    
    void SetupConnectorPoints()
    {
        SetupConnectorPointsForType(segmentType);
    }
    
    void SetupConnectorPointsForType(SegmentType type)
    {
        ClearExistingPoints();
        
        switch (type)
        {
            case SegmentType.Straight:
                CreateStraightPoints();
                break;
            case SegmentType.CurveLeft:
                CreateCurvePoints(-1f);
                break;
            case SegmentType.CurveRight:
                CreateCurvePoints(1f);
                break;
            case SegmentType.Fork:
                CreateForkPoints();
                break;
            case SegmentType.Merge:
                CreateMergePoints();
                break;
            case SegmentType.Rapid:
                CreateRapidPoints();
                break;
        }
    }
    
    void ClearExistingPoints()
    {
        if (startPoints != null)
        {
            foreach (var point in startPoints)
                if (point != null) DestroyImmediate(point.gameObject);
        }
        
        if (endPoints != null)
        {
            foreach (var point in endPoints)
                if (point != null) DestroyImmediate(point.gameObject);
        }
    }
    
    void CreateStraightPoints()
    {
        startPoints = new Transform[1];
        endPoints = new Transform[1];
        
        float randomOffset = Random.Range(-riverWidth * 0.3f, riverWidth * 0.3f);
        
        startPoints[0] = CreatePoint("StartPoint", new Vector3(randomOffset, 0, -1f));
        endPoints[0] = CreatePoint("EndPoint", new Vector3(randomOffset + Random.Range(-0.2f, 0.2f), 0, 1f));
    }
    
    void CreateCurvePoints(float direction)
    {
        startPoints = new Transform[1];
        endPoints = new Transform[1];
        
        float startOffset = Random.Range(-riverWidth * 0.2f, riverWidth * 0.2f);
        float endOffset = startOffset + (direction * Random.Range(0.3f, 0.7f));
        
        startPoints[0] = CreatePoint("StartPoint", new Vector3(startOffset, 0, -1f));
        endPoints[0] = CreatePoint("EndPoint", new Vector3(endOffset, 0, 1f));
    }
    
    void CreateForkPoints()
    {
        startPoints = new Transform[1];
        endPoints = new Transform[2];
        
        float startOffset = Random.Range(-riverWidth * 0.1f, riverWidth * 0.1f);
        
        startPoints[0] = CreatePoint("StartPoint", new Vector3(startOffset, 0, -1f));
        endPoints[0] = CreatePoint("EndPoint_Left", new Vector3(startOffset - 0.4f, 0, 1f));
        endPoints[1] = CreatePoint("EndPoint_Right", new Vector3(startOffset + 0.4f, 0, 1f));
    }
    
    void CreateMergePoints()
    {
        startPoints = new Transform[2];
        endPoints = new Transform[1];
        
        float endOffset = Random.Range(-riverWidth * 0.1f, riverWidth * 0.1f);
        
        startPoints[0] = CreatePoint("StartPoint_Left", new Vector3(-0.4f, 0, -1f));
        startPoints[1] = CreatePoint("StartPoint_Right", new Vector3(0.4f, 0, -1f));
        endPoints[0] = CreatePoint("EndPoint", new Vector3(endOffset, 0, 1f));
    }
    
    void CreateRapidPoints()
    {
        startPoints = new Transform[1];
        endPoints = new Transform[1];
        
        float startOffset = Random.Range(-riverWidth * 0.4f, riverWidth * 0.4f);
        float endOffset = Random.Range(-riverWidth * 0.4f, riverWidth * 0.4f);
        
        startPoints[0] = CreatePoint("StartPoint", new Vector3(startOffset, 0, -1f));
        endPoints[0] = CreatePoint("EndPoint", new Vector3(endOffset, 0, 1f));
    }
    
    Transform CreatePoint(string name, Vector3 localPosition)
    {
        GameObject pointObj = new GameObject(name);
        pointObj.transform.SetParent(transform);
        pointObj.transform.localPosition = localPosition;
        pointObj.transform.localRotation = Quaternion.identity;
        return pointObj.transform;
    }
    
    void UpdateMaterial()
    {
        if (meshRenderer != null)
        {
            Material materialToUse = null;
            Color defaultColor = Color.white;
            
            switch (segmentType)
            {
                case SegmentType.Straight:
                    materialToUse = straightMaterial;
                    defaultColor = Color.blue;
                    break;
                case SegmentType.CurveLeft:
                case SegmentType.CurveRight:
                    materialToUse = curveMaterial;
                    defaultColor = Color.green;
                    break;
                case SegmentType.Fork:
                case SegmentType.Merge:
                    materialToUse = forkMaterial;
                    defaultColor = Color.yellow;
                    break;
                case SegmentType.Rapid:
                    materialToUse = curveMaterial;
                    defaultColor = Color.red;
                    break;
            }
            
            if (materialToUse != null)
            {
                meshRenderer.material = materialToUse;
            }
            else
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = defaultColor;
                meshRenderer.material = defaultMat;
            }
        }
    }
    
    public Vector3[] GetStartPositions()
    {
        if (startPoints == null) return new Vector3[0];
        Vector3[] positions = new Vector3[startPoints.Length];
        for (int i = 0; i < startPoints.Length; i++)
        {
            positions[i] = startPoints[i].position;
        }
        return positions;
    }
    
    public Vector3[] GetEndPositions()
    {
        if (endPoints == null) return new Vector3[0];
        Vector3[] positions = new Vector3[endPoints.Length];
        for (int i = 0; i < endPoints.Length; i++)
        {
            positions[i] = endPoints[i].position;
        }
        return positions;
    }
    
    public Vector3 GetPrimaryStartPosition()
    {
        return startPoints != null && startPoints.Length > 0 ? startPoints[0].position : transform.position;
    }
    
    public Vector3 GetPrimaryEndPosition()
    {
        return endPoints != null && endPoints.Length > 0 ? endPoints[0].position : transform.position;
    }
    
    public int GetStartPointCount()
    {
        return startPoints != null ? startPoints.Length : 0;
    }
    
    public int GetEndPointCount()
    {
        return endPoints != null ? endPoints.Length : 0;
    }
    
    public Quaternion GetEndRotation()
    {
        return transform.rotation;
    }
    
    public void SetSegmentType(SegmentType type)
    {
        segmentType = type;
        SetupConnectorPointsForType(type);
        UpdateMaterial();
    }
    
    public void SetMaterials(Material straight, Material curve, Material fork = null)
    {
        straightMaterial = straight;
        curveMaterial = curve;
        forkMaterial = fork;
        UpdateMaterial();
    }
}
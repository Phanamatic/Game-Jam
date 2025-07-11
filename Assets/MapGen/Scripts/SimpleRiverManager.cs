using UnityEngine;
using System.Collections.Generic;

public class SimpleRiverManager : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    
    [Header("Segment Settings")]
    public SimpleRiverSegment segmentPrefab;
    public float segmentLength = 2f;
    
    [Header("Materials")]
    public Material straightMaterial;
    public Material curveMaterial;
    
    [Header("Generation")]
    public int totalSegments = 5;
    public float moveThreshold = 1f;
    
    public List<SimpleRiverSegment> activeSegments = new List<SimpleRiverSegment>();
    private Vector3 nextSpawnPosition;
    private Quaternion nextSpawnRotation;
    private float playerStartZ;
    private int currentSegmentIndex = 0;
    
    void Start()
    {
        if (player == null)
        {
            Debug.LogError("Player transform not assigned!");
            return;
        }
        
        playerStartZ = player.position.z;
        nextSpawnPosition = transform.position;
        nextSpawnRotation = transform.rotation;
        
        CreateInitialSegments();
    }
    
    void Update()
    {
        if (player == null) return;
        
        float playerProgress = player.position.z - playerStartZ;
        int targetSegmentIndex = Mathf.FloorToInt(playerProgress / segmentLength);
        
        while (currentSegmentIndex < targetSegmentIndex)
        {
            MoveSegmentsForward();
            currentSegmentIndex++;
        }
    }
    
    void CreateInitialSegments()
    {
        for (int i = 0; i < totalSegments; i++)
        {
            CreateSegment();
        }
    }
    
    void CreateSegment()
    {
        if (segmentPrefab == null)
        {
            CreateDefaultSegment();
            return;
        }
        
        SimpleRiverSegment newSegment = Instantiate(segmentPrefab);
        PositionSegment(newSegment);
        
        SegmentType segmentType = GetRandomSegmentType();
        newSegment.SetSegmentType(segmentType);
        SetupSegmentMaterials(newSegment);
        
        activeSegments.Add(newSegment);
        UpdateNextSpawnPosition(newSegment);
    }
    
    void CreateDefaultSegment()
    {
        GameObject segmentObj = new GameObject($"Segment_{activeSegments.Count}");
        SimpleRiverSegment segment = segmentObj.AddComponent<SimpleRiverSegment>();
        
        PositionSegment(segment);
        
        segment.segmentLength = segmentLength;
        segment.SetMaterials(straightMaterial, curveMaterial);
        
        SegmentType segmentType = GetRandomSegmentType();
        segment.SetSegmentType(segmentType);
        
        activeSegments.Add(segment);
        UpdateNextSpawnPosition(segment);
    }
    
    void PositionSegment(SimpleRiverSegment segment)
    {
        if (activeSegments.Count == 0)
        {
            segment.transform.position = nextSpawnPosition;
            segment.transform.rotation = nextSpawnRotation;
        }
        else
        {
            SimpleRiverSegment lastSegment = activeSegments[activeSegments.Count - 1];
            
            Vector3 lastEndPos = lastSegment.GetPrimaryEndPosition();
            Vector3 segmentStartPos = segment.GetPrimaryStartPosition();
            Vector3 offset = segmentStartPos - segment.transform.position;
            
            segment.transform.position = lastEndPos - offset;
            segment.transform.rotation = nextSpawnRotation;
        }
    }
    
    void UpdateNextSpawnPosition(SimpleRiverSegment segment)
    {
        Vector3[] endPositions = segment.GetEndPositions();
        if (endPositions.Length > 0)
        {
            int randomEndIndex = Random.Range(0, endPositions.Length);
            nextSpawnPosition = endPositions[randomEndIndex];
        }
        else
        {
            nextSpawnPosition = segment.GetPrimaryEndPosition();
        }
        
        nextSpawnRotation = segment.GetEndRotation();
    }
    
    void SetupSegmentMaterials(SimpleRiverSegment segment)
    {
        segment.straightMaterial = straightMaterial;
        segment.curveMaterial = curveMaterial;
    }
    
    SegmentType GetRandomSegmentType()
    {
        int rand = Random.Range(0, 100);
        
        if (rand < 40)
            return SegmentType.Straight;
        else if (rand < 60)
            return SegmentType.CurveLeft;
        else if (rand < 80)
            return SegmentType.CurveRight;
        else if (rand < 90)
            return SegmentType.Rapid;
        else if (rand < 95)
            return SegmentType.Fork;
        else
            return SegmentType.Merge;
    }
    
    void MoveSegmentsForward()
    {
        if (activeSegments.Count == 0) return;
        
        SimpleRiverSegment oldSegment = activeSegments[0];
        activeSegments.RemoveAt(0);
        
        PositionSegment(oldSegment);
        
        SegmentType newType = GetRandomSegmentType();
        oldSegment.SetSegmentType(newType);
        oldSegment.SetMaterials(straightMaterial, curveMaterial);
        
        activeSegments.Add(oldSegment);
        UpdateNextSpawnPosition(oldSegment);
    }
    
    void OnDrawGizmos()
    {
        if (player == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, 0.5f);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(nextSpawnPosition, 0.3f);
        
        if (activeSegments.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var segment in activeSegments)
            {
                if (segment != null)
                {
                    Gizmos.DrawWireCube(segment.transform.position, Vector3.one * 0.5f);
                }
            }
        }
    }
}
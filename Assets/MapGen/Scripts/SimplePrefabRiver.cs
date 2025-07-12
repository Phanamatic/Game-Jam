using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TerrainPrefab
{
    public TerrainType type;
    public GameObject prefab;
    [Range(0f, 1f)]
    public float weight = 1f;
}

public class SimplePrefabRiver : MonoBehaviour
{
    [Header("Terrain Prefabs")]
    public List<TerrainPrefab> terrainPrefabs = new List<TerrainPrefab>();
    
    [Header("Player Settings")]
    public string playerTag = "Player";
    
    [Header("Generation Settings")]
    public int segmentsBehindPlayer = 2;
    public int segmentsAheadOfPlayer = 2;
    public float generationTriggerDistance = 1f;
    
    [Header("Connection Settings")]
    public string startPointName = "StartPoint";
    public string endPointName = "EndPoint";
    public bool debugConnections = true;
    public float connectionTolerance = 0.01f;
     
    [Header("Straight Piece Rules")]
    [Tooltip("Enforce straight piece ordering: 1→2, 2→3, 3→1")]
    public bool enforceStraitOrder = true;
    [Tooltip("Allow same straight type to repeat")]
    public bool allowStraightRepeats = true;
    
    [Header("Meandering Logic")]
    [Range(1, 5)]
    public int minStraightBeforeCurve = 2;
    [Range(3, 8)] 
    public int maxStraightBeforeCurve = 4;
    [Range(1, 3)]
    public int maxConsecutiveCurves = 2;
    [Range(0f, 1f)]
    public float baseCurveChance = 0.3f;
    
    private Transform player;
    private List<GameObject> activeSegments = new List<GameObject>();
    private List<TerrainType> segmentTypes = new List<TerrainType>();
    private int playerSegmentIndex = 0;
    private float lastPlayerZ;
    
    private int consecutiveStraights = 0;
    private int consecutiveCurves = 0;
    private TerrainType lastCurveDirection = TerrainType.CurveLeft;
    private int straightsUntilCurve;
    
    private List<TerrainPrefab> straightPrefabs = new List<TerrainPrefab>();
    private List<TerrainPrefab> curvePrefabs = new List<TerrainPrefab>();

    void Start()
    {
        InitializePrefabLists();
        FindPlayer();
        straightsUntilCurve = Random.Range(minStraightBeforeCurve, maxStraightBeforeCurve + 1);
        
        CreateInitialSegments();
    }
    
    void InitializePrefabLists()
    {
        straightPrefabs.Clear();
        curvePrefabs.Clear();
        
        foreach (TerrainPrefab terrain in terrainPrefabs)
        {
            if (terrain.prefab != null)
            {
                if (terrain.type == TerrainType.Straight1 || terrain.type == TerrainType.Straight2 || terrain.type == TerrainType.Straight3)
                {
                    straightPrefabs.Add(terrain);
                }
                else if (terrain.type == TerrainType.CurveLeft || terrain.type == TerrainType.CurveRight)
                {
                    curvePrefabs.Add(terrain);
                }
            }
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float currentPlayerZ = player.position.z;
        if (Mathf.Abs(currentPlayerZ - lastPlayerZ) >= generationTriggerDistance)
        {
            UpdatePlayerPosition();
            ManageSegments();
            lastPlayerZ = currentPlayerZ;
        }
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj.transform;
            lastPlayerZ = player.position.z;
        }
        else
        {
            Debug.LogWarning($"No GameObject with '{playerTag}' tag found.");
        }
    }

    void CreateInitialSegments()
    {
        int totalSegments = segmentsBehindPlayer + 1 + segmentsAheadOfPlayer;
        
        for (int i = 0; i < totalSegments; i++)
        {
            SpawnNextSegment();
        }
        
        playerSegmentIndex = segmentsBehindPlayer;
    }

    void SpawnNextSegment()
    {
        TerrainType terrainType = GetSmartTerrainType();
        GameObject prefabToSpawn = GetPrefabForType(terrainType);
        
        if (prefabToSpawn == null)
        {
            Debug.LogError($"No prefab assigned for terrain type: {terrainType}");
            return;
        }

        // Spawn the new segment
        GameObject newSegment = Instantiate(prefabToSpawn);
        newSegment.name = $"TerrainSegment_{activeSegments.Count}_{terrainType}";
        
        // Get the TerrainConnector component
        TerrainConnector connector = newSegment.GetComponent<TerrainConnector>();
        if (connector == null)
        {
            Debug.LogError($"Prefab {prefabToSpawn.name} is missing TerrainConnector component! Please add it to the prefab.");
            
            // Try to add it at runtime as fallback
            connector = newSegment.AddComponent<TerrainConnector>();
            connector.terrainType = terrainType;
            connector.startPoint = FindConnectionPoint(newSegment, startPointName);
            connector.endPoint = FindConnectionPoint(newSegment, endPointName);
            
            if (!connector.IsValid())
            {
                Debug.LogError($"Could not find connection points on {newSegment.name}!");
                Destroy(newSegment);
                return;
            }
        }
        
        // Position the segment
        if (activeSegments.Count > 0)
        {
            GameObject previousSegment = activeSegments[activeSegments.Count - 1];
            TerrainConnector previousConnector = previousSegment.GetComponent<TerrainConnector>();
            
            if (previousConnector != null && previousConnector.IsValid())
            {
                // Use the TerrainConnector's built-in alignment method
                connector.AlignToPrevious(previousConnector, connectionTolerance);
                ApplyCurveRotationFix(newSegment, connector, terrainType);

                
                // Validate no overlap with other segments
                if (CheckForOverlaps(newSegment))
                {
                    Debug.LogWarning($"Segment {newSegment.name} would overlap! Trying alternative...");
                    Destroy(newSegment);
                    
                    // Try to force a different type
                    terrainType = GetAlternativeType(terrainType);
                    prefabToSpawn = GetPrefabForType(terrainType);
                    if (prefabToSpawn != null)
                    {
                        SpawnNextSegment(); // Recursive call with new type
                    }
                    return;
                }
            }
            else
            {
                Debug.LogError("Previous segment has invalid connector!");
                Destroy(newSegment);
                return;
            }
        }
        else
        {
            // First segment
            newSegment.transform.position = transform.position;
            newSegment.transform.rotation = transform.rotation;
        }
        
        activeSegments.Add(newSegment);
        segmentTypes.Add(terrainType);
    }
    
    bool CheckForOverlaps(GameObject newSegment)
    {
        Bounds newBounds = GetSegmentBounds(newSegment);
        
        // Check against all active segments except the one we're connecting to
        for (int i = 0; i < activeSegments.Count - 1; i++)
        {
            if (activeSegments[i] != null)
            {
                Bounds existingBounds = GetSegmentBounds(activeSegments[i]);
                if (newBounds.Intersects(existingBounds))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    Bounds GetSegmentBounds(GameObject segment)
    {
        Renderer[] renderers = segment.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(segment.transform.position, Vector3.one);
            
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        
        // Slightly shrink bounds to allow touching but not overlapping
        bounds.size = bounds.size * 0.95f;
        return bounds;
    }
    
    TerrainType GetAlternativeType(TerrainType originalType)
    {
        // If a curve caused overlap, try a straight
        if (originalType == TerrainType.CurveLeft || originalType == TerrainType.CurveRight)
        {
            consecutiveCurves = 0;
            consecutiveStraights++;
            return GetRandomStraightType();
        }
        
        // If a straight caused overlap, try a different straight
        return GetRandomStraightType();
    }

    GameObject GetPrefabForType(TerrainType type)
    {
        foreach (TerrainPrefab terrain in terrainPrefabs)
        {
            if (terrain.type == type && terrain.prefab != null)
            {
                return terrain.prefab;
            }
        }
        
        if (straightPrefabs.Count > 0)
            return straightPrefabs[0].prefab;
            
        return null;
    }

    TerrainType GetSmartTerrainType()
    {
        bool shouldCurve = false;
        
        if (consecutiveStraights >= straightsUntilCurve)
        {
            shouldCurve = true;
        }
        else if (consecutiveCurves >= maxConsecutiveCurves)
        {
            shouldCurve = false;
        }
        else
        {
            shouldCurve = Random.Range(0f, 1f) < baseCurveChance;
        }
        
        if (shouldCurve && curvePrefabs.Count > 0)
        {
            return GetRandomCurveType();
        }
        else
        {
            return GetRandomStraightType();
        }
    }
    
    TerrainType GetRandomStraightType()
    {
        consecutiveStraights++;
        consecutiveCurves = 0;
        
        if (straightPrefabs.Count == 0)
            return TerrainType.Straight1;
        
        List<TerrainPrefab> validStraights = straightPrefabs;
        
        if (enforceStraitOrder && activeSegments.Count > 0)
        {
            // Get the last straight type used
            TerrainType lastType = segmentTypes[segmentTypes.Count - 1];
            
            // Build list of valid next straights based on order rules
            validStraights = new List<TerrainPrefab>();
            
            switch (lastType)
            {
                case TerrainType.Straight1:
                    // After Straight1: can use Straight1 (if repeats allowed) or Straight2
                    foreach (var s in straightPrefabs)
                    {
                        if (s.type == TerrainType.Straight2 || (allowStraightRepeats && s.type == TerrainType.Straight1))
                            validStraights.Add(s);
                    }
                    break;
                    
                case TerrainType.Straight2:
                    // After Straight2: can use Straight2 (if repeats allowed) or Straight3
                    foreach (var s in straightPrefabs)
                    {
                        if (s.type == TerrainType.Straight3 || (allowStraightRepeats && s.type == TerrainType.Straight2))
                            validStraights.Add(s);
                    }
                    break;
                    
                case TerrainType.Straight3:
                    // After Straight3: can use Straight3 (if repeats allowed) or Straight1
                    foreach (var s in straightPrefabs)
                    {
                        if (s.type == TerrainType.Straight1 || (allowStraightRepeats && s.type == TerrainType.Straight3))
                            validStraights.Add(s);
                    }
                    break;
                    
                default:
                    // After a curve or at start, any straight is valid
                    validStraights = straightPrefabs;
                    break;
            }
            
            // If no valid straights found, allow any
            if (validStraights.Count == 0)
            {
                Debug.LogWarning("No valid straights found with current rules, using any straight");
                validStraights = straightPrefabs;
            }
        }
            
        return GetWeightedRandomTerrain(validStraights).type;
    }
    
    TerrainType GetRandomCurveType()
    {
        consecutiveStraights = 0;
        consecutiveCurves++;
        straightsUntilCurve = Random.Range(minStraightBeforeCurve, maxStraightBeforeCurve + 1);
        
        if (curvePrefabs.Count == 0)
            return TerrainType.CurveLeft;
        
        List<TerrainPrefab> availableCurves = new List<TerrainPrefab>();
        
        // Get the last terrain type
        TerrainType lastType = activeSegments.Count > 0 ? segmentTypes[segmentTypes.Count - 1] : TerrainType.Straight1;
        
        // Rules for curves:
        // 1. Cannot have the same curve type twice in a row
        // 2. Prefer alternating curve directions when multiple curves
        if (lastType == TerrainType.CurveLeft)
        {
            // After left curve, only allow right curves
            foreach (var curve in curvePrefabs)
            {
                if (curve.type == TerrainType.CurveRight)
                    availableCurves.Add(curve);
            }
        }
        else if (lastType == TerrainType.CurveRight)
        {
            // After right curve, only allow left curves
            foreach (var curve in curvePrefabs)
            {
                if (curve.type == TerrainType.CurveLeft)
                    availableCurves.Add(curve);
            }
        }
        else
        {
            // After a straight, any curve is fine
            availableCurves.AddRange(curvePrefabs);
        }
        
        // Fallback if no valid curves
        if (availableCurves.Count == 0)
        {
            Debug.LogWarning("No valid curves found, forcing a straight instead");
            consecutiveCurves--;
            consecutiveStraights++;
            return GetRandomStraightType();
        }
        
        TerrainType chosenCurve = GetWeightedRandomTerrain(availableCurves).type;
        lastCurveDirection = chosenCurve;
        
        return chosenCurve;
    }
    
    TerrainPrefab GetWeightedRandomTerrain(List<TerrainPrefab> terrains)
    {
        if (terrains.Count == 0)
            return null;
            
        float totalWeight = 0f;
        foreach (TerrainPrefab terrain in terrains)
        {
            totalWeight += terrain.weight;
        }
        
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        foreach (TerrainPrefab terrain in terrains)
        {
            currentWeight += terrain.weight;
            if (randomValue <= currentWeight)
            {
                return terrain;
            }
        }
        
        return terrains[terrains.Count - 1];
    }

    Transform FindConnectionPoint(GameObject segment, string pointName)
    {
        Transform[] children = segment.GetComponentsInChildren<Transform>();
        foreach (Transform child in children)
        {
            if (child.name.Equals(pointName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }
        return null;
    }

    void UpdatePlayerPosition()
    {
        if (activeSegments.Count == 0) return;

        float playerZ = player.position.z;
        int closestSegmentIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < activeSegments.Count; i++)
        {
            if (activeSegments[i] != null)
            {
                float distance = Mathf.Abs(activeSegments[i].transform.position.z - playerZ);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSegmentIndex = i;
                }
            }
        }

        playerSegmentIndex = closestSegmentIndex;
    }

    void ManageSegments()
    {
        int segmentsAhead = activeSegments.Count - 1 - playerSegmentIndex;
        while (segmentsAhead < segmentsAheadOfPlayer)
        {
            SpawnNextSegment();
            segmentsAhead++;
        }

        while (playerSegmentIndex > segmentsBehindPlayer + 2)
        {
            RemoveSegmentAtIndex(0);
            playerSegmentIndex--;
        }
    }

    void RemoveSegmentAtIndex(int index)
    {
        if (index >= 0 && index < activeSegments.Count)
        {
            if (activeSegments[index] != null)
            {
                Destroy(activeSegments[index]);
            }
            activeSegments.RemoveAt(index);
            segmentTypes.RemoveAt(index);
        }
    }

    void OnDrawGizmos()
    {
        if (!debugConnections) return;
        
        // Draw player position
        if (player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, 0.5f);
        }

        // Draw active segments and connections
        for (int i = 0; i < activeSegments.Count; i++)
        {
            if (activeSegments[i] != null)
            {
                // Color based on relationship to player
                if (i < playerSegmentIndex)
                    Gizmos.color = new Color(1, 0.5f, 0.5f); // Behind player - light red
                else if (i == playerSegmentIndex)
                    Gizmos.color = Color.yellow; // Player segment
                else
                    Gizmos.color = new Color(0.5f, 0.5f, 1); // Ahead of player - light blue

                Vector3 pos = activeSegments[i].transform.position + Vector3.up * 0.5f;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);
                
                // Draw connection verification
                if (i > 0 && activeSegments[i-1] != null)
                {
                    TerrainConnector prevConnector = activeSegments[i-1].GetComponent<TerrainConnector>();
                    TerrainConnector currConnector = activeSegments[i].GetComponent<TerrainConnector>();
                    
                    if (prevConnector != null && currConnector != null && 
                        prevConnector.endPoint != null && currConnector.startPoint != null)
                    {
                        float dist = Vector3.Distance(prevConnector.endPoint.position, currConnector.startPoint.position);
                        Gizmos.color = dist <= connectionTolerance ? Color.green : Color.red;
                        Gizmos.DrawLine(prevConnector.endPoint.position, currConnector.startPoint.position);
                        
                        #if UNITY_EDITOR
                        if (dist > connectionTolerance)
                        {
                            Vector3 midPoint = (prevConnector.endPoint.position + currConnector.startPoint.position) / 2f;
                            UnityEditor.Handles.Label(midPoint, $"Gap: {dist:F3}m");
                        }
                        #endif
                    }
                }

                #if UNITY_EDITOR
                UnityEditor.Handles.Label(pos + Vector3.up * 1.5f, $"{i}: {segmentTypes[i]}");
                #endif
            }
        }
    }
    
    [ContextMenu("Validate All Prefabs")]
    void ValidateAllPrefabs()
    {
        Debug.Log("=== Validating Terrain Prefabs ===");
        int validCount = 0;
        int invalidCount = 0;
        
        foreach (TerrainPrefab terrain in terrainPrefabs)
        {
            if (terrain.prefab == null)
            {
                Debug.LogError($"Terrain type {terrain.type} has no prefab assigned!");
                invalidCount++;
                continue;
            }
            
            TerrainConnector connector = terrain.prefab.GetComponent<TerrainConnector>();
            if (connector == null)
            {
                Debug.LogError($"Prefab '{terrain.prefab.name}' is missing TerrainConnector component!");
                invalidCount++;
            }
            else if (!connector.IsValid())
            {
                Debug.LogError($"Prefab '{terrain.prefab.name}' has invalid connection points!");
                invalidCount++;
            }
            else
            {
                Debug.Log($"✓ Prefab '{terrain.prefab.name}' is properly configured");
                validCount++;
            }
        }
        
        Debug.Log($"=== Validation Complete: {validCount} valid, {invalidCount} invalid ===");
    }
    
    // Public methods
    public void AddTerrainPrefab(TerrainType type, GameObject prefab, float weight = 1f)
    {
        TerrainPrefab newTerrain = new TerrainPrefab();
        newTerrain.type = type;
        newTerrain.prefab = prefab;
        newTerrain.weight = weight;
        terrainPrefabs.Add(newTerrain);
        InitializePrefabLists();
    }

    public void SetGenerationSettings(int behind, int ahead, float triggerDistance)
    {
        segmentsBehindPlayer = behind;
        segmentsAheadOfPlayer = ahead;
        generationTriggerDistance = triggerDistance;
    }
    
    public void SetMeanderingSettings(int minStraight, int maxStraight, int maxCurves, float curveChance)
    {
        minStraightBeforeCurve = minStraight;
        maxStraightBeforeCurve = maxStraight;
        maxConsecutiveCurves = maxCurves;
        baseCurveChance = curveChance;
    }
    void ApplyCurveRotationFix(GameObject segment, TerrainConnector connector, TerrainType type)
{
    if (type == TerrainType.CurveLeft)
    {
        segment.transform.RotateAround(connector.startPoint.position, Vector3.up, -90f);
    }
    else if (type == TerrainType.CurveRight)
    {
        segment.transform.RotateAround(connector.startPoint.position, Vector3.up, 90f);
    }
}

}
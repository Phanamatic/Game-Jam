using UnityEngine;

// Attach this component to each terrain piece prefab
[System.Serializable]
public class TerrainConnector : MonoBehaviour
{
    [Header("Connection Points")]
    [Tooltip("The point where this piece connects to the previous piece")]
    public Transform startPoint;
    
    [Tooltip("The point where the next piece will connect to this one")]
    public Transform endPoint;
    
    [Header("Terrain Info")]
    public TerrainType terrainType;
    
    [Header("Debug")]
    public bool showGizmos = true;
    public float gizmoSize = 0.3f;
    
    // Automatically find connection points if not assigned
    private void OnValidate()
    {
        if (startPoint == null)
        {
            Transform found = transform.Find("StartPoint");
            if (found == null) found = transform.Find("Start");
            if (found == null) found = transform.Find("ConnectionStart");
            startPoint = found;
        }
        
        if (endPoint == null)
        {
            Transform found = transform.Find("EndPoint");
            if (found == null) found = transform.Find("End");
            if (found == null) found = transform.Find("ConnectionEnd");
            endPoint = found;
        }
    }
    
    // Validate the setup
    public bool IsValid()
    {
        return startPoint != null && endPoint != null;
    }
    
    // Get the forward direction at the connection points
    public Vector3 GetStartDirection()
    {
        return startPoint != null ? startPoint.forward : transform.forward;
    }
    
    public Vector3 GetEndDirection()
    {
        return endPoint != null ? endPoint.forward : transform.forward;
    }
    
    // Visual debugging
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // Draw start point
        if (startPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startPoint.position, gizmoSize);
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawRay(startPoint.position, startPoint.forward * 1f);
            DrawArrow(startPoint.position, startPoint.forward, Color.green);
        }
        
        // Draw end point
        if (endPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endPoint.position, gizmoSize);
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawRay(endPoint.position, endPoint.forward * 1f);
            DrawArrow(endPoint.position, endPoint.forward, Color.red);
        }
        
        // Draw connection line
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawLine(startPoint.position, endPoint.position);
        }
        
        // Draw terrain type label
        #if UNITY_EDITOR
        if (Application.isEditor && !Application.isPlaying)
        {
            Vector3 labelPos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(labelPos, $"{gameObject.name}\n[{terrainType}]");
        }
        #endif
    }
    
    private void DrawArrow(Vector3 pos, Vector3 direction, Color color)
    {
        if (direction == Vector3.zero) return;
        
        Gizmos.color = color;
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        
        Gizmos.DrawRay(pos + direction, right * 0.3f);
        Gizmos.DrawRay(pos + direction, left * 0.3f);
    }
    
    // Helper method to align this piece to a previous piece
    public void AlignToPrevious(TerrainConnector previousConnector, float tolerance = 0.01f)
    {
        if (previousConnector == null || !previousConnector.IsValid() || !IsValid())
        {
            Debug.LogError("Cannot align - invalid connectors");
            return;
        }
        
        Transform previousEnd = previousConnector.endPoint;
        Transform currentStart = this.startPoint;
        
        // Calculate the offset from this piece's origin to its start point
        Vector3 startPointLocalPos = transform.InverseTransformPoint(currentStart.position);
        
        // Align rotation: this piece should continue in the direction of previous end
        transform.rotation = previousEnd.rotation;
        
        // Position the piece so its start point aligns with previous end point
        Vector3 rotatedOffset = transform.TransformVector(startPointLocalPos);
        transform.position = previousEnd.position - rotatedOffset;
        
        // Verify and correct if needed
        float distance = Vector3.Distance(previousEnd.position, currentStart.position);
        if (distance > tolerance)
        {
            Vector3 correction = previousEnd.position - currentStart.position;
            transform.position += correction;
            Debug.LogWarning($"Applied correction of {correction.magnitude}m to {gameObject.name}");
        }
    }
}

// Enum for terrain types (if not already defined elsewhere)
public enum TerrainType
{
    Straight1,
    Straight2,
    Straight3,
    CurveLeft,
    CurveRight
}
using UnityEngine;

public class RiverFlowVisualizer : MonoBehaviour
{
    [Header("Visualization")]
    public bool showConnectorPoints = true;
    public bool showFlowDirection = true;
    public Color startPointColor = Color.green;
    public Color endPointColor = Color.red;
    public Color flowLineColor = Color.cyan;
    public float pointSize = 0.1f;
    
    private SimpleRiverManager riverManager;
    
    void Start()
    {
        riverManager = FindObjectOfType<SimpleRiverManager>();
    }
    
    void OnDrawGizmos()
    {
        if (!showConnectorPoints && !showFlowDirection) return;
        
        if (riverManager != null && riverManager.activeSegments != null)
        {
            foreach (var segment in riverManager.activeSegments)
            {
                if (segment != null)
                {
                    DrawSegmentGizmos(segment);
                }
            }
        }
    }
    
    void DrawSegmentGizmos(SimpleRiverSegment segment)
    {
        if (showConnectorPoints)
        {
            DrawConnectorPoints(segment);
        }
        
        if (showFlowDirection)
        {
            DrawFlowLines(segment);
        }
    }
    
    void DrawConnectorPoints(SimpleRiverSegment segment)
    {
        Vector3[] startPositions = segment.GetStartPositions();
        Vector3[] endPositions = segment.GetEndPositions();
        
        Gizmos.color = startPointColor;
        foreach (var pos in startPositions)
        {
            Gizmos.DrawSphere(pos, pointSize);
        }
        
        Gizmos.color = endPointColor;
        foreach (var pos in endPositions)
        {
            Gizmos.DrawSphere(pos, pointSize);
        }
    }
    
    void DrawFlowLines(SimpleRiverSegment segment)
    {
        Vector3[] startPositions = segment.GetStartPositions();
        Vector3[] endPositions = segment.GetEndPositions();
        
        Gizmos.color = flowLineColor;
        
        foreach (var startPos in startPositions)
        {
            foreach (var endPos in endPositions)
            {
                Vector3 direction = (endPos - startPos).normalized;
                Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f);
                
                Gizmos.DrawLine(startPos, endPos);
                
                Vector3 arrowPoint1 = midPoint - direction * 0.2f + Vector3.Cross(direction, Vector3.up) * 0.1f;
                Vector3 arrowPoint2 = midPoint - direction * 0.2f - Vector3.Cross(direction, Vector3.up) * 0.1f;
                
                Gizmos.DrawLine(midPoint, arrowPoint1);
                Gizmos.DrawLine(midPoint, arrowPoint2);
            }
        }
    }
}
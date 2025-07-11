using UnityEngine;

public class PlayerSimulator : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public bool autoMove = true;
    
    [Header("Manual Control")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    
    [Header("River Following")]
    public SimpleRiverManager riverManager;
    
    private float riverProgress = 0f;
    private Vector3 currentDirection = Vector3.forward;
    
    void Start()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = Vector3.one * 0.5f;
        
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        Material playerMat = new Material(Shader.Find("Standard"));
        playerMat.color = Color.red;
        renderer.material = playerMat;
        
        if (riverManager == null)
            riverManager = FindObjectOfType<SimpleRiverManager>();
    }
    
    void Update()
    {
        if (autoMove)
        {
            FollowRiver();
        }
        else
        {
            HandleManualMovement();
        }
    }
    
    void FollowRiver()
    {
        if (riverManager == null) return;
        
        SimpleRiverSegment currentSegment = GetCurrentSegment();
        if (currentSegment != null)
        {
            Vector3 segmentDirection = GetSegmentDirection(currentSegment);
            currentDirection = Vector3.Slerp(currentDirection, segmentDirection, Time.deltaTime * 2f);
        }
        
        transform.Translate(currentDirection * moveSpeed * Time.deltaTime, Space.World);
        
        Vector3 lookDirection = currentDirection;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
    
    SimpleRiverSegment GetCurrentSegment()
    {
        if (riverManager == null || riverManager.activeSegments.Count == 0) return null;
        
        float closestDistance = float.MaxValue;
        SimpleRiverSegment closestSegment = null;
        
        foreach (var segment in riverManager.activeSegments)
        {
            if (segment != null)
            {
                float distance = Vector3.Distance(transform.position, segment.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSegment = segment;
                }
            }
        }
        
        return closestSegment;
    }
    
    Vector3 GetSegmentDirection(SimpleRiverSegment segment)
    {
        switch (segment.segmentType)
        {
            case SegmentType.Straight:
                return segment.transform.forward;
            case SegmentType.CurveLeft:
                return (segment.transform.forward + segment.transform.right * -0.5f).normalized;
            case SegmentType.CurveRight:
                return (segment.transform.forward + segment.transform.right * 0.5f).normalized;
            default:
                return Vector3.forward;
        }
    }
    
    void HandleManualMovement()
    {
        if (Input.GetKey(forwardKey))
        {
            FollowRiver();
        }
        
        if (Input.GetKey(backwardKey))
        {
            transform.Translate(-currentDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}
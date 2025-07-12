using UnityEngine;

public class WaterCurrent : MonoBehaviour
{
    [Header("Current Settings")]
    [SerializeField] private Vector3 currentDirection = Vector3.right;
    [SerializeField] private float currentStrength = 100f;
    [SerializeField] private bool visualizeInEditor = true;
    
    [Header("Optional - Area of Effect")]
    [SerializeField] private bool useAreaOfEffect = false;
    [SerializeField] private float areaRadius = 10f;
    [SerializeField] private Transform areaCenter;
    
    private void Start()
    {
        // Normalize the direction vector
        currentDirection = currentDirection.normalized;
        
        if (areaCenter == null)
        {
            areaCenter = transform;
        }
    }
    
    private void FixedUpdate()
    {
        // Find all rigidbodies that should be affected by current
        Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
        
        foreach (Rigidbody rb in allRigidbodies)
        {
            // Check if this rigidbody should be affected
            if (ShouldAffectRigidbody(rb))
            {
                ApplyCurrentForce(rb);
            }
        }
    }
    
    private bool ShouldAffectRigidbody(Rigidbody rb)
    {
        // Check if it has a player controller (is the canoe)
        if (rb.GetComponent<CanoePaddleController>() == null)
            return false;
            
        // If using area of effect, check distance
        if (useAreaOfEffect)
        {
            float distance = Vector3.Distance(rb.transform.position, areaCenter.position);
            return distance <= areaRadius;
        }
        
        // Otherwise affect all player rigidbodies
        return true;
    }
    
    private void ApplyCurrentForce(Rigidbody rb)
    {
        Vector3 currentForce = currentDirection * currentStrength;
        rb.AddForce(currentForce, ForceMode.Force);
    }
    
    private void OnDrawGizmos()
    {
        if (!visualizeInEditor) return;
        
        // Draw current direction arrow
        Gizmos.color = Color.cyan;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (currentDirection.normalized * 5f);
        
        Gizmos.DrawLine(startPos, endPos);
        Gizmos.DrawWireSphere(endPos, 0.3f);
        
        // Draw area of effect if enabled
        if (useAreaOfEffect && areaCenter != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(areaCenter.position, areaRadius);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!visualizeInEditor) return;
        
        // Draw more detailed visualization when selected
        Gizmos.color = Color.yellow;
        
        // Draw current direction with strength indication
        Vector3 strengthVector = currentDirection * (currentStrength / 100f);
        Gizmos.DrawRay(transform.position, strengthVector);
        
        // Draw text info (this only shows in scene view)
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
            $"Current: {currentDirection}\nStrength: {currentStrength}");
        #endif
    }
    
    // Public methods to change current at runtime
    public void SetCurrentDirection(Vector3 direction)
    {
        currentDirection = direction.normalized;
    }
    
    public void SetCurrentStrength(float strength)
    {
        currentStrength = strength;
    }
    
    public void SetCurrent(Vector3 direction, float strength)
    {
        SetCurrentDirection(direction);
        SetCurrentStrength(strength);
    }
}
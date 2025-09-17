using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private bool isDead = false;
    
    [Header("Death Effects")]
    [SerializeField] private float deathDelay = 1f;
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;
    
    private CanoePaddleController paddleController;
    private Rigidbody rb;
    
    void Start()
    {
        paddleController = GetComponent<CanoePaddleController>();
        rb = GetComponent<Rigidbody>();
        
        // Set respawn position to current position if not set
        if (respawnPosition == Vector3.zero)
        {
            respawnPosition = transform.position;
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Check if hit by rock
        if (collision.gameObject.CompareTag("Rock") && !isDead)
        {
            Die();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Also check triggers in case rocks are set as triggers
        if (other.CompareTag("Rock") && !isDead)
        {
            Die();
        }
    }
    
    void Die()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log("Player died from rock collision!");
        
        // Disable controls
        if (paddleController != null)
        {
            paddleController.enabled = false;
        }
        
        // Add dramatic death effect
        if (rb != null)
        {
            rb.AddForce(Vector3.up * 500f + Vector3.back * 300f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 1000f, ForceMode.Impulse);
        }
        
        // Handle respawn or game over
        if (respawnOnDeath)
        {
            Invoke(nameof(Respawn), deathDelay);
        }
        else
        {
            Invoke(nameof(GameOver), deathDelay);
        }
    }
    
    void Respawn()
    {
        isDead = false;
        
        // Reset position and rotation
        transform.position = respawnPosition;
        transform.rotation = Quaternion.identity;
        
        // Reset physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Re-enable controls
        if (paddleController != null)
        {
            paddleController.enabled = true;
        }
        
        Debug.Log("Player respawned!");
    }
    
    void GameOver()
    {
        Debug.Log("GAME OVER!");
        // Add game over logic here (restart scene, show game over UI, etc.)
        
        // For now, just respawn anyway
        Respawn();
    }
    
    // Public methods for other scripts
    public bool IsDead => isDead;
    
    public void SetRespawnPosition(Vector3 position)
    {
        respawnPosition = position;
    }
    
    public void Kill()
    {
        Die();
    }
}
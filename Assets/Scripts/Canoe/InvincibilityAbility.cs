using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InvincibilityAbility : MonoBehaviour
{
    [Header("Ability Settings")]
    [SerializeField] float invincibilityDuration = 3f;
    [SerializeField] float angryRocksDuration = 2f;
    [SerializeField] float cooldownTime = 10f;
    [SerializeField] float rockChaseForce = 100f;
    [SerializeField] float rockDetectionRange = 15f;

    [Header("UI References")]
    [SerializeField] Image cooldownFillImage;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] GameObject statusPanel;

    private InputAction invincibilityInput;
    private bool isOnCooldown = false;
    private bool isInvincible = false;
    private bool angryRocksActive = false;
    private float currentCooldownTime = 0f;
    private float invincibilityTimer = 0f;
    private float angryRocksTimer = 0f;
    
    // Store original rock positions
    private System.Collections.Generic.Dictionary<GameObject, Vector3> originalRockPositions = new System.Collections.Generic.Dictionary<GameObject, Vector3>();

    void Awake()
    {
        // Set up input
        invincibilityInput = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/1");
        invincibilityInput.Enable();
        
        // Initialize UI
        if (statusPanel != null)
            statusPanel.SetActive(false);
            
        if (statusText != null)
            statusText.text = "";
            
        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = 1f;
    }

    void Update()
    {
        HandleInput();
        UpdateTimers();
        UpdateUI();
        
        if (angryRocksActive)
        {
            MakeRocksChasePlayer();
        }
    }

    void HandleInput()
    {
        if (invincibilityInput.WasPressedThisFrame() && !isOnCooldown)
        {
            ActivateInvincibility();
        }
    }

    void ActivateInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
        
        // Store original rock positions
        StoreOriginalRockPositions();
        
        // Turn off rock colliders
        ToggleRockColliders(false);
        
        // Show status panel and ability activated text
        if (statusPanel != null)
            statusPanel.SetActive(true);
            
        if (statusText != null)
            statusText.text = "ABILITY ACTIVATED: Invincibility";
        
        Debug.Log("INVINCIBILITY ACTIVATED!");
    }

    void EndInvincibility()
    {
        isInvincible = false;
        
        // Turn rock colliders back on
        ToggleRockColliders(true);
        
        // Start angry rocks phase
        angryRocksActive = true;
        angryRocksTimer = angryRocksDuration;
        
        // Change to side effect text
        if (statusText != null)
        {
            statusText.text = "SIDE EFFECT: Angry Rocks";
        }
        
        // Start cooldown
        isOnCooldown = true;
        currentCooldownTime = cooldownTime;
        
        Debug.Log("INVINCIBILITY ENDED - ANGRY ROCKS ACTIVATED!");
    }

    void EndAngryRocks()
    {
        angryRocksActive = false;
        
        // Return rocks to original positions
        ReturnRocksToOriginalPositions();
        
        // Hide status panel and clear text
        if (statusPanel != null)
            statusPanel.SetActive(false);
            
        if (statusText != null)
            statusText.text = "";
        
        Debug.Log("Angry rocks ended");
    }

    void UpdateTimers()
    {
        // Invincibility timer
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0f)
            {
                EndInvincibility();
            }
        }
        
        // Angry rocks timer
        if (angryRocksActive)
        {
            angryRocksTimer -= Time.deltaTime;
            if (angryRocksTimer <= 0f)
            {
                EndAngryRocks();
            }
        }
        
        // Cooldown timer
        if (isOnCooldown)
        {
            currentCooldownTime -= Time.deltaTime;
            if (currentCooldownTime <= 0f)
            {
                isOnCooldown = false;
                currentCooldownTime = 0f;
            }
        }
    }

    void UpdateUI()
    {
        if (cooldownFillImage != null)
        {
            if (isOnCooldown)
            {
                cooldownFillImage.fillAmount = 1f - (currentCooldownTime / cooldownTime);
            }
            else
            {
                cooldownFillImage.fillAmount = 1f;
            }
        }
    }

    void ToggleRockColliders(bool enabled)
    {
        GameObject[] rocks = GameObject.FindGameObjectsWithTag("Rock");
        
        foreach (GameObject rock in rocks)
        {
            Collider rockCollider = rock.GetComponent<Collider>();
            if (rockCollider != null)
            {
                rockCollider.enabled = enabled;
            }
        }
    }

    void MakeRocksChasePlayer()
    {
        GameObject[] rocks = GameObject.FindGameObjectsWithTag("Rock");
        GameObject player = GameObject.FindWithTag("Player");
        
        if (player == null) return;
        
        foreach (GameObject rock in rocks)
        {
            float distanceToPlayer = Vector3.Distance(rock.transform.position, player.transform.position);
            
            if (distanceToPlayer <= rockDetectionRange)
            {
                // Calculate direction to player
                Vector3 directionToPlayer = (player.transform.position - rock.transform.position).normalized;
                
                // Move rock position towards player
                float moveSpeed = rockChaseForce * Time.deltaTime;
                rock.transform.position += directionToPlayer * moveSpeed;
            }
        }
    }

    void StoreOriginalRockPositions()
    {
        originalRockPositions.Clear();
        GameObject[] rocks = GameObject.FindGameObjectsWithTag("Rock");
        
        foreach (GameObject rock in rocks)
        {
            originalRockPositions[rock] = rock.transform.position;
        }
    }
    
    void ReturnRocksToOriginalPositions()
    {
        foreach (var kvp in originalRockPositions)
        {
            if (kvp.Key != null) // Check if rock still exists
            {
                kvp.Key.transform.position = kvp.Value;
            }
        }
        originalRockPositions.Clear();
    }

    void OnDestroy()
    {
        invincibilityInput?.Dispose();
    }
}
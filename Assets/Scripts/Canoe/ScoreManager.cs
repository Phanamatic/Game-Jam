using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    [SerializeField] float timeScoreRate = 10f; // Points per second
    [SerializeField] float paddleScoreBonus = 50f; // Points per paddle stroke
    
    [Header("UI")]
    [SerializeField] TextMeshProUGUI scoreText;
    
    private float currentScore = 0f;
    private InputAction paddleInput;
    
    void Awake()
    {
        paddleInput = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        paddleInput.Enable();
    }
    
    void Update()
    {
        // Increase score over time (survival)
        currentScore += timeScoreRate * Time.deltaTime;
        
        // Increase score on paddle (left click)
        if (paddleInput.WasPressedThisFrame())
        {
            currentScore += paddleScoreBonus;
        }
        
        // Update UI
        if (scoreText != null)
        {
            scoreText.text = "Score: " + Mathf.RoundToInt(currentScore).ToString();
        }
    }
    
    void OnDestroy()
    {
        paddleInput?.Dispose();
    }
}
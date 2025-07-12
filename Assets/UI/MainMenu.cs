using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject controlsPanel;

    // Called when "Play" is pressed
    public void PlayGame()
    {
        // Load the next scene in build order
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        SceneManager.LoadScene(nextSceneIndex);
    }

    // Called when "Controls" is pressed
    public void ShowControls()
    {
        controlsPanel.SetActive(true);
    }

    // Called when "Close" is pressed in controls panel
    public void CloseControls() 
    { 
        controlsPanel.SetActive(false);
    }

    // Called when "Quit" is pressed
    public void QuitGame()
    {
        Debug.Log("Quitting the game...");
        Application.Quit();
    }
}

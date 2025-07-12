// Assets/Scripts/FadeInOnLoad.cs
using System.Collections;
using UnityEngine;

public class FadeInOnLoad : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackFader; // Assign full-screen black CanvasGroup (alpha=1 at start)
    [SerializeField] private float fadeTime = 0.8f;  // Fade duration

    private void Start() => StartCoroutine(Fade());

    private IEnumerator Fade()
    {
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            blackFader.alpha = 1f - (t / fadeTime); // 1 ➜ 0
            yield return null;
        }
        blackFader.alpha = 0f;
        // Optional: Destroy(blackFader.gameObject); if you want to remove it after fade
    }
}
// Assets/Scripts/SplashScreenController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashScreenController : MonoBehaviour
{
    [Header("Logo & UI")]
    [SerializeField] private RectTransform logo;    // assign Image RectTransform
    [SerializeField] private CanvasGroup fader;     // assign CanvasGroup on the Image
    [SerializeField] private CanvasGroup additionalImageFader; // assign CanvasGroup on the other image (starts at alpha 1)

    [Header("Timing")]
    [SerializeField] private float fadeInTime   = 0.6f;
    [SerializeField] private float spinTime     = 3.0f;
    [SerializeField] private float postSpinWait = 1.5f;
    [SerializeField] private float fadeOutTime  = 0.6f; // New: time to fade out logo before load

    [Header("Spin")]
    [Tooltip("Complete spins before stopping upright")]
    [SerializeField, Min(0.1f)] private float fullSpins = 3f;

    [Header("Scene to load")]
    [SerializeField] private string nextScene = "MainMenu";

    private void Start() => StartCoroutine(Sequence());

    private IEnumerator Sequence()
    {
        // 1. Fade-in
        for (float t = 0; t < fadeInTime; t += Time.deltaTime)
        {
            fader.alpha = t / fadeInTime;      // 0 ➜ 1
            yield return null;
        }
        fader.alpha = 1f;

        // 2. Stylised spin (ease-out, always ends at 0°)
        for (float t = 0; t < spinTime; t += Time.deltaTime)
        {
            float progress = t / spinTime;                 // 0-1
            // Ease-out cubic (feel free to tweak)
            float eased = 1f - Mathf.Pow(1f - progress, 3);
            float angle  = eased * fullSpins * 360f;       // exact multiple of 360°
            logo.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }
        // Snap perfectly upright
        logo.localRotation = Quaternion.identity;

        // 3. Little squash-and-stretch “pop”
        const float popScale = 1.1f;
        const float popTime  = 0.25f;
        Vector3 original = Vector3.one;
        Vector3 target   = Vector3.one * popScale;

        // scale up
        for (float t = 0; t < popTime; t += Time.deltaTime)
        {
            float k = t / popTime;
            logo.localScale = Vector3.LerpUnclamped(original, target, k);
            yield return null;
        }
        // scale back
        for (float t = 0; t < popTime; t += Time.deltaTime)
        {
            float k = t / popTime;
            logo.localScale = Vector3.LerpUnclamped(target, original, k);
            yield return null;
        }
        logo.localScale = original;

        // 4. Wait
        yield return new WaitForSeconds(postSpinWait);

        // 5. Fade-out (new)
        for (float t = 0; t < fadeOutTime; t += Time.deltaTime)
        {
            fader.alpha = 1f - (t / fadeOutTime);      // 1 ➜ 0 for logo
            additionalImageFader.alpha = 1f - (t / fadeOutTime); // 1 ➜ 0 for other image
            yield return null;
        }
        fader.alpha = 0f;
        additionalImageFader.alpha = 0f;

        // 6. Load next scene
        SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }
}
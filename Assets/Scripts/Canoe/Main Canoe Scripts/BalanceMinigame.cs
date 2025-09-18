using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Simple balancing mini-game inspired by Stardew Valley's fishing bar.
/// A vertical target window drifts based on canoe roll + noise while the player
/// nudges a smaller marker with A/D. Keeping the marker inside the window grants
/// full authority to counter the canoe's wobble; slipping out reduces control.
/// </summary>
[DisallowMultipleComponent]
public class BalanceMinigame : MonoBehaviour
{
    [Header("UI Layout")]
    [SerializeField] Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] Vector2 anchoredPosition   = new(0f, 0f);
    [SerializeField] Vector2 gaugeSize          = new(86f, 320f);
    [SerializeField, Range(0.15f, 0.9f)] float targetWindowFraction = 0.38f;
    [SerializeField, Range(0.08f, 0.6f)] float markerFraction       = 0.18f;

    [Header("Gameplay")]
    [SerializeField] float markerAcceleration = 6.5f;
    [SerializeField] float markerDrag         = 6f;
    [SerializeField] float markerReturn       = 3.5f;
    [SerializeField] float targetDriftSpeed   = 1.15f;
    [SerializeField] float targetNoiseAmp     = 0.55f;
    [SerializeField] float targetNoiseFreq    = 0.8f;
    [SerializeField] float rollInfluence      = 0.65f;
    [SerializeField] float authoritySmooth    = 8f;
    [SerializeField] float dangerAuthority    = 0.35f;
    [SerializeField] Color gaugeColor         = new(0f, 0f, 0f, 0.6f);
    [SerializeField] Color windowColor        = new(0.88f, 0.9f, 0.35f, 0.85f);
    [SerializeField] Color markerSafeColor    = new(0.2f, 0.7f, 1f, 0.85f);
    [SerializeField] Color markerDangerColor  = new(1f, 0.3f, 0.3f, 0.85f);

    Canvas canvas;
    RectTransform gaugeRect;
    RectTransform targetRect;
    RectTransform markerRect;
    Image markerImage;

    float markerPos;   // [-1,1]
    float markerVel;
    float targetPos;   // [-1,1]
    float authority;
    float rollBias;

    float TargetHalf => Mathf.Clamp(targetWindowFraction * 0.5f, 0.08f, 0.45f);
    float MarkerHalf => Mathf.Clamp(markerFraction * 0.5f, 0.04f, 0.45f);

    public float Authority => authority;
    public float ManualLean => Mathf.Clamp(markerPos, -1f, 1f);

    void Awake()
    {
        BuildUI();
    }

    void OnEnable()
    {
        if (!canvas) BuildUI();
        if (canvas) canvas.enabled = true;
    }

    void OnDisable()
    {
        if (canvas) canvas.enabled = false;
    }

    void OnDestroy()
    {
        if (canvas)
        {
            Destroy(canvas.gameObject);
            canvas = null;
        }
    }

    public void SetRollBias(float normalized)
    {
        rollBias = Mathf.Clamp(normalized, -1f, 1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var keyboard = Keyboard.current;
        float input = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) input -= 1f;
            if (keyboard.dKey.isPressed) input += 1f;
        }

        // Player marker integrates acceleration with damping + spring toward centre.
        markerVel += (input * markerAcceleration - markerPos * markerReturn) * dt;
        markerVel = Mathf.MoveTowards(markerVel, 0f, markerDrag * dt);

        float markerLimit = 1f - MarkerHalf;
        markerPos = Mathf.Clamp(markerPos + markerVel * dt, -markerLimit, markerLimit);

        // Target window drifts with roll bias + Perlin noise.
        float noise = Mathf.PerlinNoise(Time.time * targetNoiseFreq, 0.37f) * 2f - 1f;
        float desired = Mathf.Clamp(rollBias * rollInfluence + noise * targetNoiseAmp,
                                    -1f + TargetHalf, 1f - TargetHalf);
        targetPos = Mathf.MoveTowards(targetPos, desired, targetDriftSpeed * dt);

        float distance = Mathf.Abs(markerPos - targetPos);
        float rawAuthority = Mathf.Clamp01((TargetHalf - distance) / Mathf.Max(TargetHalf, 1e-3f));
        float lerp = 1f - Mathf.Exp(-authoritySmooth * dt);
        authority = Mathf.Lerp(authority, rawAuthority, lerp);

        UpdateVisuals();
    }

    void BuildUI()
    {
        if (canvas) return;

        canvas = new GameObject("BalanceMinigameCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;

        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        gaugeRect = new GameObject("BalanceGauge", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        gaugeRect.SetParent(canvas.transform, false);
        gaugeRect.anchorMin = gaugeRect.anchorMax = new Vector2(0.92f, 0.5f);
        gaugeRect.sizeDelta = gaugeSize;
        gaugeRect.anchoredPosition = anchoredPosition;
        var gaugeImage = gaugeRect.GetComponent<Image>();
        gaugeImage.color = gaugeColor;
        gaugeImage.raycastTarget = false;

        targetRect = new GameObject("TargetWindow", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        targetRect.SetParent(gaugeRect, false);
        targetRect.anchorMin = targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.sizeDelta = new Vector2(gaugeSize.x * 0.82f, gaugeSize.y * targetWindowFraction);
        var targetImage = targetRect.GetComponent<Image>();
        targetImage.color = windowColor;
        targetImage.raycastTarget = false;

        markerRect = new GameObject("BalanceMarker", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        markerRect.SetParent(gaugeRect, false);
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(gaugeSize.x * 0.6f, gaugeSize.y * markerFraction);
        markerImage = markerRect.GetComponent<Image>();
        markerImage.color = markerSafeColor;
        markerImage.raycastTarget = false;

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (!gaugeRect || !markerRect || !targetRect) return;

        float gaugeHalf = gaugeRect.sizeDelta.y * 0.5f;
        float markerHalf = markerRect.sizeDelta.y * 0.5f;
        float targetHalf = targetRect.sizeDelta.y * 0.5f;

        markerRect.anchoredPosition = new Vector2(0f, markerPos * Mathf.Max(0f, gaugeHalf - markerHalf));
        targetRect.anchoredPosition = new Vector2(0f, targetPos * Mathf.Max(0f, gaugeHalf - targetHalf));

        float danger = Mathf.Clamp01((dangerAuthority - authority) / Mathf.Max(dangerAuthority, 1e-3f));
        markerImage.color = Color.Lerp(markerSafeColor, markerDangerColor, danger);
    }
}

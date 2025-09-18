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

    [Header("Mini View")]
    [SerializeField] Vector2 miniViewSize = new(220f, 120f);
    [SerializeField] Vector2 miniViewOffset = new(0f, -80f);
    [SerializeField] Color miniBackgroundColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] Color miniWaterColor = new(0.45f, 0.65f, 0.9f, 0.4f);
    [SerializeField] Color miniCanoeColor = new(0.88f, 0.42f, 0.18f, 0.9f);
    [SerializeField] Color miniBubbleSafeColor = new(0.4f, 0.95f, 0.65f, 0.95f);
    [SerializeField] Color miniBubbleDangerColor = new(1f, 0.45f, 0.35f, 0.95f);
    [SerializeField] float miniBubbleRollRange = 28f;
    [SerializeField] float miniCanoeRollMultiplier = 1f;

    [Header("Hit Disruption")]
    [SerializeField] float hitMarkerImpulse = 0.85f;
    [SerializeField] float hitWindowShrink = 0.4f;
    [SerializeField] float hitWindowRecovery = 0.8f;
    [SerializeField] float hitTargetKick = 0.45f;
    [SerializeField] float hitTargetRecovery = 1.4f;
    [SerializeField] float hitJitterDuration = 0.75f;
    [SerializeField] float hitJitterFrequency = 16f;
    [SerializeField] float hitJitterAmplitude = 0.18f;

    Canvas canvas;
    RectTransform gaugeRect;
    RectTransform targetRect;
    RectTransform markerRect;
    Image markerImage;

    RectTransform miniRoot;
    RectTransform miniCanoeRect;
    RectTransform miniBubbleRect;
    Image miniBubbleImage;
    Image miniWaterImage;

    float markerPos;   // [-1,1]
    float markerVel;
    float targetPos;   // [-1,1]
    float targetVisualPos;
    float authority;
    float rollBias;
    float rollDegrees;
    float windowScale = 1f;
    float hitOffset;
    float hitJitterTimer;
    float miniBubbleBaseY;
    Vector2 targetBaseSize;

    float TargetHalf => Mathf.Clamp(targetWindowFraction * windowScale * 0.5f, 0.08f, 0.45f);
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
            miniRoot = null;
            miniCanoeRect = null;
            miniBubbleRect = null;
            miniBubbleImage = null;
            miniWaterImage = null;
        }
    }

    public void SetRollState(float normalized, float rollDeg)
    {
        rollBias = Mathf.Clamp(normalized, -1f, 1f);
        rollDegrees = rollDeg;
    }

    public void SetRollBias(float normalized)
    {
        SetRollState(normalized, Mathf.Clamp(normalized, -1f, 1f) * miniBubbleRollRange);
    }

    public void ApplyHitDisruption(float sideSign, float strength01)
    {
        float strength = Mathf.Clamp01(strength01);
        if (strength <= 0f) return;

        float direction = Mathf.Sign(sideSign);
        if (direction == 0f)
            direction = Mathf.Sign(Random.value - 0.5f);

        markerVel += direction * hitMarkerImpulse * Mathf.Lerp(0.45f, 1.1f, strength);
        windowScale = Mathf.Clamp(windowScale - hitWindowShrink * strength, 0.25f, 1f);
        hitOffset = Mathf.Clamp(hitOffset + direction * hitTargetKick * strength, -0.9f, 0.9f);
        if (hitJitterDuration > 0f)
            hitJitterTimer = Mathf.Max(hitJitterTimer, hitJitterDuration * strength);
        else
            hitJitterTimer = 0f;
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

        if (windowScale < 1f)
            windowScale = Mathf.MoveTowards(windowScale, 1f, hitWindowRecovery * dt);

        if (!Mathf.Approximately(hitOffset, 0f))
            hitOffset = Mathf.MoveTowards(hitOffset, 0f, hitTargetRecovery * dt);

        float windowHalf = TargetHalf;

        // Target window drifts with roll bias, disruption offset, and Perlin noise.
        float noise = Mathf.PerlinNoise(Time.time * targetNoiseFreq, 0.37f) * 2f - 1f;
        float desired = Mathf.Clamp(rollBias * rollInfluence + hitOffset + noise * targetNoiseAmp,
                                    -1f + windowHalf, 1f - windowHalf);
        targetPos = Mathf.MoveTowards(targetPos, desired, targetDriftSpeed * dt);

        float jitter = 0f;
        if (hitJitterTimer > 0f)
        {
            hitJitterTimer = Mathf.Max(0f, hitJitterTimer - dt);
            float normalized = hitJitterDuration > 1e-4f ? hitJitterTimer / hitJitterDuration : 0f;
            jitter = Mathf.Sin(Time.time * hitJitterFrequency * Mathf.PI * 2f) * hitJitterAmplitude * normalized;
        }

        targetVisualPos = Mathf.Clamp(targetPos + jitter, -1f + windowHalf, 1f - windowHalf);

        float distance = Mathf.Abs(markerPos - targetVisualPos);
        float rawAuthority = Mathf.Clamp01((windowHalf - distance) / Mathf.Max(windowHalf, 1e-3f));
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
        targetBaseSize = targetRect.sizeDelta;
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

        BuildMiniView();
        UpdateVisuals();
    }

    void BuildMiniView()
    {
        if (!canvas || miniRoot) return;

        miniRoot = new GameObject("BalanceMiniView", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        miniRoot.SetParent(canvas.transform, false);
        miniRoot.anchorMin = miniRoot.anchorMax = new Vector2(0.5f, 1f);
        miniRoot.pivot = new Vector2(0.5f, 1f);
        miniRoot.sizeDelta = miniViewSize;
        miniRoot.anchoredPosition = miniViewOffset;
        var bg = miniRoot.GetComponent<Image>();
        bg.color = miniBackgroundColor;
        bg.raycastTarget = false;

        var waterRect = new GameObject("MiniWater", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        waterRect.SetParent(miniRoot, false);
        waterRect.anchorMin = new Vector2(0f, 0.5f);
        waterRect.anchorMax = new Vector2(1f, 0.5f);
        waterRect.sizeDelta = new Vector2(0f, Mathf.Max(6f, miniViewSize.y * 0.12f));
        miniWaterImage = waterRect.GetComponent<Image>();
        miniWaterImage.color = miniWaterColor;
        miniWaterImage.raycastTarget = false;

        miniCanoeRect = new GameObject("MiniCanoe", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        miniCanoeRect.SetParent(miniRoot, false);
        miniCanoeRect.anchorMin = miniCanoeRect.anchorMax = new Vector2(0.5f, 0.38f);
        miniCanoeRect.sizeDelta = new Vector2(miniViewSize.x * 0.6f, Mathf.Max(8f, miniViewSize.y * 0.18f));
        var canoeImage = miniCanoeRect.GetComponent<Image>();
        canoeImage.color = miniCanoeColor;
        canoeImage.raycastTarget = false;

        miniBubbleRect = new GameObject("BalanceBubble", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        miniBubbleRect.SetParent(miniRoot, false);
        miniBubbleRect.anchorMin = miniBubbleRect.anchorMax = new Vector2(0.5f, 0.74f);
        miniBubbleRect.sizeDelta = new Vector2(Mathf.Max(10f, miniViewSize.y * 0.18f), Mathf.Max(10f, miniViewSize.y * 0.18f));
        miniBubbleRect.anchoredPosition = new Vector2(0f, Mathf.Max(6f, miniViewSize.y * 0.26f));
        miniBubbleBaseY = miniBubbleRect.anchoredPosition.y;
        miniBubbleImage = miniBubbleRect.GetComponent<Image>();
        miniBubbleImage.color = miniBubbleSafeColor;
        miniBubbleImage.raycastTarget = false;

        UpdateMiniView();
    }

    void UpdateVisuals()
    {
        if (!gaugeRect || !markerRect || !targetRect) return;

        float gaugeHalf = gaugeRect.sizeDelta.y * 0.5f;

        markerRect.sizeDelta = new Vector2(gaugeRect.sizeDelta.x * 0.6f, gaugeRect.sizeDelta.y * markerFraction);
        float markerHalf = markerRect.sizeDelta.y * 0.5f;
        markerRect.anchoredPosition = new Vector2(0f, markerPos * Mathf.Max(0f, gaugeHalf - markerHalf));

        float clampedScale = Mathf.Clamp(windowScale, 0.2f, 1.4f);
        targetRect.sizeDelta = new Vector2(targetBaseSize.x, targetBaseSize.y * clampedScale);
        float targetHalf = targetRect.sizeDelta.y * 0.5f;
        targetRect.anchoredPosition = new Vector2(0f, targetVisualPos * Mathf.Max(0f, gaugeHalf - targetHalf));

        float danger = Mathf.Clamp01((dangerAuthority - authority) / Mathf.Max(dangerAuthority, 1e-3f));
        markerImage.color = Color.Lerp(markerSafeColor, markerDangerColor, danger);

        UpdateMiniView();
    }

    void UpdateMiniView()
    {
        if (!miniRoot) return;

        if (miniCanoeRect)
            miniCanoeRect.localRotation = Quaternion.Euler(0f, 0f, -rollDegrees * miniCanoeRollMultiplier);

        if (miniBubbleRect)
        {
            float range = Mathf.Max(1f, miniBubbleRollRange);
            float normalized = Mathf.Clamp(rollDegrees / range, -1f, 1f);
            float travel = Mathf.Max(0f, (miniViewSize.x * 0.5f) - (miniBubbleRect.sizeDelta.x * 0.5f) - 4f);
            miniBubbleRect.anchoredPosition = new Vector2(normalized * travel, miniBubbleBaseY);
        }

        if (miniBubbleImage)
        {
            float danger = Mathf.Clamp01((dangerAuthority - authority) / Mathf.Max(dangerAuthority, 1e-3f));
            miniBubbleImage.color = Color.Lerp(miniBubbleSafeColor, miniBubbleDangerColor, danger);
        }

        if (miniWaterImage)
        {
            float wob = Mathf.Sin(Time.time * 1.2f) * 0.5f + 0.5f;
            float mul = Mathf.Lerp(0.9f, 1.05f, wob);
            Color wobble = miniWaterColor;
            wobble.r *= mul;
            wobble.g *= mul;
            wobble.b *= mul;
            miniWaterImage.color = wobble;
        }
    }
}

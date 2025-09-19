using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

/* BalanceMinigame (horizontal)
 * - Gauge runs left↔right instead of vertical.
 * - Same Authority/ManualLean API.
 */
[DisallowMultipleComponent]
public class BalanceMinigame : MonoBehaviour
{
    [Header("UI Layout")]
    [SerializeField] Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] Vector2 anchoredPosition   = new(0f, 0f);
    [SerializeField] Vector2 gaugeSize          = new(640f, 120f); // width, height
    [SerializeField] Vector2 gaugeAnchor        = new(0.5f, 0.18f);
    [SerializeField] float gaugeFramePadding    = 28f;
    [SerializeField] Color gaugeFrameColor      = new(0f, 0f, 0f, 0.45f);
    [SerializeField] Color gaugeFillColor       = new(1f, 1f, 1f, 0.12f);
    [SerializeField, Range(0.15f, 0.9f)] float targetWindowFraction = 0.38f;
    [SerializeField, Range(0.08f, 0.6f)] float markerFraction       = 0.18f;

    [Header("Marker Motion (2nd-order)")]
    [SerializeField] float markerHz = 3.0f;
    [SerializeField, Range(0.5f, 2f)] float markerZeta = 1.0f;
    [SerializeField] float inputGain = 2.2f;
    [SerializeField] float recenterGain = 0.8f;

    [Header("Target Drift")]
    [SerializeField] float targetDriftSpeed = 1.15f;
    [SerializeField] float targetNoiseAmp   = 0.45f;
    [SerializeField] float targetNoiseFreq  = 0.8f;
    [SerializeField] float rollInfluence    = 0.65f;
    [SerializeField] float rollRateBoost    = 0.4f;
    [SerializeField] float rollRateLowpass  = 8f;

    [Header("Authority")]
    [SerializeField] float authoritySmooth = 8f;
    [SerializeField] float dangerAuthority = 0.35f;
    [SerializeField, Range(1f,3f)] float authorityCurve = 2f;

    [Header("Colors")]
    [SerializeField] Color gaugeColor        = new(0f, 0f, 0f, 0.6f);
    [SerializeField] Color windowColor       = new(0.88f, 0.9f, 0.35f, 0.85f);
    [SerializeField] Color markerSafeColor   = new(0.2f, 0.7f, 1f, 0.85f);
    [SerializeField] Color markerDangerColor = new(1f, 0.3f, 0.3f, 0.85f);

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
    [SerializeField] float hitMarkerImpulse   = 0.85f;
    [SerializeField] float hitWindowShrink    = 0.4f;
    [SerializeField] float hitWindowRecovery  = 0.8f;
    [SerializeField] float hitTargetKick      = 0.45f;
    [SerializeField] float hitTargetRecovery  = 1.4f;
    [SerializeField] float hitJitterDuration  = 0.75f;
    [SerializeField] float hitJitterFrequency = 16f;
    [SerializeField] float hitJitterAmplitude = 0.18f;

    Canvas canvas;
    RectTransform frameRect, gaugeRect, targetRect, markerRect, gaugeFillRect;
    Image markerImage, gaugeFillImage;

    RectTransform miniRoot, miniCanoeRect, miniBubbleRect;
    Image miniBubbleImage, miniWaterImage;

    // Marker state in horizontal normalized space [-1,1]
    float markerPos, markerVel, markerTarget;

    // Target
    float targetPos, targetVisualPos, targetRollRate;

    // Meters
    float authority, rollBias, rollDegrees, windowScale = 1f, hitOffset, hitJitterTimer;
    float instability, targetOffset;
    float miniBubbleBaseY;
    Vector2 targetBaseSize;

    float TargetHalf => Mathf.Clamp(targetWindowFraction * windowScale * 0.5f, 0.08f, 0.45f);
    float MarkerHalf => Mathf.Clamp(markerFraction * 0.5f, 0.04f, 0.45f);

    public float Authority => authority;
    public float ManualLean => Mathf.Clamp(markerPos, -1f, 1f);
    public float Instability => Mathf.Clamp01(instability);
    public float TargetOffset => targetOffset;
    public float RollBias => rollBias;
    public float MarkerNormalized => markerPos;
    public float TargetNormalized => targetVisualPos;
    public bool  InsideWindow => wasInside;

    public event Action<float> OnAuthorityChanged;
    public event Action<bool>  OnInsideWindowChanged;
    bool wasInside;
    float _prevRollBias;

    void Awake(){ BuildUI(); }
    void OnEnable(){ if (!canvas) BuildUI(); if (canvas) canvas.enabled = true; }
    void OnDisable(){ if (canvas) canvas.enabled = false; }
    void OnDestroy()
    {
        if (!canvas) return;
        Destroy(canvas.gameObject); canvas=null;
        frameRect=gaugeRect=targetRect=markerRect=gaugeFillRect=null;
        miniRoot=miniCanoeRect=miniBubbleRect=null;
        miniBubbleImage=miniWaterImage=markerImage=gaugeFillImage=null;
    }

    // —— Public API ——
    public void SetRollState(float normalized, float rollDeg){ rollBias = Mathf.Clamp(normalized,-1f,1f); rollDegrees = rollDeg; }
    public void SetRollBias(float normalized){ SetRollState(normalized, Mathf.Clamp(normalized,-1f,1f)*miniBubbleRollRange); }
    public void ApplyHitDisruption(float sideSign, float strength01)
    {
        float s = Mathf.Clamp01(strength01); if (s<=0f) return;
        float dir = Mathf.Sign(sideSign); if (dir==0f) dir = Mathf.Sign(UnityEngine.Random.value-0.5f);
        markerVel += dir * hitMarkerImpulse * Mathf.Lerp(0.45f, 1.1f, s);
        windowScale = Mathf.Clamp(windowScale - hitWindowShrink * s, 0.25f, 1f);
        hitOffset = Mathf.Clamp(hitOffset + dir * hitTargetKick * s, -0.9f, 0.9f);
        hitJitterTimer = Mathf.Max(hitJitterTimer, hitJitterDuration * s);
    }
    public void ResetMinigame()
    {
        markerPos=markerVel=markerTarget=0f; targetPos=targetVisualPos=0f;
        authority=1f; windowScale=1f; hitOffset=0f; hitJitterTimer=0f;
        instability = 0f; targetOffset = 0f;
        UpdateVisuals(); RaiseInsideWindow(true); OnAuthorityChanged?.Invoke(authority);
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt<=0f) return;

        // Input
        float input = ReadHorizontalInput();
        markerTarget += (input * inputGain - markerTarget * recenterGain) * dt;
        markerTarget = Mathf.Clamp(markerTarget, -1f + MarkerHalf, 1f - MarkerHalf);

        // 2nd-order to target
        float w = Mathf.Max(0.1f, markerHz) * (2f*Mathf.PI);
        float z = markerZeta;
        markerVel += (-2f*z*w*markerVel + w*w*(markerTarget - markerPos)) * dt;
        markerPos += markerVel * dt;

        float mLimit = 1f - MarkerHalf;
        if (markerPos<-mLimit){ markerPos=-mLimit; markerVel=0f; }
        if (markerPos> mLimit){ markerPos= mLimit; markerVel=0f; }

        // Recovery
        if (windowScale<1f) windowScale = Mathf.MoveTowards(windowScale,1f, hitWindowRecovery*dt);
        if (!Mathf.Approximately(hitOffset,0f)) hitOffset = Mathf.MoveTowards(hitOffset,0f, hitTargetRecovery*dt);

        // Target motion
        float windowHalf = TargetHalf;
        float biasRate = (rollBias - _prevRollBias) / Mathf.Max(1e-6f, dt);
        _prevRollBias = rollBias;
        targetRollRate = Mathf.Lerp(targetRollRate, Mathf.Abs(biasRate), 1f - Mathf.Exp(-rollRateLowpass * dt));

        float noise = Mathf.PerlinNoise(Time.time * targetNoiseFreq, 0.37f)*2f-1f;
        float desired = Mathf.Clamp(rollBias * rollInfluence
                                    + Mathf.Clamp01(targetRollRate) * rollRateBoost
                                    + hitOffset
                                    + noise * targetNoiseAmp,
                                    -1f + windowHalf, 1f - windowHalf);
        targetPos = Mathf.MoveTowards(targetPos, desired, targetDriftSpeed * dt);

        float jitter=0f;
        if (hitJitterTimer>0f){
            hitJitterTimer=Mathf.Max(0f, hitJitterTimer-dt);
            float n = hitJitterDuration>1e-4f ? hitJitterTimer/hitJitterDuration : 0f;
            jitter = Mathf.Sin(Time.time * hitJitterFrequency * Mathf.PI * 2f) * hitJitterAmplitude * n;
        }
        targetVisualPos = Mathf.Clamp(targetPos + jitter, -1f + windowHalf, 1f - windowHalf);

        // Authority
        float dist = Mathf.Abs(markerPos - targetVisualPos);
        float raw = Mathf.Clamp01((windowHalf - dist)/Mathf.Max(windowHalf,1e-3f));
        instability = 1f - raw;
        targetOffset = targetVisualPos - markerPos;
        float curved = Mathf.Pow(raw, authorityCurve);
        float aLerp = 1f - Mathf.Exp(-authoritySmooth * dt);
        float prev = authority;
        authority = Mathf.Lerp(authority, curved, aLerp);

        bool inside = raw > 1e-3f;
        if (inside != wasInside) RaiseInsideWindow(inside);
        if (!Mathf.Approximately(prev, authority)) OnAuthorityChanged?.Invoke(authority);

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
        scaler.matchWidthOrHeight = 0.5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        frameRect = new GameObject("BalanceGaugeFrame", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        frameRect.SetParent(canvas.transform, false);
        frameRect.anchorMin = frameRect.anchorMax = gaugeAnchor;
        frameRect.sizeDelta = gaugeSize + new Vector2(gaugeFramePadding * 2f, gaugeFramePadding * 2f);
        frameRect.anchoredPosition = anchoredPosition;
        var frameImage = frameRect.GetComponent<Image>();
        frameImage.color = gaugeFrameColor; frameImage.raycastTarget = false;

        gaugeRect = new GameObject("BalanceGauge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        gaugeRect.SetParent(frameRect, false);
        gaugeRect.anchorMin = gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeRect.sizeDelta = gaugeSize;
        gaugeRect.anchoredPosition = Vector2.zero;
        var gaugeImage = gaugeRect.GetComponent<Image>();
        gaugeImage.color = gaugeColor; gaugeImage.raycastTarget = false;

        gaugeFillRect = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        gaugeFillRect.SetParent(gaugeRect, false);
        gaugeFillRect.anchorMin = new Vector2(0f, 0.5f);
        gaugeFillRect.anchorMax = new Vector2(1f, 0.5f);
        gaugeFillRect.sizeDelta = new Vector2(0f, gaugeSize.y * 0.45f);
        gaugeFillRect.anchoredPosition = Vector2.zero;
        gaugeFillImage = gaugeFillRect.GetComponent<Image>();
        gaugeFillImage.color = gaugeFillColor; gaugeFillImage.raycastTarget = false;

        targetRect = new GameObject("TargetWindow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        targetRect.SetParent(gaugeRect, false);
        targetRect.anchorMin = targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        // horizontal: width scales, height fixed
        targetRect.sizeDelta = new Vector2(gaugeSize.x * targetWindowFraction, gaugeSize.y * 0.82f);
        targetBaseSize = targetRect.sizeDelta;
        var targetImage = targetRect.GetComponent<Image>();
        targetImage.color = windowColor; targetImage.raycastTarget = false;

        markerRect = new GameObject("BalanceMarker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        markerRect.SetParent(gaugeRect, false);
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(gaugeSize.x * markerFraction, gaugeSize.y * 0.6f);
        markerImage = markerRect.GetComponent<Image>();
        markerImage.color = markerSafeColor; markerImage.raycastTarget = false;

        BuildMiniView();
        UpdateVisuals();
    }

    void BuildMiniView()
    {
        if (!canvas || miniRoot) return;
        miniRoot = new GameObject("BalanceMiniView", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        miniRoot.SetParent(canvas.transform, false);
        miniRoot.anchorMin = miniRoot.anchorMax = new Vector2(0.5f, 1f);
        miniRoot.pivot = new Vector2(0.5f, 1f);
        miniRoot.sizeDelta = miniViewSize;
        miniRoot.anchoredPosition = miniViewOffset;
        var bg = miniRoot.GetComponent<Image>(); bg.color = miniBackgroundColor; bg.raycastTarget=false;

        var waterRect = new GameObject("MiniWater", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        waterRect.SetParent(miniRoot, false);
        waterRect.anchorMin = new Vector2(0f, 0.5f);
        waterRect.anchorMax = new Vector2(1f, 0.5f);
        waterRect.sizeDelta = new Vector2(0f, Mathf.Max(6f, miniViewSize.y * 0.12f));
        miniWaterImage = waterRect.GetComponent<Image>();
        miniWaterImage.color = miniWaterColor; miniWaterImage.raycastTarget = false;

        miniCanoeRect = new GameObject("MiniCanoe", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        miniCanoeRect.SetParent(miniRoot, false);
        miniCanoeRect.anchorMin = miniCanoeRect.anchorMax = new Vector2(0.5f, 0.38f);
        miniCanoeRect.sizeDelta = new Vector2(miniViewSize.x * 0.6f, Mathf.Max(8f, miniViewSize.y * 0.18f));
        var canoeImage = miniCanoeRect.GetComponent<Image>();
        canoeImage.color = miniCanoeColor; canoeImage.raycastTarget = false;

        miniBubbleRect = new GameObject("BalanceBubble", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        miniBubbleRect.SetParent(miniRoot, false);
        miniBubbleRect.anchorMin = miniBubbleRect.anchorMax = new Vector2(0.5f, 0.74f);
        miniBubbleRect.sizeDelta = new Vector2(Mathf.Max(10f, miniViewSize.y * 0.18f), Mathf.Max(10f, miniViewSize.y * 0.18f));
        miniBubbleRect.anchoredPosition = new Vector2(0f, Mathf.Max(6f, miniViewSize.y * 0.26f));
        miniBubbleBaseY = miniBubbleRect.anchoredPosition.y;
        miniBubbleImage = miniBubbleRect.GetComponent<Image>();
        miniBubbleImage.color = miniBubbleSafeColor; miniBubbleImage.raycastTarget = false;

        UpdateMiniView();
    }

    void UpdateVisuals()
    {
        if (!gaugeRect || !markerRect || !targetRect) return;

        if (frameRect)
        {
            frameRect.anchorMin = frameRect.anchorMax = gaugeAnchor;
            frameRect.sizeDelta = gaugeSize + new Vector2(gaugeFramePadding * 2f, gaugeFramePadding * 2f);
            frameRect.anchoredPosition = anchoredPosition;
        }

        gaugeRect.sizeDelta = gaugeSize;

        if (gaugeFillRect)
            gaugeFillRect.sizeDelta = new Vector2(0f, gaugeSize.y * 0.45f);

        float gaugeHalf = gaugeRect.sizeDelta.x * 0.5f; // horizontal extent

        // Marker width scales; move along X
        markerRect.sizeDelta = new Vector2(gaugeRect.sizeDelta.x * markerFraction, gaugeRect.sizeDelta.y * 0.6f);
        float markerHalf = markerRect.sizeDelta.x * 0.5f;
        markerRect.anchoredPosition = new Vector2(markerPos * Mathf.Max(0f, gaugeHalf - markerHalf), 0f);

        // Window width scales with windowScale
        float clampedScale = Mathf.Clamp(windowScale, 0.2f, 1.4f);
        targetRect.sizeDelta = new Vector2(targetBaseSize.x * clampedScale, targetBaseSize.y);
        float targetHalf = targetRect.sizeDelta.x * 0.5f;
        targetRect.anchoredPosition = new Vector2(targetVisualPos * Mathf.Max(0f, gaugeHalf - targetHalf), 0f);

        // Colors
        float danger = Mathf.Clamp01((dangerAuthority - authority) / Mathf.Max(dangerAuthority, 1e-3f));
        markerImage.color = Color.Lerp(markerSafeColor, markerDangerColor, danger);

        if (gaugeFillImage)
        {
            float wobble = Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(instability));
            Color fill = gaugeFillColor;
            fill.a *= wobble;
            gaugeFillImage.color = fill;
        }

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
            Color c = miniWaterColor; c.r *= mul; c.g *= mul; c.b *= mul;
            miniWaterImage.color = c;
        }
    }

    float ReadHorizontalInput()
    {
        float x = 0f;
        var k = Keyboard.current;
        if (k != null){ if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f; if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f; }
        var g = Gamepad.current; if (g != null) x += g.leftStick.x.ReadValue();
        return Mathf.Clamp(x, -1f, 1f);
    }
    void RaiseInsideWindow(bool inside){ wasInside = inside; OnInsideWindowChanged?.Invoke(inside); }
}

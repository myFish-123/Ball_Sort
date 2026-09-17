using UnityEngine;
using System.Collections;

/// <summary>
/// 自适应缩放组件，支持按宽度或高度进行屏幕适配
/// </summary>
public class AdaptiveScaler : MonoBehaviour
{
    public enum ScaleMode { FitWidth, FitHeight }

    [Header("设计分辨率")]
    public float designWidth = 1080f;
    public float designHeight = 1920f;

    [Header("适配设置")]
    public ScaleMode scaleMode = ScaleMode.FitWidth;

    [Header("缩放限制")]
    [Range(0f, 2f)] public float minScale = 0.5f;
    [Range(0f, 3f)] public float maxScale = 2f;

    [Header("运行设置")]
    public bool autoScaleOnStart = true;
    public bool continuousUpdate = false;

    private Vector3 originalScale;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float currentScaleFactor = 1f;

    void Awake()
    {
        originalScale = transform.localScale;
        if (originalScale.x == 0 || originalScale.y == 0 || originalScale.z == 0)
            originalScale = Vector3.one;
    }

    void Start()
    {
        if (autoScaleOnStart)
            ApplyAdaptiveScale();

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Update()
    {
        if (continuousUpdate && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
        {
            ApplyAdaptiveScale();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            StartCoroutine(DelayedResolutionCheck());
    }

    IEnumerator DelayedResolutionCheck()
    {
        yield return null;
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyAdaptiveScale();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    private void ValidateScaleLimits()
    {
        if (minScale > 0 && maxScale > 0 && minScale > maxScale)
        {
            float temp = minScale;
            minScale = maxScale;
            maxScale = temp;
        }
    }

    public void ApplyAdaptiveScale()
    {
        float currentAspectRatio = (float)Screen.width / Screen.height;
        float designAspectRatio = designWidth / designHeight;
        float scaleFactor = 1f;

        switch (scaleMode)
        {
            case ScaleMode.FitWidth:
                scaleFactor = currentAspectRatio / designAspectRatio;
                break;

            case ScaleMode.FitHeight:
                scaleFactor = designAspectRatio / currentAspectRatio;
                break;
        }

        // 应用缩放限制
        if (minScale > 0 && scaleFactor < minScale)
            scaleFactor = minScale;
        if (maxScale > 0 && scaleFactor > maxScale)
            scaleFactor = maxScale;

        currentScaleFactor = scaleFactor;
        transform.localScale = originalScale * currentScaleFactor;
    }

    public void ResetScale()
    {
        transform.localScale = originalScale;
        currentScaleFactor = 1f;
    }

    public float GetCurrentScaleFactor() => currentScaleFactor;

    public void SetDesignResolution(float width, float height)
    {
        designWidth = width;
        designHeight = height;
        ApplyAdaptiveScale();
    }

    public void SetScaleMode(ScaleMode mode)
    {
        scaleMode = mode;
        ApplyAdaptiveScale();
    }

    public void SetScaleLimits(float min, float max)
    {
        minScale = min;
        maxScale = max;
        ValidateScaleLimits();
        ApplyAdaptiveScale();
    }

    public bool IsScaled() => !Mathf.Approximately(currentScaleFactor, 1f);

    public Vector2 GetEffectiveResolution() => new Vector2(Screen.width / currentScaleFactor, Screen.height / currentScaleFactor);
}
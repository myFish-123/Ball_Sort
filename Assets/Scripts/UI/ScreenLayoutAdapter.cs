using UnityEngine;

[DisallowMultipleComponent]
public class ScreenLayoutAdapter : MonoBehaviour
{
    public enum LayoutOrientation
    {
        Portrait,
        Landscape
    }

    public enum CameraAdaptMode
    {
        Auto,
        FieldOfView,
        OrthographicSize
    }

    [System.Serializable]
    public class UiLayoutSettings
    {
        [Tooltip("竖屏时目标 UI 的 anchoredPosition。")]
        public Vector2 portraitAnchoredPosition;
        [Tooltip("横屏时目标 UI 的 anchoredPosition。")]
        public Vector2 landscapeAnchoredPosition;
    }

    [System.Serializable]
    public class CameraLayoutSettings
    {
        [Tooltip("竖屏时透视相机使用的 Field Of View。")]
        public float portraitFieldOfView = 60f;
        [Tooltip("横屏时透视相机使用的 Field Of View。")]
        public float landscapeFieldOfView = 60f;
        [Tooltip("竖屏时正交相机使用的 Orthographic Size。")]
        public float portraitOrthographicSize = 5f;
        [Tooltip("横屏时正交相机使用的 Orthographic Size。")]
        public float landscapeOrthographicSize = 5f;
        [Tooltip("竖屏时至少要看到的世界宽度；小于等于 0 时不限制。")]
        public float portraitMinWorldWidth = 6.75f;
        [Tooltip("横屏时至少要看到的世界宽度；小于等于 0 时不限制。")]
        public float landscapeMinWorldWidth = 0f;
    }

    [Header("Targets")]
    [Tooltip("顶部 UI 容器，竖屏锚到顶部中间，横屏锚到左侧中间。")]
    public RectTransform topUi;
    [Tooltip("游戏主体根节点，预留给布局扩展使用。")]
    public Transform gameRoot;
    [Tooltip("底部 UI 容器，竖屏锚到底部中间，横屏锚到右侧中间。")]
    public RectTransform bottomUi;
    [Tooltip("需要随横竖屏调整参数的相机。")]
    public Camera targetCamera;

    [Header("Top UI Layout")]
    [Tooltip("顶部 UI 在竖屏和横屏下的位置配置。")]
    public UiLayoutSettings topUiLayout = new UiLayoutSettings();

    [Header("Bottom UI Layout")]
    [Tooltip("底部 UI 在竖屏和横屏下的位置配置。")]
    public UiLayoutSettings bottomUiLayout = new UiLayoutSettings();

    [Header("Camera Layout")]
    [Tooltip("相机适配模式；Auto 会根据相机是否正交自动选择。")]
    public CameraAdaptMode cameraAdaptMode = CameraAdaptMode.Auto;
    [Tooltip("相机在竖屏和横屏下的视野/正交尺寸配置。")]
    public CameraLayoutSettings cameraLayout = new CameraLayoutSettings();

    [Header("Behavior")]
    [Tooltip("宽高比 >= 该值时判定为横屏，否则判定为竖屏")]
    [Min(0.01f)]
    public float landscapeAspectThreshold = 1f;
    [Tooltip("组件启用时是否立即应用一次当前布局。")]
    public bool applyOnEnable = true;
    [Tooltip("是否持续监听屏幕宽高变化，并在变化时重新应用布局。")]
    public bool keepWatchingResolution = true;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private LayoutOrientation? lastAppliedOrientation;

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyCurrentLayout(true);
        }
    }

    private void Update()
    {
        if (!keepWatchingResolution)
        {
            return;
        }

        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
        {
            ApplyCurrentLayout(false);
        }
    }

    [ContextMenu("Apply Current Layout")]
    public void ApplyCurrentLayout()
    {
        ApplyCurrentLayout(true);
    }

    private void ApplyCurrentLayout(bool force)
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        LayoutOrientation orientation = GetCurrentOrientation();
        if (!force && lastAppliedOrientation == orientation)
        {
            return;
        }

        ApplyUiLayout(topUi, topUiLayout, orientation, true);
        ApplyUiLayout(bottomUi, bottomUiLayout, orientation, false);
        ApplyCameraLayout(orientation);

        lastAppliedOrientation = orientation;
    }

    private LayoutOrientation GetCurrentOrientation()
    {
        if (Screen.height <= 0)
        {
            return LayoutOrientation.Portrait;
        }

        float threshold = Mathf.Max(0.01f, landscapeAspectThreshold);
        float aspect = (float)Screen.width / Screen.height;
        return aspect >= threshold
            ? LayoutOrientation.Landscape
            : LayoutOrientation.Portrait;
    }

    private void ApplyUiLayout(
        RectTransform target,
        UiLayoutSettings settings,
        LayoutOrientation orientation,
        bool isTopUi)
    {
        if (target == null)
        {
            return;
        }

        if (isTopUi)
        {
            if (orientation == LayoutOrientation.Portrait)
            {
                SetAnchors(target, new Vector2(0.5f, 1f));
            }
            else
            {
                SetAnchors(target, new Vector2(0f, 0.5f));
            }
        }
        else
        {
            if (orientation == LayoutOrientation.Portrait)
            {
                SetAnchors(target, new Vector2(0.5f, 0f));
            }
            else
            {
                SetAnchors(target, new Vector2(1f, 0.5f));
            }
        }

        target.anchoredPosition = orientation == LayoutOrientation.Portrait
            ? settings.portraitAnchoredPosition
            : settings.landscapeAnchoredPosition;
    }

    private void ApplyCameraLayout(LayoutOrientation orientation)
    {
        if (targetCamera == null)
        {
            return;
        }

        CameraAdaptMode resolvedMode = cameraAdaptMode;
        if (resolvedMode == CameraAdaptMode.Auto)
        {
            resolvedMode = targetCamera.orthographic
                ? CameraAdaptMode.OrthographicSize
                : CameraAdaptMode.FieldOfView;
        }

        if (resolvedMode == CameraAdaptMode.OrthographicSize)
        {
            float orthographicSize = orientation == LayoutOrientation.Portrait
                ? cameraLayout.portraitOrthographicSize
                : cameraLayout.landscapeOrthographicSize;
            float minWorldWidth = orientation == LayoutOrientation.Portrait
                ? cameraLayout.portraitMinWorldWidth
                : cameraLayout.landscapeMinWorldWidth;

            targetCamera.orthographicSize = GetOrthographicSizeForMinWidth(orthographicSize, minWorldWidth);
            return;
        }

        targetCamera.fieldOfView = orientation == LayoutOrientation.Portrait
            ? cameraLayout.portraitFieldOfView
            : cameraLayout.landscapeFieldOfView;
    }

    private void SetAnchors(RectTransform target, Vector2 anchor)
    {
        target.anchorMin = anchor;
        target.anchorMax = anchor;
        target.pivot = anchor;
    }

    private float GetOrthographicSizeForMinWidth(float baseSize, float minWorldWidth)
    {
        if (minWorldWidth <= 0f || Screen.width <= 0 || Screen.height <= 0)
        {
            return baseSize;
        }

        float aspect = (float)Screen.width / Screen.height;
        if (aspect <= 0f)
        {
            return baseSize;
        }

        float sizeForWidth = minWorldWidth / (2f * aspect);
        return Mathf.Max(baseSize, sizeForWidth);
    }
}

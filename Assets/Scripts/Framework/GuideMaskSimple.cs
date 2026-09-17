using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 简化版新手引导遮罩（纯C#实现，不依赖自定义Shader）
/// 通过动态生成带镂空的纹理实现遮罩效果
/// 适用于 Luna Playable Ads 等对 Shader 支持有限的环境
/// </summary>
[RequireComponent(typeof(RawImage))]
public class GuideMaskSimple : MonoBehaviour
{
    /// <summary>
    /// 尺寸计算模式
    /// </summary>
    public enum SizeMode
    {
        FixedPixel,      // 固定像素尺寸（手动指定）
        AutoFromBounds,  // 自动从 Renderer/Collider 的 Bounds 计算
        AutoFromRect,    // 自动从 RectTransform 计算（UI专用）
        WorldUnits       // 世界单位尺寸（手动指定，自动转屏幕像素）
    }

    [Header("遮罩设置")]
    [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private int textureSize = 256;
    [SerializeField] private float edgeSoftness = 0.02f;

    [Header("目标设置")]
    [SerializeField] private Transform target;
    [SerializeField] private SizeMode sizeMode = SizeMode.AutoFromBounds;
    [SerializeField] private Vector2 fixedSize = new Vector2(100, 100);
    [SerializeField] private float fixedRadius = 50f;
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private Vector2 padding = Vector2.zero;
    [SerializeField] private Vector2 autoSizeScale = Vector2.zero;
    [SerializeField] private bool isCircle = false;

    private RawImage rawImage;
    private Texture2D maskTexture;
    private Camera mainCamera;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private Vector2 lastHoleCenter;
    private Vector2 lastHoleSize;
    private bool lastIsCircle;
    private bool needsUpdate = true;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        mainCamera = Camera.main;
        parentCanvas = GetComponentInParent<Canvas>();

        CreateTexture();
    }

    void Start()
    {
        UpdateMask();
    }

    void LateUpdate()
    {
        UpdateMask();
    }

    void OnDestroy()
    {
        if (maskTexture != null)
        {
            Destroy(maskTexture);
            maskTexture = null;
        }
    }

    private void CreateTexture()
    {
        maskTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        maskTexture.wrapMode = TextureWrapMode.Clamp;
        maskTexture.filterMode = FilterMode.Bilinear;
        rawImage.texture = maskTexture;
    }

    private void UpdateMask()
    {
        Vector2 holeCenter;
        Vector2 holeSizeNormalized;

        float screenW = Screen.width;
        float screenH = Screen.height;

        if (target != null)
        {
            Vector2 size = CalculateSizeByMode();
            Vector2 autoSizeExtra = Vector2.zero;
            if (sizeMode == SizeMode.AutoFromBounds || sizeMode == SizeMode.AutoFromRect)
            {
                autoSizeExtra = Vector2.Scale(size, autoSizeScale);
            }
            size += padding * 2 + autoSizeExtra;
            size = new Vector2(Mathf.Max(0, size.x), Mathf.Max(0, size.y));

            Vector2 screenPos = GetScreenPosition(target) + GetCenterOffsetInPixels(size);
            holeCenter = new Vector2(screenPos.x / screenW, screenPos.y / screenH);
            holeSizeNormalized = new Vector2(size.x / screenW, size.y / screenH);
        }
        else
        {
            holeCenter = new Vector2(-1, -1);
            holeSizeNormalized = Vector2.zero;
        }

        if (holeCenter != lastHoleCenter || holeSizeNormalized != lastHoleSize || isCircle != lastIsCircle || needsUpdate)
        {
            GenerateMaskTexture(holeCenter, holeSizeNormalized, isCircle);
            lastHoleCenter = holeCenter;
            lastHoleSize = holeSizeNormalized;
            lastIsCircle = isCircle;
            needsUpdate = false;
        }
    }

    private void GenerateMaskTexture(Vector2 center, Vector2 size, bool circle)
    {
        Color32[] pixels = new Color32[textureSize * textureSize];
        Color32 maskCol = new Color32(
            (byte)(maskColor.r * 255),
            (byte)(maskColor.g * 255),
            (byte)(maskColor.b * 255),
            (byte)(maskColor.a * 255)
        );
        Color32 clearCol = new Color32(0, 0, 0, 0);

        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = center.x * screenW;
        float centerY = center.y * screenH;
        float halfW = size.x * screenW * 0.5f;
        float halfH = size.y * screenH * 0.5f;
        float softnessPixels = edgeSoftness * Mathf.Min(screenW, screenH);

        for (int y = 0; y < textureSize; y++)
        {
            float v = (float)y / (textureSize - 1);
            float py = v * screenH;
            for (int x = 0; x < textureSize; x++)
            {
                float u = (float)x / (textureSize - 1);
                float px = u * screenW;
                int idx = y * textureSize + x;

                float dist;
                if (circle)
                {
                    float dx = px - centerX;
                    float dy = py - centerY;
                    float radius = Mathf.Max(halfW, halfH);
                    dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                }
                else
                {
                    float dx = Mathf.Abs(px - centerX) - halfW;
                    float dy = Mathf.Abs(py - centerY) - halfH;
                    dist = Mathf.Max(dx, dy);
                    if (dx > 0 && dy > 0)
                    {
                        dist = Mathf.Sqrt(dx * dx + dy * dy);
                    }
                }

                float alpha;
                if (softnessPixels > 0.001f)
                {
                    alpha = Mathf.SmoothStep(0, 1, (dist + softnessPixels) / (softnessPixels * 2));
                }
                else
                {
                    alpha = dist > 0 ? 1 : 0;
                }

                byte a = (byte)(maskCol.a * alpha);
                pixels[idx] = new Color32(maskCol.r, maskCol.g, maskCol.b, a);
            }
        }

        maskTexture.SetPixels32(pixels);
        maskTexture.Apply();
    }

    private Vector2 GetScreenPosition(Transform t)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (t is RectTransform rt)
        {
            return GetRectTransformScreenCenter(rt);
        }
        else
        {
            return mainCamera.WorldToScreenPoint(t.position);
        }
    }

    private Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null) return null;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera;
    }

    /// <summary>
    /// 根据 SizeMode 计算尺寸
    /// </summary>
    private Vector2 CalculateSizeByMode()
    {
        switch (sizeMode)
        {
            case SizeMode.AutoFromBounds:
                return CalculateSizeFromBounds();

            case SizeMode.AutoFromRect:
                return CalculateSizeFromRectTransform();

            case SizeMode.WorldUnits:
                return ConvertWorldSizeToScreen(target.position, fixedSize);

            case SizeMode.FixedPixel:
            default:
                return GetScaledSize(fixedSize);
        }
    }

    /// <summary>
    /// 从 Renderer/Collider 的 Bounds 计算屏幕尺寸
    /// </summary>
    private Vector2 CalculateSizeFromBounds()
    {
        if (target == null) return fixedSize;

        if (target.TryGetComponent<Renderer>(out var renderer))
        {
            return ProjectBoundsToScreen(renderer.bounds);
        }

        if (target.TryGetComponent<Collider>(out var col3d))
        {
            return ProjectBoundsToScreen(col3d.bounds);
        }

        if (target.TryGetComponent<Collider2D>(out var col2d))
        {
            return ProjectBoundsToScreen(col2d.bounds);
        }

        Debug.LogWarning($"[GuideMaskSimple] {target.name} 没有 Renderer 或 Collider，使用默认尺寸");
        return fixedSize;
    }

    /// <summary>
    /// 从 RectTransform 计算屏幕尺寸
    /// </summary>
    private Vector2 CalculateSizeFromRectTransform()
    {
        if (target is RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Canvas targetCanvas = rt.GetComponentInParent<Canvas>();
            Camera cam = GetCanvasCamera(targetCanvas);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        Debug.LogWarning($"[GuideMaskSimple] {target.name} 不是 RectTransform，使用默认尺寸");
        return fixedSize;
    }

    private Vector2 GetRectTransformScreenCenter(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Canvas targetCanvas = rt.GetComponentInParent<Canvas>();
        Camera cam = GetCanvasCamera(targetCanvas);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return (min + max) * 0.5f;
    }

    private Vector2 GetCenterOffsetInPixels(Vector2 targetSize)
    {
        return new Vector2(targetSize.x * offset.x, targetSize.y * offset.y);
    }

    /// <summary>
    /// 将世界单位尺寸转换为屏幕像素尺寸
    /// </summary>
    private Vector2 ConvertWorldSizeToScreen(Vector3 worldPosition, Vector2 worldSize)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return worldSize;
        }

        Vector3 pos1 = worldPosition + new Vector3(-worldSize.x * 0.5f, -worldSize.y * 0.5f, 0);
        Vector3 pos2 = worldPosition + new Vector3(worldSize.x * 0.5f, worldSize.y * 0.5f, 0);

        Vector3 screen1 = mainCamera.WorldToScreenPoint(pos1);
        Vector3 screen2 = mainCamera.WorldToScreenPoint(pos2);

        return new Vector2(Mathf.Abs(screen2.x - screen1.x), Mathf.Abs(screen2.y - screen1.y));
    }

    /// <summary>
    /// 根据 Canvas 缩放获取实际尺寸
    /// </summary>
    private Vector2 GetScaledSize(Vector2 size)
    {
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return size;
        }

        float scaleFactor = parentCanvas.scaleFactor;
        return size * scaleFactor;
    }

    private Vector2 ProjectBoundsToScreen(Bounds bounds)
    {
        if (mainCamera == null) return new Vector2(100, 100);

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                ((i & 1) == 0 ? -1 : 1) * e.x,
                ((i & 2) == 0 ? -1 : 1) * e.y,
                ((i & 4) == 0 ? -1 : 1) * e.z
            );
            Vector3 sp = mainCamera.WorldToScreenPoint(corner);
            if (sp.z < 0) continue;
            if (sp.x < minX) minX = sp.x;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.y > maxY) maxY = sp.y;
        }

        if (minX == float.MaxValue) return new Vector2(100, 100);
        return new Vector2(maxX - minX, maxY - minY);
    }

    #region 公共API - 自动尺寸模式

    /// <summary>
    /// 设置自动尺寸的矩形目标（从 Bounds 计算）
    /// </summary>
    public void SetAutoRectTarget(Transform t, Vector2 pad = default, Vector2 off = default)
    {
        target = t;
        sizeMode = SizeMode.AutoFromBounds;
        padding = pad;
        offset = off;
        isCircle = false;
        needsUpdate = true;
    }

    /// <summary>
    /// 设置自动尺寸的圆形目标（从 Bounds 计算）
    /// </summary>
    public void SetAutoCircleTarget(Transform t, Vector2 pad = default, Vector2 off = default)
    {
        target = t;
        sizeMode = SizeMode.AutoFromBounds;
        padding = pad;
        offset = off;
        isCircle = true;
        needsUpdate = true;
    }

    /// <summary>
    /// 设置 UI 元素目标（从 RectTransform 计算）
    /// </summary>
    public void SetUITarget(Transform t, Vector2 pad = default, Vector2 off = default, bool circle = false)
    {
        target = t;
        sizeMode = SizeMode.AutoFromRect;
        padding = pad;
        offset = off;
        isCircle = circle;
        needsUpdate = true;
    }

    #endregion

    #region 公共API - 固定尺寸模式

    /// <summary>
    /// 设置固定像素尺寸的矩形目标
    /// </summary>
    public void SetFixedRectTarget(Transform t, Vector2 size, Vector2 off = default)
    {
        target = t;
        sizeMode = SizeMode.FixedPixel;
        fixedSize = size;
        offset = off;
        isCircle = false;
        needsUpdate = true;
    }

    /// <summary>
    /// 设置固定像素尺寸的圆形目标
    /// </summary>
    public void SetFixedCircleTarget(Transform t, float radius, Vector2 off = default)
    {
        target = t;
        sizeMode = SizeMode.FixedPixel;
        fixedSize = new Vector2(radius * 2, radius * 2);
        offset = off;
        isCircle = true;
        needsUpdate = true;
    }

    /// <summary>
    /// 设置世界单位尺寸的矩形目标
    /// </summary>
    public void SetWorldUnitsRectTarget(Transform t, Vector2 worldSize, Vector2 off = default)
    {
        target = t;
        sizeMode = SizeMode.WorldUnits;
        fixedSize = worldSize;
        offset = off;
        isCircle = false;
        needsUpdate = true;
    }

    #endregion

    #region 公共API - 通用

    /// <summary>
    /// 设置目标（保持当前 SizeMode）
    /// </summary>
    public void SetTarget(Transform t, Vector2 pad = default)
    {
        target = t;
        padding = pad;
        needsUpdate = true;
    }

    public void ClearTarget()
    {
        target = null;
        needsUpdate = true;
    }

    public void SetMaskColor(Color color)
    {
        maskColor = color;
        needsUpdate = true;
    }

    public void SetPadding(Vector2 pad)
    {
        padding = pad;
        needsUpdate = true;
    }

    public void SetOffset(Vector2 off)
    {
        offset = off;
        needsUpdate = true;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #endregion
}

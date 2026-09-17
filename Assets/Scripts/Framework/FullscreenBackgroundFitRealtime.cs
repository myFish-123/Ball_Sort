using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class FullscreenBackgroundFitRealtime : MonoBehaviour
{
    private Image _image;
    private RectTransform _selfRT;
    private RectTransform _rootCanvasRT;

    private Vector2 _lastTargetSize;
    private Sprite _lastSprite;

    // 可选：限制最大检查帧率（例如每帧检查即可，开销很小；如需更省，可改为0.1s一次）
    [SerializeField] private bool checkEveryFrame = true;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _selfRT = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // 使用 rootCanvas 的 RectTransform 作为基准区域
            var root = canvas.rootCanvas;
            _rootCanvasRT = root != null ? root.GetComponent<RectTransform>() : canvas.GetComponent<RectTransform>();
        }

        // 保持比例，不让Image自身做不必要的缩放
        _image.preserveAspect = true;

        // 由我们完全控制尺寸，不使用 SetNativeSize
        // _image.SetNativeSize(); // 移除

        // 统一居中对齐，避免锚点干扰覆盖逻辑
        _selfRT.anchorMin = new Vector2(0.5f, 0.5f);
        _selfRT.anchorMax = new Vector2(0.5f, 0.5f);
        _selfRT.pivot = new Vector2(0.5f, 0.5f);
        _selfRT.anchoredPosition = Vector2.zero;
    }

    private void OnEnable()
    {
        StartCoroutine(Co_WatchAndFit());
        // 初次应用
        ApplyFit(force: true);
    }

    private IEnumerator Co_WatchAndFit()
    {
        // 在编辑器下，Game 窗口 Free Aspect 或拖拽缩放时，用协程轮询最稳妥
        while (enabled && gameObject.activeInHierarchy)
        {
            ApplyFit();

            if (checkEveryFrame)
                yield return null; // 每帧检测
            else
                yield return new WaitForSeconds(0.1f); // 降低频率
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        // UI 变化时也尝试更新（大多数情况下协程已覆盖，这里作为补充）
        ApplyFit();
    }

    private Vector2 GetTargetSize()
    {
        // 优先使用根 Canvas 的 UI 空间尺寸
        if (_rootCanvasRT != null)
        {
            var size = _rootCanvasRT.rect.size;
            if (size.x > 0 && size.y > 0)
                return size;
        }

        // 退化为屏幕像素分辨率
        return new Vector2(Screen.width, Screen.height);
    }

    private void ApplyFit(bool force = false)
    {
        var sp = _image != null ? _image.sprite : null;
        if (sp == null) return;

        Vector2 targetSize = GetTargetSize();
        if (targetSize.x <= 0f || targetSize.y <= 0f) return;

        // 若尺寸与资源均未变化，则不重复计算
        if (!force && _lastTargetSize == targetSize && _lastSprite == sp)
            return;

        _lastTargetSize = targetSize;
        _lastSprite = sp;

        // 原始图片像素尺寸（使用 sprite rect 尺寸而非 texture 尺寸更精确）
        Rect sr = sp.rect;
        float imgW = sr.width;
        float imgH = sr.height;

        if (imgW <= 0f || imgH <= 0f) return;

        float targetAspect = targetSize.x / targetSize.y;
        float imageAspect = imgW / imgH;

        // 覆盖策略：保证至少一边“完全覆盖”
        // - 若目标更宽（targetAspect > imageAspect），优先拉满宽度
        // - 否则优先拉满高度
        float finalW, finalH;

        if (targetAspect > imageAspect)
        {
            // 屏幕更宽：先匹配宽度，横屏必然拉满
            finalW = targetSize.x;
            finalH = finalW / imageAspect;
        }
        else
        {
            // 屏幕更窄或等宽：先匹配高度
            finalH = targetSize.y;
            finalW = finalH * imageAspect;
        }

        // 应用到 RectTransform 尺寸（UI 单位）
        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalW);
        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalH);

        // 居中（裁切在四边对称分布）
        _selfRT.anchoredPosition = Vector2.zero;
    }
}
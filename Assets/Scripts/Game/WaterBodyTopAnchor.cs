using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WaterBodyTopAnchor : MonoBehaviour
{
    [Tooltip("保持在 Body 顶部的 Up，和 Body 放在同一父节点下。")]
    [SerializeField] private SpriteRenderer up;
    [Tooltip("Up 的贴图垂直偏移（世界单位）；负值下移，用于遮住素材接缝，不影响有效高度。")]
    [SerializeField] private float upVerticalOffset = -0.10f;
    [Tooltip("Body 额外向下覆盖的世界距离，遮住两张素材弧线不一致时露出的边缘。")]
    [Min(0f)] [SerializeField] private float seamOverlap = 0.04f;
    [Tooltip("Up 相对 Body 向白色提亮的程度。")]
    [Range(0f, 1f)]
    [SerializeField] private float upLightenAmount = 0.4f;

    private SpriteRenderer body;
    private float revealProgress = 1f;
    private WaterBodyTopAnchor incomingColorSource;
    private MaterialPropertyBlock revealProperties;
    private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
    private static readonly int RevealPlaneId = Shader.PropertyToID("_RevealPlane");

    private Color NaturalUpColor
    {
        get
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            Color color = Color.Lerp(body.color, Color.white, upLightenAmount);
            color.a = body.color.a;
            return color;
        }
    }

    public void SetIncomingColor(WaterBodyTopAnchor source)
    {
        incomingColorSource = source;
        AlignUp();
    }

    public void ClearIncomingColor(WaterBodyTopAnchor source)
    {
        if (incomingColorSource != source) return;
        incomingColorSource = null;
        AlignUp();
    }

    private void OnDisable()
    {
        incomingColorSource = null;
    }

    public float CrossSectionHalfHeightWorld => up != null
        ? up.transform.TransformVector(Vector3.up * up.localBounds.extents.y).magnitude
        : 0f;

    // Extend beyond the lowered Up to cover differences between the two sprite curves.
    public float BottomOverlapWorld => Mathf.Max(0f, CrossSectionHalfHeightWorld - upVerticalOffset + seamOverlap);

    private Vector3 GetSurfaceWorldPosition()
    {
        Bounds bounds = body.localBounds;
        float bottomOverlap = BottomOverlapWorld / Mathf.Abs(transform.lossyScale.y);
        return transform.TransformPoint(new Vector3(bounds.center.x,
            Mathf.Lerp(bounds.min.y + bottomOverlap, bounds.max.y, revealProgress), bounds.center.z));
    }

    private void OnEnable()
    {
        body = GetComponent<SpriteRenderer>();
        AlignUp();
    }

    private void LateUpdate()
    {
        AlignUp();
        if (revealProgress > 0f && revealProgress < 1f)
        {
            ApplyRevealClip();
        }
    }

    public void SetRevealProgress(float progress)
    {
        revealProgress = Mathf.Clamp01(progress);
        if (body == null) body = GetComponent<SpriteRenderer>();
        AlignUp();
        ApplyRevealClip();
    }

    private void ApplyRevealClip()
    {
        if (revealProperties == null) revealProperties = new MaterialPropertyBlock();
        Vector3 surface = GetSurfaceWorldPosition();
        Vector3 normal = transform.up;
        body.GetPropertyBlock(revealProperties);
        revealProperties.SetFloat(RevealProgressId, revealProgress);
        revealProperties.SetVector(RevealPlaneId,
            new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, surface)));
        body.SetPropertyBlock(revealProperties);
    }

    private void AlignUp()
    {
        if (body == null || up == null || up == body || body.sprite == null || up.sprite == null)
        {
            return;
        }

        Vector3 upCenterOffset = up.transform.TransformVector(up.localBounds.center);
        Vector3 targetPosition = GetSurfaceWorldPosition() + transform.up * upVerticalOffset - upCenterOffset;

        if (up.transform.position != targetPosition)
        {
            up.transform.position = targetPosition;
        }

        up.color = incomingColorSource != null ? incomingColorSource.NaturalUpColor : NaturalUpColor;
        up.sortingLayerID = body.sortingLayerID;
        up.sortingOrder = body.sortingOrder + 1;
        up.enabled = revealProgress > 0f;
    }
}

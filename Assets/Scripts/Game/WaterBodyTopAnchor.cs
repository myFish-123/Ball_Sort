using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WaterBodyTopAnchor : MonoBehaviour
{
    [Tooltip("保持在 Body 顶部的 Up，和 Body 放在同一父节点下。")]
    [SerializeField] private SpriteRenderer up;
    [Tooltip("Up 底边与 Body 顶边的距离（世界单位），负值表示重叠。")]
    [SerializeField] private float topOffset;
    [Tooltip("Up 相对 Body 向白色提亮的程度。")]
    [Range(0f, 1f)]
    [SerializeField] private float upLightenAmount = 0.4f;

    private SpriteRenderer body;
    private float revealProgress = 1f;
    private MaterialPropertyBlock revealProperties;
    private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
    private static readonly int RevealPlaneId = Shader.PropertyToID("_RevealPlane");

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
        Bounds bounds = body.localBounds;
        Vector3 surface = transform.TransformPoint(new Vector3(bounds.center.x,
            Mathf.Lerp(bounds.min.y, bounds.max.y, revealProgress), bounds.center.z));
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

        Bounds bodyBounds = body.localBounds;
        Bounds upBounds = up.localBounds;
        Vector3 bodyTop = transform.TransformPoint(
            new Vector3(bodyBounds.center.x,
                Mathf.Lerp(bodyBounds.min.y, bodyBounds.max.y, revealProgress), bodyBounds.center.z));
        Vector3 upBottomOffset = up.transform.TransformVector(
            new Vector3(upBounds.center.x, upBounds.min.y, upBounds.center.z));
        Vector3 targetPosition = bodyTop + transform.up * topOffset - upBottomOffset;

        if (up.transform.position != targetPosition)
        {
            up.transform.position = targetPosition;
        }

        Color upColor = Color.Lerp(body.color, Color.white, upLightenAmount);
        upColor.a = body.color.a;
        up.color = upColor;
        up.sortingLayerID = body.sortingLayerID;
        up.sortingOrder = body.sortingOrder + 1;
        up.enabled = revealProgress > 0f;
    }
}

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

    private void OnEnable()
    {
        body = GetComponent<SpriteRenderer>();
        AlignUp();
    }

    private void LateUpdate()
    {
        AlignUp();
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
            new Vector3(bodyBounds.center.x, bodyBounds.max.y, bodyBounds.center.z));
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
    }
}

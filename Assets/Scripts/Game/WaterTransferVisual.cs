using DG.Tweening;
using UnityEngine;

public class WaterTransferVisual : MonoBehaviour
{
    private static readonly int WaterUpState = Animator.StringToHash("Base Layer.water-up");
    private static readonly int WaterDownState = Animator.StringToHash("Base Layer.water-down");
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Animator waterUp;
    [SerializeField] private Animator waterDown;
    [Tooltip("落位后，水柱从底部展开到完整高度的时间。")]
    [Min(0.01f)]
    [SerializeField] private float revealDuration = 0.25f;

    private SpriteRenderer upRenderer;
    private SpriteRenderer downRenderer;
    private Vector3 bodyScale;
    private float downClipDuration;

    private void Awake()
    {
        bodyScale = body.transform.localScale;
        upRenderer = waterUp.GetComponent<SpriteRenderer>();
        downRenderer = waterDown.GetComponent<SpriteRenderer>();
        downClipDuration = waterDown.runtimeAnimatorController.animationClips[0].length;
    }

    public void PlayUp()
    {
        RestoreBody();
        visualRoot.SetActive(false);
        upRenderer.color = body.color;
        SyncSorting();
        waterUp.gameObject.SetActive(true);
        waterUp.Play(WaterUpState, 0, 0f);
        waterUp.Update(0f);
    }

    public void PlayDown(float duration)
    {
        visualRoot.SetActive(false);
        waterUp.gameObject.SetActive(false);
        downRenderer.color = body.color;
        SyncSorting();
        waterDown.gameObject.SetActive(true);
        waterDown.speed = downClipDuration / Mathf.Max(0.01f, duration);
        waterDown.Play(WaterDownState, 0, 0f);
        waterDown.Update(0f);
    }

    public void SyncSorting()
    {
        // BallView.Awake 和此组件 Awake 的执行顺序不固定。
        if (upRenderer == null || downRenderer == null)
        {
            return;
        }

        upRenderer.sortingLayerID = body.sortingLayerID;
        downRenderer.sortingLayerID = body.sortingLayerID;
        upRenderer.sortingOrder = body.sortingOrder + 1;
        downRenderer.sortingOrder = body.sortingOrder + 1;
    }

    public Sequence CreateRevealSequence()
    {
        Sequence sequence = DOTween.Sequence();
        sequence.AppendCallback(() =>
        {
            waterUp.gameObject.SetActive(false);
            waterDown.gameObject.SetActive(false);
            SetRevealProgress(0f);
            visualRoot.SetActive(true);
        });
        sequence.Append(DOVirtual.Float(0f, 1f, revealDuration, SetRevealProgress)
            .SetEase(Ease.OutQuad));
        sequence.AppendCallback(RestoreBody);
        return sequence;
    }

    private void SetRevealProgress(float progress)
    {
        // Body 使用底部支点，Up 是其同级对象，由 WaterBodyTopAnchor 跟随。
        Vector3 scale = bodyScale;
        scale.y *= progress;
        body.transform.localScale = scale;
    }

    public void RestoreBody()
    {
        body.transform.localScale = bodyScale;
        waterUp.gameObject.SetActive(false);
        waterDown.gameObject.SetActive(false);
        visualRoot.SetActive(true);
    }
}

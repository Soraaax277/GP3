using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class ActionLogBox : MonoBehaviour
{
    public static ActionLogBox Instance;

    [Header("Child Panels")]
    public RectTransform researchPanel;
    public RectTransform logPanel;

    [Header("Content RectTransforms")]
    public RectTransform researchContent;
    public RectTransform logContent;

    [Header("Layout")]
    public float panelGap      = 8f;
    public float maxHeight     = 300f;
    public float minHeight     = 60f;

    [Header("Animation")]
    public float tweenDuration = 0.35f;
    public Ease  tweenEase     = Ease.OutCubic;

    private bool _hasResearch = false;
    private bool _hasLog      = false;

    private float _pendingHeight = 0f;
    private float _pendingAlpha  = 0f;
    private bool  _pendingDirty  = false;

    private CanvasGroup   _cg;
    private RectTransform _rt;
    private Tweener       _heightTween;
    private Tweener       _alphaTween;

    private void Awake()
    {
        Instance           = this;
        _cg                = GetComponent<CanvasGroup>();
        _rt                = GetComponent<RectTransform>();
        _cg.alpha          = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;
        _rt.sizeDelta      = new Vector2(_rt.sizeDelta.x, 0f);

        // Both panels hidden at start — box is empty
        if (researchPanel != null) researchPanel.gameObject.SetActive(false);
        if (logPanel      != null) logPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_pendingDirty)
            PlayAnimation(_pendingHeight, _pendingAlpha);
    }

    // -------------------------------------------------------------------------
    //  PUBLIC NOTIFY API
    // -------------------------------------------------------------------------

    public void NotifyResearchChanged(bool hasContent)
    {
        _hasResearch = hasContent;
        UpdateBox();
    }

    public void NotifyLogChanged(bool hasContent)
    {
        _hasLog = hasContent;
        UpdateBox();
    }

    // -------------------------------------------------------------------------
    //  CORE UPDATE
    // -------------------------------------------------------------------------

    private void UpdateBox()
    {
        bool anyContent    = _hasResearch || _hasLog;
        float targetAlpha  = anyContent ? 1f : 0f;
        float targetHeight = 0f;

        if (anyContent)
        {
            // Show panels that have content immediately.
            // Panels losing content stay visible until the tween completes
            // — they get deactivated in OnComplete to allow the fade-out to show.
            if (_hasResearch && researchPanel != null)
                researchPanel.gameObject.SetActive(true);
            if (_hasLog && logPanel != null)
                logPanel.gameObject.SetActive(true);

            // Sibling order — active content panel on top
            if (_hasResearch && _hasLog)
            {
                researchPanel.SetSiblingIndex(0);
                logPanel.SetSiblingIndex(1);
            }
            else if (_hasLog)
            {
                logPanel.SetSiblingIndex(0);
                researchPanel.SetSiblingIndex(1);
            }
            else
            {
                researchPanel.SetSiblingIndex(0);
                logPanel.SetSiblingIndex(1);
            }

            // Measure height after layout
            Canvas.ForceUpdateCanvases();
            if (_hasResearch && researchContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(researchContent);
            if (_hasLog && logContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(logContent);

            float rh  = (_hasResearch && researchContent != null) ? researchContent.rect.height : 0f;
            float lh  = (_hasLog      && logContent      != null) ? logContent.rect.height      : 0f;
            float gap = (_hasResearch && _hasLog)                 ? panelGap                    : 0f;

            targetHeight = Mathf.Clamp(rh + lh + gap, minHeight, maxHeight);
        }

        _pendingHeight = targetHeight;
        _pendingAlpha  = targetAlpha;
        _pendingDirty  = true;

        if (gameObject.activeInHierarchy)
            PlayAnimation(targetHeight, targetAlpha);
    }

    private void PlayAnimation(float targetHeight, float targetAlpha)
    {
        _pendingDirty = false;

        // Snapshot which panels should be deactivated after fade-out
        bool deactivateResearch = !_hasResearch;
        bool deactivateLog      = !_hasLog;

        KillTweens();

        _heightTween = DOTween.To(
                () => _rt.sizeDelta.y,
                y  => _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, y),
                targetHeight, tweenDuration)
            .SetEase(tweenEase);

        _alphaTween = _cg.DOFade(targetAlpha, tweenDuration)
            .SetEase(tweenEase)
            .OnComplete(() =>
            {
                if (targetAlpha <= 0f)
                {
                    // Fully faded — clean up both panels
                    _cg.blocksRaycasts = false;
                    _cg.interactable   = false;
                    if (researchPanel != null) researchPanel.gameObject.SetActive(false);
                    if (logPanel      != null) logPanel.gameObject.SetActive(false);
                }
                else
                {
                    // Fade-in complete — now deactivate panels that lost their content
                    // This gives them a clean fade-out as part of the box shrink
                    if (deactivateResearch && researchPanel != null)
                        researchPanel.gameObject.SetActive(false);
                    if (deactivateLog && logPanel != null)
                        logPanel.gameObject.SetActive(false);

                    _cg.blocksRaycasts = true;
                    _cg.interactable   = true;
                }
            });
    }

    private void KillTweens()
    {
        _heightTween?.Kill();
        _alphaTween?.Kill();
    }
}
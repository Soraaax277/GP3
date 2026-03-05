using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; 
using DG.Tweening;
using System.Collections; 
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
// Add a CanvasGroup component to the prefab for the Fade and Interaction logic to work
public class UIAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public enum UIType { Button, Window, Shutter, SlidePanel, WorldSpacePopUp, TechNodeButton }
    public enum SlideDirection { Left, Right, Top, Bottom }

    [Header("Main Settings")]
    public UIType uiType = UIType.Button;
    public UITheme overrideTheme; 

    [Header("Slide Settings")]
    public SlideDirection slideDirection = SlideDirection.Right;

    [Header("Shutter References")]
    public RectTransform topShutter;        
    public RectTransform bottomShutter; 
    public RectTransform contentRoot;   

    [Header("Shutter Logic")]
    public bool isTechTreeWindow = false; 
    public UnityEvent onShutterClosed; 

    [Header("Pop Settings")]
    [Range(1f, 2f)] public float overshootScale = 1.2f;
    [Range(0f, 90f)] public float tiltAngle = 7f;

    private UITheme activeTheme;
    private Vector3 originalScale;
    private Vector2 originalAnchoredPos; 
    private RectTransform rectTrans;
    private CanvasGroup canvasGroup; 
    private bool isInitialized = false;
    private Sequence currentSequence; 
    
    // Internal variable to track animated tilt without fighting the Billboard LookAt
    private float currentZTilt = 0f;

    //  TechNodeButton state
    private static UIAnimator s_activeTechButton = null;
    private RectTransform childImageRectTrans;
    private float originalChildZRotation;
    private bool isTechNodeActive = false;
    private Vector2 techPointerDownPos;
    private bool techPointerIsDown = false;

    //  Unity Lifecycle
    private void Awake()
    {
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>(); 

        if (uiType == UIType.WorldSpacePopUp)
            rectTrans.pivot = new Vector2(0, 0);
        
        originalAnchoredPos = rectTrans.anchoredPosition; 
        originalScale = rectTrans.localScale;

        if (uiType == UIType.TechNodeButton)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != this.gameObject)
                {
                    childImageRectTrans = img.GetComponent<RectTransform>();
                    img.raycastTarget = false;
                    break;
                }
            }

            if (childImageRectTrans != null)
            {
                float raw = childImageRectTrans.localEulerAngles.z;
                originalChildZRotation = raw > 180f ? raw - 360f : raw;
            }

            Image parentImage = GetComponent<Image>();
            if (parentImage != null)
                parentImage.raycastTarget = true;
        }
    }

    private void OnEnable()
    {
        // Try to load theme — may silently fail here if UIAnimationManager isn't ready yet.
        // That is fine: InitializeTheme is called again in PlayEntryAnimation and every
        // pointer event handler, so it will succeed as soon as the manager exists.
        InitializeTheme();
        isInitialized = true;

        if (uiType != UIType.Button && uiType != UIType.TechNodeButton && activeTheme != null)
            PlayEntryAnimation();
    }

    private void LateUpdate()
    {
        if (uiType == UIType.WorldSpacePopUp && Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
            transform.Rotate(0, 0, currentZTilt, Space.Self);
        }
    }

    //  Theme — retries every call until successfully loaded
    private void InitializeTheme()
    {
        // Already loaded — nothing to do
        if (activeTheme != null) return;

        if (overrideTheme != null)
        {
            activeTheme = overrideTheme;
            return;
        }

        // Manager not ready yet — will retry next call
        if (UIAnimationManager.Instance == null) return;

        switch (uiType)
        {
            case UIType.Button:          activeTheme = UIAnimationManager.Instance.defaultButtonTheme;     break;
            case UIType.TechNodeButton:  activeTheme = UIAnimationManager.Instance.defaultTechButtonTheme; break;
            case UIType.Window:          activeTheme = UIAnimationManager.Instance.defaultWindowTheme;     break;
            case UIType.Shutter:         activeTheme = UIAnimationManager.Instance.defaultShutterTheme;    break;
            case UIType.SlidePanel:      activeTheme = UIAnimationManager.Instance.defaultSlideTheme;      break;
            case UIType.WorldSpacePopUp: activeTheme = UIAnimationManager.Instance.defaultPopUpTheme;      break;
        }
    }

    //  Entry / Exit Animations (Windows, Shutters, Slides, PopUps)
    public void PlayEntryAnimation()
    {
        InitializeTheme();

        if (!isInitialized) 
        { 
            if (rectTrans == null) rectTrans = GetComponent<RectTransform>();
            originalAnchoredPos = rectTrans.anchoredPosition;
            originalScale = rectTrans.localScale;
            isInitialized = true; 
        }

        if (activeTheme != null)
        {
            if (originalScale.sqrMagnitude < 0.01f) originalScale = Vector3.one; 
            PlayAnimationBasedOnType();
        }
    }

    private void PlayAnimationBasedOnType()
    {
        rectTrans.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        if (uiType == UIType.Shutter || activeTheme.windowStyle == UITheme.AnimationStyle.Shutter)
            AnimateShutterEntry();
        else if (activeTheme.windowStyle == UITheme.AnimationStyle.Slide)
            AnimateSlideEntry();
        else if (activeTheme.windowStyle == UITheme.AnimationStyle.PopUp || uiType == UIType.WorldSpacePopUp)
            AnimatePopUpEntry(); 
        else
            AnimateScaleEntry();
    }

    public void AnimateExit(System.Action onComplete)
    {
        InitializeTheme();

        // ── NULL GUARD ────────────────────────────────────────────────────────
        // rectTrans can be null if the GameObject was destroyed or OnDisable
        // already ran before DOTween got to start the tween.  Skip the animation
        // and invoke the callback immediately so Close() still completes cleanly.
        if (rectTrans == null || !gameObject.activeInHierarchy)
        {
            onComplete?.Invoke();
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

        if (uiType == UIType.Button || uiType == UIType.TechNodeButton || activeTheme == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (currentSequence != null) currentSequence.Kill();
        rectTrans.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        if (uiType == UIType.Shutter || activeTheme.windowStyle == UITheme.AnimationStyle.Shutter)
        {
            StartCoroutine(AnimateShutterExitRoutine(onComplete));
        }
        else if (activeTheme.windowStyle == UITheme.AnimationStyle.Slide)
        {
            Vector2 exitPos = GetOffScreenPosition();
            rectTrans.DOAnchorPos(exitPos, activeTheme.slideDuration)
                     .SetEase(Ease.InBack).SetUpdate(true)
                     .OnComplete(() => onComplete?.Invoke());
            if (canvasGroup != null)
                canvasGroup.DOFade(0f, activeTheme.slideDuration).SetUpdate(true);
        }
        else if (activeTheme.windowStyle == UITheme.AnimationStyle.PopUp || uiType == UIType.WorldSpacePopUp)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // Guard: originalScale must be non-zero or DOScale will NullRef internally
            Vector3 exitScale = Vector3.zero;
            rectTrans.DOScale(exitScale, activeTheme.popUpExitDuration)
                     .SetEase(activeTheme.popUpExitEase).SetUpdate(true)
                     .OnComplete(() => onComplete?.Invoke());

            if (canvasGroup != null)
                canvasGroup.DOFade(0f, activeTheme.popUpExitDuration).SetUpdate(true);

            DOTween.To(() => currentZTilt, x => currentZTilt = x, 0f, activeTheme.popUpExitDuration)
                   .SetUpdate(true);
        }
        else
        {
            rectTrans.DOScale(Vector3.zero, activeTheme.entryDuration)
                     .SetEase(Ease.InBack).SetUpdate(true)
                     .OnComplete(() => onComplete?.Invoke());
            if (canvasGroup != null)
                canvasGroup.DOFade(0f, activeTheme.entryDuration).SetUpdate(true);
        }
    }

    private void AnimatePopUpEntry()
    {
        if (currentSequence != null) currentSequence.Kill();

        // Guard: if originalScale is zero the overshoot target will also be zero,
        // causing DOTween's getter lambda to resolve to a zero-magnitude vector.
        if (originalScale.sqrMagnitude < 0.01f) originalScale = Vector3.one;

        rectTrans.localScale = Vector3.zero;
        currentZTilt = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
        }

        float halfDuration = activeTheme.popUpDuration * 0.5f;
        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Append(rectTrans.DOScale(originalScale * overshootScale, halfDuration).SetEase(Ease.OutQuad));

        // Only join canvasGroup tween if it actually exists
        if (canvasGroup != null)
            currentSequence.Join(canvasGroup.DOFade(1f, halfDuration));

        currentSequence.Join(DOTween.To(() => currentZTilt, x => currentZTilt = x, tiltAngle, halfDuration).SetEase(Ease.OutQuad));
        currentSequence.Append(rectTrans.DOScale(originalScale, halfDuration).SetEase(Ease.InOutQuad));
        currentSequence.Join(DOTween.To(() => currentZTilt, x => currentZTilt = x, 0f, halfDuration).SetEase(Ease.InOutQuad));
        currentSequence.OnComplete(() => { if (canvasGroup != null) canvasGroup.blocksRaycasts = true; });
    }

    private void AnimateScaleEntry()
    {
        rectTrans.localScale = Vector3.zero;
        rectTrans.DOScale(originalScale, activeTheme.entryDuration).SetEase(activeTheme.entryEase).SetUpdate(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1f, activeTheme.entryDuration).SetUpdate(true);
        }
    }

    private void AnimateSlideEntry()
    {
        rectTrans.localScale = originalScale; 
        rectTrans.anchoredPosition = GetOffScreenPosition();
        rectTrans.DOAnchorPos(originalAnchoredPos, activeTheme.slideDuration).SetEase(activeTheme.slideEase).SetUpdate(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1f, activeTheme.slideDuration).SetUpdate(true);
        }
    }

    private Vector2 GetOffScreenPosition()
    {
        float width = rectTrans.rect.width; 
        float height = rectTrans.rect.height;
        float buffer = 50f; 
        switch (slideDirection)
        {
            case SlideDirection.Right: return new Vector2(originalAnchoredPos.x + width + buffer, originalAnchoredPos.y);
            case SlideDirection.Left:  return new Vector2(originalAnchoredPos.x - width - buffer, originalAnchoredPos.y);
            case SlideDirection.Top:   return new Vector2(originalAnchoredPos.x, originalAnchoredPos.y + height + buffer);
            case SlideDirection.Bottom:return new Vector2(originalAnchoredPos.x, originalAnchoredPos.y - height - buffer);
            default: return originalAnchoredPos;
        }
    }

    private void AnimateShutterEntry()
    {
        if (topShutter == null || bottomShutter == null) return;
        float height = topShutter.rect.height;
        topShutter.anchoredPosition = new Vector2(0, height);         
        bottomShutter.anchoredPosition = new Vector2(0, -height);    
        if (contentRoot != null) contentRoot.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Append(topShutter.DOAnchorPos(Vector2.zero, activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        currentSequence.Join(bottomShutter.DOAnchorPos(Vector2.zero, activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        currentSequence.AppendCallback(() => { 
            if (contentRoot != null) contentRoot.localScale = Vector3.one; 
            onShutterClosed?.Invoke();
        });
        currentSequence.AppendInterval(activeTheme.shutterDelay);
        currentSequence.Append(topShutter.DOAnchorPos(new Vector2(0, height), activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        currentSequence.Join(bottomShutter.DOAnchorPos(new Vector2(0, -height), activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
    }

    private IEnumerator AnimateShutterExitRoutine(System.Action onComplete)
    {
        if (topShutter == null || bottomShutter == null) { onComplete?.Invoke(); yield break; }
        Sequence closeSeq = DOTween.Sequence().SetUpdate(true);
        closeSeq.Append(topShutter.DOAnchorPos(Vector2.zero, activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        closeSeq.Join(bottomShutter.DOAnchorPos(Vector2.zero, activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        yield return closeSeq.WaitForCompletion();
        if (contentRoot != null) contentRoot.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        float height = topShutter.rect.height;
        Sequence openSeq = DOTween.Sequence().SetUpdate(true);
        openSeq.Append(topShutter.DOAnchorPos(new Vector2(0, height), activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        openSeq.Join(bottomShutter.DOAnchorPos(new Vector2(0, -height), activeTheme.shutterDuration).SetEase(activeTheme.shutterEase));
        yield return openSeq.WaitForCompletion();
        onComplete?.Invoke();
    }

    //  Pointer Events
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (uiType == UIType.TechNodeButton)
        {
            InitializeTheme();
            if (activeTheme == null || isTechNodeActive) return;
            TechNodeButton_AnimateActive();
            return;
        }

        if (activeTheme == null) return;
        if (uiType != UIType.Button) return;
        rectTrans.DOScale(originalScale * activeTheme.hoverScale, activeTheme.hoverDuration).SetEase(activeTheme.hoverEase).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (uiType == UIType.TechNodeButton)
        {
            InitializeTheme();
            if (activeTheme == null || isTechNodeActive) return;
            TechNodeButton_AnimateNormal();
            return;
        }

        if (activeTheme == null) return;
        if (uiType != UIType.Button) return;
        rectTrans.DOScale(originalScale, activeTheme.hoverDuration).SetEase(activeTheme.hoverEase).SetUpdate(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (uiType == UIType.TechNodeButton)
        {
            techPointerDownPos = eventData.position;
            techPointerIsDown = true;
            return;
        }

        if (activeTheme == null) return;
        if (uiType != UIType.Button) return;
        rectTrans.DOScale(originalScale * activeTheme.clickScale, activeTheme.clickDuration).SetEase(activeTheme.clickEase).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (uiType == UIType.TechNodeButton)
        {
            if (!techPointerIsDown) return;
            techPointerIsDown = false;

            InitializeTheme();
            if (activeTheme == null) return;

            float drag = Vector2.Distance(techPointerDownPos, eventData.position);
            if (drag >= activeTheme.techButtonScrollDragThreshold)
            {
                if (!isTechNodeActive) TechNodeButton_AnimateNormal();
            }
            else
            {
                TechNodeButton_SetActive(true);
            }
            return;
        }

        OnPointerEnter(eventData);
    }

    //  TechNodeButton — Active State Management
    public static void DeactivateCurrentTechButton()
    {
        if (s_activeTechButton != null)
        {
            s_activeTechButton.TechNodeButton_SetActive(false);
            s_activeTechButton = null;
        }
    }

    private void TechNodeButton_SetActive(bool active)
    {
        if (active && s_activeTechButton != null && s_activeTechButton != this)
            s_activeTechButton.TechNodeButton_SetActive(false);

        isTechNodeActive = active;

        if (active)
        {
            s_activeTechButton = this;
            TechNodeButton_AnimateActive();
        }
        else
        {
            if (s_activeTechButton == this) s_activeTechButton = null;
            TechNodeButton_AnimateNormal();
        }
    }

    //  TechNodeButton — Per-State Animations
    private void TechNodeButton_AnimateActive()
    {
        rectTrans.DOKill();
        rectTrans.DOScale(originalScale * activeTheme.techButtonActiveScale, activeTheme.techButtonScaleDuration)
                 .SetEase(activeTheme.techButtonScaleUpEase)
                 .SetUpdate(true);

        if (childImageRectTrans != null)
        {
            childImageRectTrans.DOKill();
            Vector3 e = childImageRectTrans.localEulerAngles;
            childImageRectTrans.DOLocalRotate(new Vector3(e.x, e.y, 0f), activeTheme.techButtonRotationDuration)
                .SetEase(activeTheme.techButtonRotationEase)
                .SetUpdate(true);
        }
    }

    private void TechNodeButton_AnimateNormal()
    {
        rectTrans.DOKill();
        rectTrans.DOScale(originalScale, activeTheme.techButtonScaleDuration)
                 .SetEase(activeTheme.techButtonScaleDownEase)
                 .SetUpdate(true);

        if (childImageRectTrans != null)
        {
            childImageRectTrans.DOKill();
            Vector3 e = childImageRectTrans.localEulerAngles;
            childImageRectTrans.DOLocalRotate(new Vector3(e.x, e.y, originalChildZRotation), activeTheme.techButtonRotationDuration)
                .SetEase(activeTheme.techButtonRotationEase)
                .SetUpdate(true);
        }
    }

    //  Cleanup
    private void OnDisable()
    {
        rectTrans.DOKill();
        if (canvasGroup != null) 
        { 
            canvasGroup.DOKill(); 
            canvasGroup.alpha = 1f; 
            canvasGroup.blocksRaycasts = true;
        } 
        if (currentSequence != null) currentSequence.Kill();
        rectTrans.localScale = originalScale;
        currentZTilt = 0f;
        if (activeTheme != null && activeTheme.windowStyle == UITheme.AnimationStyle.Slide)
            rectTrans.anchoredPosition = originalAnchoredPos;

        if (uiType == UIType.TechNodeButton)
        {
            if (childImageRectTrans != null) childImageRectTrans.DOKill();
            if (s_activeTechButton == this) s_activeTechButton = null;
            isTechNodeActive = false;
            techPointerIsDown = false;
        }
    }
}
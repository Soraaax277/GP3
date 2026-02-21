using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName = "New UI Theme", menuName = "UI/Animation Theme")]
public class UITheme : ScriptableObject
{
    // Updated enum to include Slide and PopUp
    public enum AnimationStyle { Scale, Shutter, Slide, PopUp }

    [Header("General Settings")]
    public AnimationStyle windowStyle = AnimationStyle.Scale;

    [Header("Button Hover Settings")]
    public float hoverScale = 1.1f;
    public float hoverDuration = 0.2f;
    public Ease hoverEase = Ease.OutQuad;

    [Header("Button Click Settings")]
    public float clickScale = 0.95f;
    public float clickDuration = 0.1f;
    public Ease clickEase = Ease.OutQuad;

    [Header("Window Scale Settings")]
    public float entryScale = 0.8f; 
    public float entryDuration = 0.5f;
    public Ease entryEase = Ease.OutBack;

    [Header("Window Shutter Settings")]
    public float shutterDuration = 0.5f;
    public Ease shutterEase = Ease.OutExpo;
    public float shutterDelay = 0.2f;

    [Header("Window Slide Settings")]
    public float slideDuration = 0.4f;
    public Ease slideEase = Ease.OutQuart;

    [Header("World Space Pop-Up Settings")]
    public float popUpDuration = 0.35f;
    public Ease popUpEase = Ease.OutBack; // OutBack gives that comic-book popping out effect
    public float popUpExitDuration = 0.2f;
    public Ease popUpExitEase = Ease.InBack;

    [Header("Tech Node Button Settings")]
    [Tooltip("Scale applied to the button when hovered or locked-active.")]
    public float techButtonActiveScale = 1.2f;
    [Tooltip("Duration of the scale up / scale down tween.")]
    public float techButtonScaleDuration = 0.15f;
    [Tooltip("Ease used when scaling up (hover enter / select).")]
    public Ease techButtonScaleUpEase = Ease.OutBack;
    [Tooltip("Ease used when scaling back down (hover exit / deselect).")]
    public Ease techButtonScaleDownEase = Ease.InQuad;
    [Tooltip("Duration of the child image Z-rotation tween (to 0 on hover/select, back to original on exit/deselect).")]
    public float techButtonRotationDuration = 0.15f;
    [Tooltip("Ease used for the child image Z-rotation tween.")]
    public Ease techButtonRotationEase = Ease.OutQuad;
    [Tooltip("How many pixels the pointer must move between PointerDown and PointerUp to count as a scroll rather than a click.")]
    public float techButtonScrollDragThreshold = 8f;
}
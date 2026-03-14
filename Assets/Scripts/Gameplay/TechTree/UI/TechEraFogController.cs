using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Attach to each Era panel (e.g. Era1Panel, Era2Panel) inside a category.
/// Controls two fog Image states:
///   FogFull    — covers the entire era (era not yet reachable)
///   FogPartial — covers columns 2-5 only (era reachable but gate not yet unlocked)
/// </summary>
public class TechEraFogController : MonoBehaviour
{
    [Header("Fog Images")]
    [Tooltip("Fog image that covers the ENTIRE era. Visible when era is not yet reachable.")]
    [SerializeField] private Image fogFull;

    [Tooltip("Fog image that covers columns 2-5 only. Visible when era is reachable but gate node is not yet unlocked.")]
    [SerializeField] private Image fogPartial;

    [Header("Era Gate")]
    [Tooltip("The column 1 TechNode for this era. When unlocked, fogPartial fades out.")]
    [SerializeField] private TechNode gateNode;

    [Header("Era Transition (from previous era)")]
    [Tooltip("ALL of these nodes must be unlocked for this era to become reachable. " +
             "Leave empty for Era 1 — it is always reachable from the start.")]
    [SerializeField] private TechNode[] transitionNodes;

    [Header("Tween Settings")]
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private Ease fadeEase = Ease.OutCubic;

    // -----------------------------------------------------------------------
    //  Public state — read by TechCategoryFogManager if needed
    // -----------------------------------------------------------------------
    public bool IsEraReachable  { get; private set; }
    public bool IsGateUnlocked  { get; private set; }

    // -----------------------------------------------------------------------

    private void Awake()
    {
        // Safety: if no fog images assigned just warn and do nothing
        if (fogFull == null)
            Debug.LogWarning($"[TechEraFogController] {gameObject.name}: fogFull is not assigned.");
        if (fogPartial == null)
            Debug.LogWarning($"[TechEraFogController] {gameObject.name}: fogPartial is not assigned.");
    }

    /// <summary>
    /// Called by TechCategoryFogManager every time the tech tree needs to
    /// re-evaluate fog state.
    /// instant = true  → set alpha immediately (no tween), used on open/switch
    /// instant = false → DOTween fade, used after a purchase
    /// </summary>
    public void RefreshFogState(PlayerData player, bool instant)
    {
        if (player == null) return;

        IsEraReachable = CheckEraReachable(player);
        IsGateUnlocked = gateNode != null && gateNode.IsUnlockedBy(player);

        if (!IsEraReachable)
        {
            // Era not yet reachable — show FogFull, hide FogPartial
            SetFogAlpha(fogFull,    1f, instant);
            SetFogAlpha(fogPartial, 0f, instant);
        }
        else if (!IsGateUnlocked)
        {
            // Era reachable but gate not unlocked — hide FogFull, show FogPartial
            SetFogAlpha(fogFull,    0f, instant);
            SetFogAlpha(fogPartial, 1f, instant);
        }
        else
        {
            // Gate unlocked — hide both fogs entirely
            SetFogAlpha(fogFull,    0f, instant);
            SetFogAlpha(fogPartial, 0f, instant);
        }
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Era 1 is always reachable (no transition nodes needed).
    /// All other eras require ALL their transition nodes to be unlocked.
    /// </summary>
    private bool CheckEraReachable(PlayerData player)
    {
        if (transitionNodes == null || transitionNodes.Length == 0)
            return true; // Era 1 — always reachable

        foreach (var node in transitionNodes)
        {
            if (node != null && !node.IsUnlockedBy(player))
                return false;
        }
        return true;
    }

    private void SetFogAlpha(Image fog, float targetAlpha, bool instant)
    {
        if (fog == null) return;

        fog.DOKill();

        if (instant)
        {
            var c = fog.color;
            c.a = targetAlpha;
            fog.color = c;

            // Also disable the GameObject when fully transparent so it
            // doesn't block raycasts or waste rendering
            fog.gameObject.SetActive(targetAlpha > 0f);
        }
        else
        {
            // Make sure the object is active before tweening TO it
            if (targetAlpha > 0f)
                fog.gameObject.SetActive(true);

            fog.DOFade(targetAlpha, fadeDuration)
               .SetEase(fadeEase)
               .SetUpdate(true) // Works even when timeScale = 0
               .OnComplete(() =>
               {
                   if (targetAlpha <= 0f)
                       fog.gameObject.SetActive(false);
               });
        }
    }
}
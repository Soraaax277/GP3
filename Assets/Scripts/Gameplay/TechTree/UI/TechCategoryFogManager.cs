using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to each category panel (Hardware, Workforce, Services, Sabotage).
/// Owns and coordinates all TechEraFogController instances for that category.
/// Each category is fully self-contained — Hardware fog state has no knowledge
/// of Workforce fog state and vice versa.
/// 
/// TechTreeWindowManager calls RefreshAll() on the active category after
/// every purchase, on open, and on category switch.
/// </summary>
public class TechCategoryFogManager : MonoBehaviour
{
    [Header("Era Fog Controllers")]
    [Tooltip("Assign the TechEraFogController for each era in order (Era1, Era2, Era3, Era4). " +
             "Leave slots empty for eras that don't exist in this category.")]
    [SerializeField] private List<TechEraFogController> eraControllers;

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Refreshes fog state for every era in this category.
    /// instant = true  → immediate alpha set (no tween), used on open/switch
    /// instant = false → DOTween fade, used after a purchase
    /// </summary>
    public void RefreshAll(PlayerData player, bool instant)
    {
        if (player == null) return;
        if (eraControllers == null) return;

        foreach (var controller in eraControllers)
        {
            if (controller != null)
                controller.RefreshFogState(player, instant);
        }
    }

    /// <summary>
    /// Convenience overload — refreshes with animation (instant = false).
    /// </summary>
    public void RefreshAll(PlayerData player) => RefreshAll(player, false);
}
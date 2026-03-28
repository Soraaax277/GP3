using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
//  GridTransitionBootstrapper
//
//  PURPOSE:
//    GridTransitionManager uses DontDestroyOnLoad and is normally created when
//    the game starts from MainMenu. If you hit Play directly from GameScene
//    (or any non-MainMenu scene), GridTransitionManager never gets created and
//    VictoryManager silently falls back to its plain delay.
//
//    Drop this script on a GameObject in every scene that can be your entry
//    point (e.g. GameScene). Assign the same prefab you use in MainMenu.
//    It checks on Awake whether an instance already exists — if it does
//    (because you started from MainMenu normally), it does nothing.
//
//  SCENE SETUP:
//    1. Create an empty GameObject in GameScene, name it "GridTransitionBootstrapper".
//    2. Attach this script to it.
//    3. Drag your GridTransition prefab (the one that holds GridTransitionManager)
//       into the 'Grid Transition Prefab' field in the Inspector.
//    4. That's it. Do the same for any other scene you want to test from directly.
//
//  NOTE:
//    This does NOT need to be in MainMenu — MainMenu already instantiates the
//    prefab directly. Only add this to scenes you might start from in the Editor.
// ═══════════════════════════════════════════════════════════════════════════════

public class GridTransitionBootstrapper : MonoBehaviour
{
    [Tooltip("The root prefab that contains GridTransitionManager and its Canvas/GridLayoutGroup. " +
             "This is the same prefab you placed in your MainMenu scene.")]
    public GameObject gridTransitionPrefab;

    void Awake()
    {
        // Already alive from a previous scene — nothing to do.
        if (GridTransitionManager.Instance != null)
        {
            Debug.Log("[GridTransitionBootstrapper] GridTransitionManager already exists. Skipping.");
            return;
        }

        if (gridTransitionPrefab == null)
        {
            Debug.LogWarning("[GridTransitionBootstrapper] No prefab assigned! " +
                             "VictoryManager will fall back to plain delay. " +
                             "Assign the GridTransition prefab in the Inspector.");
            return;
        }

        Instantiate(gridTransitionPrefab);
        Debug.Log("[GridTransitionBootstrapper] GridTransitionManager instantiated " +
                  "because this scene was entered directly (not from MainMenu).");
    }
}

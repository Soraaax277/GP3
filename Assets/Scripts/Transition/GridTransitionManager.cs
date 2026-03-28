using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

public class GridTransitionManager : MonoBehaviour
{
    public static GridTransitionManager Instance;

    [Header("Configuration")]
    public GameObject cellPrefab;
    public int columns = 16;
    public int rows = 9;
    public float waveSpeed = 0.05f;
    public float fadeDuration = 0.5f;

    [Header("Grid Reference")]
    public GridLayoutGroup gridLayout;

    private List<RectTransform> cells = new List<RectTransform>();
    private bool isGridGenerated = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject.transform.parent.gameObject);

            // Set capacity high enough for all cells up front.
            // columns * rows = max concurrent tweens in the wave.
            // Add headroom for anything else in the project.
            DOTween.SetTweensCapacity(columns * rows + 200, 50);
        }
        else
        {
            Destroy(gameObject.transform.parent.gameObject);
        }
    }

    void Start()
    {
        StartCoroutine(GenerateGridRoutine());
    }

    IEnumerator GenerateGridRoutine()
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Canvas.ForceUpdateCanvases();
        yield return null;

        float width  = rt.rect.width;
        float height = rt.rect.height;

        Vector2 cellSize = new Vector2(width / columns, height / rows);

        gridLayout.cellSize        = cellSize;
        gridLayout.spacing         = Vector2.zero;
        gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;

        for (int i = 0; i < (columns * rows); i++)
        {
            GameObject cell = Instantiate(cellPrefab, transform);

            cell.transform.localScale    = Vector3.zero;
            cell.transform.localPosition = Vector3.zero;

            Destroy(cell.GetComponent<ContentSizeFitter>());
            Destroy(cell.GetComponent<LayoutElement>());

            cells.Add(cell.GetComponent<RectTransform>());
        }

        isGridGenerated = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC ENTRY POINT
    //  Covers the screen with the grid wipe, loads the scene asynchronously,
    //  then reveals the new scene by animating the grid back out.
    //  Two-way: cover → load → reveal.
    // ─────────────────────────────────────────────────────────────────────────
    public void LoadScene(string sceneName)
    {
        if (!isGridGenerated)
        {
            Debug.LogWarning("[GridTransitionManager] Grid not ready yet, loading instantly.");
            SceneManager.LoadScene(sceneName);
            return;
        }

        AnimateGrid(true, () => StartCoroutine(LoadAndReveal(sceneName)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Waits for the scene to finish loading, THEN plays the reveal.
    //  Using LoadSceneAsync means we know exactly when the scene is ready
    //  before we start animating the cells away.
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator LoadAndReveal(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        // Keep the screen covered (cells at full scale) while loading.
        while (!op.isDone)
            yield return null;

        // Scene is fully loaded and active. Now reveal it.
        AnimateGrid(false, null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ANIMATE GRID
    //  show = true  → cells scale up  (cover,   plays before scene load)
    //  show = false → cells scale down (reveal,  plays after scene load)
    // ─────────────────────────────────────────────────────────────────────────
    private void AnimateGrid(bool show, System.Action onComplete)
    {
        // Kill any in-progress grid animation before starting a new one.
        // Tag-based kill so we don't nuke unrelated project tweens.
        DOTween.Kill("GridTransition");

        float endScale = show ? 1.05f : 0f;

        Sequence seq = DOTween.Sequence()
                              .SetUpdate(true)
                              .SetId("GridTransition");

        for (int i = 0; i < cells.Count; i++)
        {
            int x       = i % columns;
            int y       = i / columns;
            int visualY = (rows - 1) - y; // top-left origin

            float delay = (x + visualY) * waveSpeed;

            seq.Insert(delay, cells[i].DOScale(endScale, fadeDuration)
                                      .SetEase(Ease.InOutQuad)
                                      .SetUpdate(true));
        }

        seq.OnComplete(() => onComplete?.Invoke());
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("UI Blocking")]
    [Tooltip("Drag your UnitActionPanel and BuildUIManager Panel here. If any are active, movement is blocked.")]
    public List<GameObject> blockingPanels = new List<GameObject>();

    [Tooltip("Drag panels here (e.g. ActiveResearchPanel) that should block scroll and drag " +
             "only while the mouse is physically hovering over them. " +
             "Unlike blockingPanels, these do NOT block camera movement when merely visible.")]
    public List<RectTransform> hoverBlockingPanels = new List<RectTransform>();

    [Header("Movement Settings")]
    public float panSpeed = 20f;  
    public float scrollSpeed = 20f;
    public float minY = 8f; 
    public float maxY = 35f; 

    [Header("Build Mode (Focus) Settings")]
    public float buildHeight = 25f;     
    public float buildDistance = 20f;  
    public float lockTransitionTime = 0.5f; 

    // Cutscene Mode flag 
    [Header("Cutscene Settings")]
    public bool cutsceneMode = false;

    private Vector3 leftDragOrigin;
    private Vector3 rightDragOrigin;
    private Vector3 rotationEuler;
    private Vector2 panLimitX;
    private Vector2 panLimitZ;
    
    private bool isTransitioning = false;
    private Coroutine activeRoutine;

    /// <summary>Set to true whenever any text input field is active so WASD does not move the camera.</summary>
    public static bool IsTyping = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GridManager.Instance != null)
        {
            float hexWidth = GridManager.Instance.hexSize * 2f;
            float hexHeight = Mathf.Sqrt(3f) * GridManager.Instance.hexSize;
            panLimitX = new Vector2(0f, GridManager.Instance.width * hexWidth);
            panLimitZ = new Vector2(0f, GridManager.Instance.height * hexHeight);
        }
        
        // Initialize rotation state
        rotationEuler = transform.eulerAngles;
    }

    private void LateUpdate()
    {
        // THE LOCK
        // If any UI panel is active or we are animating, block inputs.
        if (IsBlockedByUI()) return;
        if (PauseMenuUI.GameIsPaused) return;

        // 2. Normal Movement
        HandleMovement();
    }

    private bool IsBlockedByUI()
    {
        // Block input if in cutscene mode (AI Turn)
        if (cutsceneMode) return true;

        if (blockingPanels != null)
        {
            foreach (var panel in blockingPanels)
            {
                // Ensure the panel is actually active before blocking
                if (panel != null && panel.activeInHierarchy) return true;
            }
        }
        
        if (isTransitioning) return true;

        return false;
    }

    // Returns true if the mouse cursor is currently inside any of the
    // hoverBlockingPanels RectTransforms. Used to suppress scroll and
    // left-drag when the player is interacting with a UI scroll rect.
    private bool IsMouseOverHoverPanel()
    {
        if (hoverBlockingPanels == null || hoverBlockingPanels.Count == 0) return false;

        // We need the camera that renders the UI Canvas. For Screen Space Overlay
        // canvases the camera argument is null; for Screen Space Camera or World
        // Space canvases pass the canvas camera. Null works for Overlay mode.
        foreach (var rect in hoverBlockingPanels)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) continue;

            // Determine the canvas camera for this panel (null = Overlay canvas).
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera canvasCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, canvasCam))
                return true;
        }
        return false;
    }

    private void HandleMovement()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 pos = transform.position;

        // WASD — never blocked by hover panels; only affects pan, not scroll.
        // Skip entirely while a text field (e.g. company rename) has keyboard focus.
        if (!IsTyping)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            pos += transform.right   * h * panSpeed * Time.deltaTime;
            pos += transform.forward * v * panSpeed * Time.deltaTime;
        }

        bool hoverBlocked = IsMouseOverHoverPanel();

        // Mouse Drag (Left) — suppressed while hovering a UI scroll panel.
        if (!hoverBlocked)
        {
            if (Input.GetMouseButtonDown(0)) leftDragOrigin = Input.mousePosition;
            if (Input.GetMouseButton(0))
            {
                Vector3 difference = Input.mousePosition - leftDragOrigin;
                Vector3 move = new Vector3(-difference.x, 0f, -difference.y) * panSpeed * Time.deltaTime * 0.1f;
                pos += transform.TransformDirection(move);
                leftDragOrigin = Input.mousePosition;
            }
        }
        else
        {
            // Keep the drag origin in sync so releasing over the panel and then
            // dragging elsewhere doesn't cause a sudden camera jump.
            leftDragOrigin = Input.mousePosition;
        }

        // Mouse Rotation (Right)
        if (Input.GetMouseButtonDown(1))
        {
            rightDragOrigin = Input.mousePosition;
            // Sync state just in case
            rotationEuler = transform.eulerAngles;
        }
        if (Input.GetMouseButton(1))
        {
            Vector3 difference = Input.mousePosition - rightDragOrigin;
            rotationEuler.y += difference.x * 0.2f;
            rotationEuler.x -= difference.y * 0.2f;
            rotationEuler.x = Mathf.Clamp(rotationEuler.x, 10f, 80f); 
            transform.rotation = Quaternion.Euler(rotationEuler);
            rightDragOrigin = Input.mousePosition;
        }

        // Zoom — suppressed while hovering a UI scroll panel so the
        // ScrollRect can consume the scroll wheel event instead.
        float scroll = hoverBlocked ? 0f : Input.GetAxis("Mouse ScrollWheel");
        pos.y -= scroll * scrollSpeed * 100f * Time.deltaTime;
        
        // Clamping
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        if (panLimitX != Vector2.zero)
        {
            pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
            pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);
        }

        transform.position = pos;
    }

    // This allows GameManager to call FocusOnPosition with 4 arguments, 
    // but we ignore the extra numbers to enforce Inspector settings for consistency.
    public void FocusOnPosition(Vector3 target, float h, float d, float s)
    {
        FocusOnPosition(target);
    }

    // MAIN FOCUS METHOD
    public void FocusOnPosition(Vector3 target)
    {
        SetBuildModeLock(true, target);
    }

    public void SetBuildModeLock(bool active, Vector3 target = default)
    {
        if (active)
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(TransitionToLockedView(target));
        }
        else
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            isTransitioning = false;
            
            // Sync state so we don't snap back when moving mouse
            rotationEuler = transform.eulerAngles;
        }
    }

    private IEnumerator TransitionToLockedView(Vector3 target)
    {
        isTransitioning = true;
        
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // Calculate direction: flattening Y ensures we don't dive into the ground
        Vector3 currentDir = transform.forward;
        currentDir.y = 0; 
        currentDir.Normalize();
        
        // Fallback if looking straight down/up to avoid zero vector errors
        if (currentDir.sqrMagnitude < 0.01f) currentDir = Vector3.forward;

        // Calculate ideal position
        Vector3 offset = (currentDir * -buildDistance) + (Vector3.up * buildHeight);
        Vector3 desiredPos = target + offset;
        Quaternion desiredRot = Quaternion.LookRotation(target - desiredPos);

        float elapsed = 0f;
        while (elapsed < lockTransitionTime)
        {
            float t = elapsed / lockTransitionTime;
            t = t * t * (3f - 2f * t); // SmoothStep

            transform.position = Vector3.Lerp(startPos, desiredPos, t);
            transform.rotation = Quaternion.Slerp(startRot, desiredRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Finalize position and rotation
        transform.position = desiredPos;
        // Use desiredRot explicitly to match the end of the Slerp
        transform.rotation = desiredRot; 
        
        // Update the internal rotation variable.
        // If we don't do this, the next time HandleMovement() runs, 
        // it will snap the camera back to the old rotationEuler value.
        rotationEuler = transform.eulerAngles;
        
        isTransitioning = false;
    }
}
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    public enum CameraState { Idle, Panning, Rotating }
    private CameraState currentState = CameraState.Idle;

    [Header("Cursor Settings")]
    [Tooltip("Leave empty to use the system default cursor, or assign a custom idle pointer.")]
    public Texture2D defaultCursor;
    [Tooltip("Cursor to show when holding Left Mouse Button to pan.")]
    public Texture2D dragCursor;
    [Tooltip("Cursor to show when holding Right Mouse Button to rotate.")]
    public Texture2D rotateCursor;
    [Tooltip("The pixel coordinate of the cursor's click point. Default is (0,0) top-left.")]
    public Vector2 cursorHotspot = Vector2.zero;

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

    [Header("Smoothing Settings")]
    [Tooltip("Enable or disable camera smoothing.")]
    public bool enableSmoothing = true;
    [Tooltip("Higher values make the camera position catch up faster.")]
    public float positionSmoothSpeed = 10f;
    [Tooltip("Higher values make the camera rotation catch up faster.")]
    public float rotationSmoothSpeed = 15f;

    [Header("Build Mode (Focus) Settings")]
    public float buildHeight = 25f;     
    public float buildDistance = 20f;  
    public float lockTransitionTime = 0.5f; 

    [Header("Startup Lock")]
    [Tooltip("Blocks camera input on scene start. Match to GridTransitionManager.glitchOutDuration + EraAnnouncementController.startupDelay (default: 1.2 + 1.5 = 2.7).")]
    public float startupLockDuration = 2.7f;
    private bool _startupLocked = true;

    // Cutscene Mode flag
    [Header("Cutscene Settings")]
    [SerializeField] private bool _cutsceneMode = false;

    /// <summary>
    /// When set to true, immediately stops any in-flight camera transition coroutine
    /// and prevents CameraController from touching the transform — giving VictoryManager
    /// (or any other system) exclusive control over the camera.
    /// </summary>
    public bool cutsceneMode
    {
        get => _cutsceneMode;
        set
        {
            if (_cutsceneMode == value) return;
            _cutsceneMode = value;

            if (value)
            {
                // Kill any active transition so it cannot fight an external camera sequence.
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                    activeRoutine = null;
                }
                isTransitioning = false;
                SyncTargets();
            }
            else
            {
                // Sync state so HandleMovement won't snap when control returns from an external system.
                SyncTargets();
            }
        }
    }

    private Vector3 targetPosition;
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
        SyncTargets();
    }

    private IEnumerator Start()
    {
        // Set the initial cursor
        SetCameraState(CameraState.Idle);

        // Wait for GridManager to finish generating the world continent.
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        // ── CALCULATE DYNAMIC WORLD LIMITS ──────────────────────────────────
        if (GridManager.Instance != null && GridManager.Instance.tiles != null)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool foundTiles = false;

            foreach (var tile in GridManager.Instance.GetAllTiles())
            {
                if (tile == null) continue;
                Vector3 p = tile.transform.position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
                foundTiles = true;
            }

            if (foundTiles)
            {
                float buffer = 5f;
                panLimitX = new Vector2(minX - buffer, maxX + buffer);
                panLimitZ = new Vector2(minZ - buffer, maxZ + buffer);
            }
        }
        
        SyncTargets();

        // Lock camera for the grid-out + era announcement window
        if (startupLockDuration > 0f)
            yield return StartCoroutine(StartupLockRoutine());
    }

    private IEnumerator StartupLockRoutine()
    {
        _startupLocked = true;
        yield return new WaitForSecondsRealtime(startupLockDuration);
        _startupLocked = false;
        SyncTargets();
    }

    private void SyncTargets()
    {
        targetPosition = transform.position;
        rotationEuler = transform.eulerAngles;
    }

    private void LateUpdate()
    {
        // THE LOCK
        if (IsBlockedByUI())
        {
            // Ensure cursor resets to normal if UI opens while dragging
            SetCameraState(CameraState.Idle); 
            return;
        }

        // 2. Normal Movement
        HandleMovement();
    }

    private bool IsBlockedByUI()
    {
        if (_cutsceneMode) return true;
        if (_startupLocked) return true;

        if (blockingPanels != null)
        {
            foreach (var panel in blockingPanels)
            {
                if (panel != null && panel.activeInHierarchy) return true;
            }
        }
        
        if (isTransitioning) return true;

        return false;
    }

    private bool IsMouseOverHoverPanel()
    {
        if (hoverBlockingPanels == null || hoverBlockingPanels.Count == 0) return false;

        foreach (var rect in hoverBlockingPanels)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) continue;

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera canvasCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, Mouse.current.position.ReadValue(), canvasCam))
                return true;
        }
        return false;
    }

    private void HandleMovement()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) 
        {
            SetCameraState(CameraState.Idle);
            return;
        }

        // Default state for this frame
        CameraState intendedState = CameraState.Idle;

        // WASD
        if (!IsTyping)
        {
            var kb = Keyboard.current;
            float h = 0f, v = 0f;
            if (kb != null)
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
            }
            
            targetPosition += transform.right   * h * panSpeed * Time.deltaTime;
            targetPosition += transform.forward * v * panSpeed * Time.deltaTime;
        }

        bool hoverBlocked = IsMouseOverHoverPanel();

        // Mouse Drag (Left)
        if (!hoverBlocked)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) leftDragOrigin = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.isPressed)
            {
                intendedState = CameraState.Panning; // Set intent to Pan

                Vector3 difference = (Vector3)Mouse.current.position.ReadValue() - leftDragOrigin;
                Vector3 move = new Vector3(-difference.x, 0f, -difference.y) * panSpeed * Time.deltaTime * 0.1f;
                targetPosition += transform.TransformDirection(move);
                leftDragOrigin = Mouse.current.position.ReadValue();
            }
        }
        else
        {
            leftDragOrigin = Mouse.current.position.ReadValue();
        }

        // Mouse Rotation (Right)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            rightDragOrigin = Mouse.current.position.ReadValue();
            rotationEuler = transform.eulerAngles; 
        }
        if (Mouse.current.rightButton.isPressed)
        {
            intendedState = CameraState.Rotating; // Overrides Pan intent if both are held

            Vector3 difference = (Vector3)Mouse.current.position.ReadValue() - rightDragOrigin;
            rotationEuler.y += difference.x * 0.2f;
            rotationEuler.x -= difference.y * 0.2f;
            rotationEuler.x = Mathf.Clamp(rotationEuler.x, 10f, 80f); 
            rightDragOrigin = Mouse.current.position.ReadValue();
        }

        // Apply calculated cursor state
        SetCameraState(intendedState);

        // Zoom 
        float scroll = hoverBlocked ? 0f : Mouse.current.scroll.ReadValue().y * 0.1f;
        targetPosition.y -= scroll * scrollSpeed * 100f * Time.deltaTime;
        
        // Clamping Targets
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        if (panLimitX != Vector2.zero)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, panLimitX.x, panLimitX.y);
            targetPosition.z = Mathf.Clamp(targetPosition.z, panLimitZ.x, panLimitZ.y);
        }

        // Apply Smoothing
        if (enableSmoothing)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionSmoothSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(rotationEuler), Time.deltaTime * rotationSmoothSpeed);
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = Quaternion.Euler(rotationEuler);
        }
    }

    /// <summary>
    /// Changes the hardware cursor based on the current action, only updating when the state changes.
    /// </summary>
    private void SetCameraState(CameraState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        switch (currentState)
        {
            case CameraState.Idle:
                Cursor.SetCursor(defaultCursor, cursorHotspot, CursorMode.Auto);
                break;
            case CameraState.Panning:
                Cursor.SetCursor(dragCursor, cursorHotspot, CursorMode.Auto);
                break;
            case CameraState.Rotating:
                Cursor.SetCursor(rotateCursor, cursorHotspot, CursorMode.Auto);
                break;
        }
    }

    public void FocusOnPosition(Vector3 target, float h, float d, float s)
    {
        FocusOnPosition(target);
    }

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
            SyncTargets();
        }
    }

    private IEnumerator TransitionToLockedView(Vector3 target)
    {
        isTransitioning = true;
        SetCameraState(CameraState.Idle); // Clear cursor during automated transit
        
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 currentDir = transform.forward;
        currentDir.y = 0; 
        currentDir.Normalize();
        
        if (currentDir.sqrMagnitude < 0.01f) currentDir = Vector3.forward;

        Vector3 offset = (currentDir * -buildDistance) + (Vector3.up * buildHeight);
        Vector3 desiredPos = target + offset;
        Quaternion desiredRot = Quaternion.LookRotation(target - desiredPos);

        float elapsed = 0f;
        while (elapsed < lockTransitionTime)
        {
            if (_cutsceneMode)
            {
                isTransitioning = false;
                SyncTargets();
                yield break;
            }

            float t = elapsed / lockTransitionTime;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, desiredPos, t);
            transform.rotation = Quaternion.Slerp(startRot, desiredRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = desiredPos;
        transform.rotation = desiredRot; 
        
        SyncTargets();
        
        isTransitioning = false;
    }
}
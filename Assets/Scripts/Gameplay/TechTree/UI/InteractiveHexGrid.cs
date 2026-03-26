using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HexGridInteraction : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("Time (seconds) to reach the mouse target.")]
    public float smoothTime = 0.15f; 
    
    [Tooltip("Max speed of the highlight effect.")]
    public float maxSpeed = 3000.0f; 

    [Header("Behavior")]
    public bool autoAspectRatio = false; 

    private RawImage _rawImage;
    private RectTransform _rectTransform;
    private Material _mat;
    
    // Shader Property IDs
    private int _mousePosID;
    private int _aspectID;
    private int _timeID; 

    // SmoothDamp state variables
    private Vector2 _currentVelocity;
    private Vector2 _currentSmoothUV = new Vector2(-1, -1);
    private Vector2 _targetUV = new Vector2(-1, -1);

    void Awake()
    {
        // Cache components
        _rawImage = GetComponent<RawImage>();
        _rectTransform = GetComponent<RectTransform>();
        
        // Cache Shader IDs
        _mousePosID = Shader.PropertyToID("_MouseUV");
        _aspectID = Shader.PropertyToID("_AspectRatio");
        _timeID = Shader.PropertyToID("_UI_UnscaledTime");
    }

    void OnEnable()
    {
        // Ensure a unique material instance exists when the object is enabled
        if (_rawImage != null && (_mat == null || _rawImage.material != _mat))
        {
            if (_rawImage.material != null)
            {
                _mat = new Material(_rawImage.material);
                _rawImage.material = _mat;
            }
        }

        // Reset trail position to prevent visual jumping
        _currentSmoothUV = new Vector2(-1, -1);
        _targetUV = new Vector2(-1, -1);
        _currentVelocity = Vector2.zero;
    }

    void Update()
    {
        if (_mat == null) return;

        // Use unscaledDeltaTime so animation continues even if Time.timeScale is 0
        float deltaTime = Time.unscaledDeltaTime;

        // Sync unscaled time to the shader for pulsing/scrolling effects
        _mat.SetFloat(_timeID, Time.unscaledTime);

        // Input Logic
        Camera uiCam = null;
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCam = parentCanvas.worldCamera;
        }

        // Convert mouse position to local RectTransform UV coordinates (0 to 1)
        Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, mouseScreenPos, uiCam, out Vector2 localPoint))
        {
            float nX = (localPoint.x - _rectTransform.rect.x) / _rectTransform.rect.width;
            float nY = (localPoint.y - _rectTransform.rect.y) / _rectTransform.rect.height;

            if (nX >= 0 && nX <= 1 && nY >= 0 && nY <= 1)
            {
                _targetUV = new Vector2(nX, nY);
            }
        }

        // Smooth Movement
        // Interpolate current position towards target using unscaled time
        _currentSmoothUV = Vector2.SmoothDamp(
            _currentSmoothUV, 
            _targetUV, 
            ref _currentVelocity, 
            smoothTime, 
            maxSpeed, 
            deltaTime
        );

        // Update Shader Properties
        _mat.SetVector(_mousePosID, _currentSmoothUV);
        
        if (autoAspectRatio && _rectTransform.rect.height > 0)
        {
            float aspect = _rectTransform.rect.width / _rectTransform.rect.height;
            _mat.SetFloat(_aspectID, aspect);
        }
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
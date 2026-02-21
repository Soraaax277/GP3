using UnityEngine;
using UnityEngine.UI;

public class UICRTEffect : MonoBehaviour
{
    // TARGETS
    [Header("Drag Buttons/Images Here")]
    public Graphic[] targets;

    // MATCHING SHADER PROPERTIES
    [Header("Screen Shape")]
    [Range(-0.5f, 0.5f)] public float curveX = 0.1f;
    [Range(-0.5f, 0.5f)] public float curveY = 0.1f;
    [Range(0.5f, 1.5f)] public float zoom = 0.95f;

    [Header("The Glitch")]
    [Tooltip("How often the screen glitches (Matches _GlitchFrequency)")]
    [Range(0f, 10f)] public float glitchFrequency = 3.0f;

    [Tooltip("How far the UI tears horizontally (Matches _GlitchTearing)")]
    [Range(0f, 100f)] public float glitchTearing = 20f; // Multiplied up for vertex units

    [Tooltip("White Noise Jitter Strength")]
    [Range(0f, 10f)] public float noiseStrength = 1.0f;

    [Header("Roll & Scanline Jitter")]
    [Tooltip("Rolling Wave Speed (Matches _RollSpeed)")]
    public float rollSpeed = -0.5f;

    [Tooltip("How much the roll distorts the mesh")]
    [Range(0f, 20f)] public float rollStrength = 0.0f; // Defaults to 0 as shader uses color

    [Tooltip("Vertical vibration mimicking scanline instability")]
    [Range(0f, 5f)] public float scanlineJitter = 1.0f;

    // INTERNAL STATE (Calculated once per frame)
    private float _glitchBarY;     // Where is the glitch bar right now?
    private float _tearAmount;     // How much should we shift X?
    private bool _isGlitching;     // Are we glitching this frame?
    private float _timer;

    private void Start()
    {
        if (targets != null)
        {
            foreach (var target in targets)
            {
                if (target != null)
                {
                    // Clean up old hooks first just in case
                    var oldHook = target.GetComponent<CRTRuntimeHook>();
                    if (oldHook == null)
                    {
                        var newHook = target.gameObject.AddComponent<CRTRuntimeHook>();
                        newHook.hideFlags = HideFlags.DontSave; // GHOST MODE: Don't save to scene
                        newHook.Setup(this);
                    }
                    else
                    {
                         oldHook.Setup(this);
                    }
                }
            }
        }
    }

    private void Update()
    {
        // CALCULATE GLITCH STATE (Once per frame, shared by all UI)
        // This mimics the shader's "floor(glitchTime)" logic
        float timeVal = Time.time * glitchFrequency;
        float discreteTime = Mathf.Floor(timeVal);

        // Randomly decide if this time block is a glitch
        // pseudo-random hash based on time so it's consistent across frames
        float randomTrigger = Mathf.PerlinNoise(discreteTime, 0.0f);
        _isGlitching = randomTrigger > 0.6f; // Threshold

        if (_isGlitching)
        {
            // Position the bar randomly (0 to 1)
            _glitchBarY = Mathf.PerlinNoise(0.0f, discreteTime);
            
            // Calculate a random tear amount (Positive or Negative)
            _tearAmount = (Mathf.PerlinNoise(discreteTime, 1.0f) - 0.5f) * glitchTearing;
        }
        else
        {
            _tearAmount = 0;
        }
    }

    // MATH
    public Vector3 DistortPoint(Vector3 worldPoint)
    {
        if (this == null) return worldPoint;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) return worldPoint;

        Vector3 local = rect.InverseTransformPoint(worldPoint);
        Vector2 size = rect.rect.size;

        if (size.x == 0 || size.y == 0) return worldPoint;

        // Normalize (-1 to 1 range is easier for curves, but 0-1 is better for bars)
        // Let's stick to -1 to 1 for the Curve Math
        Vector2 n = new Vector2(local.x / (size.x * 0.5f), local.y / (size.y * 0.5f));

        // APPLY CURVE (Matches Shader CurveUV)
        float x = n.x + (n.x * (n.y * n.y) * curveX);
        float y = n.y + (n.y * (n.x * n.x) * curveY);

        x *= zoom;
        y *= zoom;

        // APPLY GLITCH (The Tearing)
        if (_isGlitching)
        {
            // Convert y (-1 to 1) to (0 to 1) for comparison with _glitchBarY
            float normalizedY = y * 0.5f + 0.5f;

            // Check if this vertex is inside the horizontal "Bar"
            // Bar thickness is approx 10% of screen
            if (Mathf.Abs(normalizedY - _glitchBarY) < 0.1f)
            {
                x += _tearAmount * 0.1f; // Apply the horizontal tear
                
                // Add Noise inside the bar
                x += (Random.value - 0.5f) * noiseStrength * 0.1f;
                y += (Random.value - 0.5f) * noiseStrength * 0.1f;
            }
        }

        // APPLY ROLL (Sine wave offset)
        if (rollStrength > 0)
        {
            float roll = Mathf.Sin(y * 3.0f + (Time.time * rollSpeed));
            x += roll * (rollStrength * 0.01f);
        }

        // SCANLINE JITTER (High frequency vertical vibration)
        if (scanlineJitter > 0)
        {
            y += (Random.value - 0.5f) * scanlineJitter * 0.01f;
        }

        // Convert back to World Space
        Vector3 resultLocal = new Vector3(x * (size.x * 0.5f), y * (size.y * 0.5f), local.z);
        return rect.TransformPoint(resultLocal);
    }
}
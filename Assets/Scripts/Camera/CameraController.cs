using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;      // speed for WASD / left drag

    [Header("Zoom Settings")]
    public float scrollSpeed = 20f;
    public float minY = 10f; // closest zoom
    public float maxY = 80f; // farthest zoom

    [Header("Optional Tilt")]
    public float tiltAngle = 45f;

    private Vector3 leftDragOrigin;
    private Vector3 rightDragOrigin;
    private Vector3 rotationEuler;

    private Vector2 panLimitX;
    private Vector2 panLimitZ;

    private void Start()
    {
        // --- Calculate pan limits dynamically based on GridManager ---
        if (GridManager.Instance != null)
        {
            float hexWidth = GridManager.Instance.hexSize * 2f;
            float hexHeight = Mathf.Sqrt(3f) * GridManager.Instance.hexSize;

            float gridWidth = GridManager.Instance.width * hexWidth;
            float gridHeight = GridManager.Instance.height * hexHeight;

            panLimitX = new Vector2(0f, gridWidth);
            panLimitZ = new Vector2(0f, gridHeight);
        }
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.position;

        // --- Panning via WASD or Arrow Keys ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        pos += transform.right * h * panSpeed * Time.deltaTime;
        pos += transform.forward * v * panSpeed * Time.deltaTime;

        // --- Left Mouse Drag: Pan map freely ---
        if (Input.GetMouseButtonDown(0))
            leftDragOrigin = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            Vector3 difference = Input.mousePosition - leftDragOrigin;
            Vector3 move = new Vector3(-difference.x, 0f, -difference.y) * panSpeed * Time.deltaTime * 0.1f;
            pos += transform.TransformDirection(move);
            leftDragOrigin = Input.mousePosition;
        }

        // --- Right Mouse Drag: Rotate camera like Unity ---
        if (Input.GetMouseButtonDown(1))
        {
            rightDragOrigin = Input.mousePosition;
            rotationEuler = transform.eulerAngles;
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 difference = Input.mousePosition - rightDragOrigin;
            float rotationSpeed = 0.2f;

            rotationEuler.y += difference.x * rotationSpeed;
            rotationEuler.x -= difference.y * rotationSpeed;
            rotationEuler.x = Mathf.Clamp(rotationEuler.x, 10f, 80f); // clamp vertical tilt

            transform.rotation = Quaternion.Euler(rotationEuler);
            rightDragOrigin = Input.mousePosition;
        }

        // --- Zoom ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        pos.y -= scroll * scrollSpeed * 100f * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // --- Clamp position to map bounds dynamically ---
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;

        // --- Note: no forced tilt for left drag anymore ---
    }
}

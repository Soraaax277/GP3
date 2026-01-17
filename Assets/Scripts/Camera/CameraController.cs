using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;  

    [Header("Zoom Settings")]
    public float scrollSpeed = 20f;
    public float minY = 10f; 
    public float maxY = 80f; 

    [Header("Optional Tilt")]
    public float tiltAngle = 45f;

    private Vector3 leftDragOrigin;
    private Vector3 rightDragOrigin;
    private Vector3 rotationEuler;

    private Vector2 panLimitX;
    private Vector2 panLimitZ;

    public static CameraController Instance;

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

            float gridWidth = GridManager.Instance.width * hexWidth;
            float gridHeight = GridManager.Instance.height * hexHeight;

            panLimitX = new Vector2(0f, gridWidth);
            panLimitZ = new Vector2(0f, gridHeight);
        }
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.position;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        pos += transform.right * h * panSpeed * Time.deltaTime;
        pos += transform.forward * v * panSpeed * Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
            leftDragOrigin = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            Vector3 difference = Input.mousePosition - leftDragOrigin;
            Vector3 move = new Vector3(-difference.x, 0f, -difference.y) * panSpeed * Time.deltaTime * 0.1f;
            pos += transform.TransformDirection(move);
            leftDragOrigin = Input.mousePosition;
        }

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
            rotationEuler.x = Mathf.Clamp(rotationEuler.x, 10f, 80f); 

            transform.rotation = Quaternion.Euler(rotationEuler);
            rightDragOrigin = Input.mousePosition;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        pos.y -= scroll * scrollSpeed * 100f * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;

    }


    public void FocusOnPosition(Vector3 target, float distance = 5f, float height = 3f, float duration = 1f)
    {
        StartCoroutine(FocusSmooth(target, distance, height, duration));
    }

    private IEnumerator FocusSmooth(Vector3 target, float distance, float height, float duration)
    {
        Vector3 startPos = transform.position;

        Vector3 offset = new Vector3(-distance, height, -distance);
        Vector3 desiredPos = target + offset;

        desiredPos.x = Mathf.Clamp(desiredPos.x, panLimitX.x + distance, panLimitX.y - distance);
        desiredPos.z = Mathf.Clamp(desiredPos.z, panLimitZ.x + distance, panLimitZ.y - distance);
        desiredPos.y = Mathf.Clamp(desiredPos.y, minY, maxY);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, desiredPos, elapsed / duration);
            transform.LookAt(target);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = desiredPos;
        transform.LookAt(target);
    }

}

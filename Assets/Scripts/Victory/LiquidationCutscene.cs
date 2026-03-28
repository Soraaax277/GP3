using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LiquidationCutscene : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("Assign your 3 cameras here")]
    public Camera[] cameras;
    [Tooltip("Total time each camera stays on screen before the next fade starts")]
    public float switchInterval = 5f;

    [Header("Transition Settings")]
    [Tooltip("Attach a CanvasGroup containing a full-screen black/white Image here")]
    public CanvasGroup cameraFadeOverlay;
    [Tooltip("How long it takes to fade to solid color, and then back to clear")]
    public float transitionFadeDuration = 0.5f;

    [Header("Handheld Camera Settings")]
    [Tooltip("How fast the camera drifts/wobbles")]
    public float wobbleSpeed = 0.5f;
    [Tooltip("How much the camera rotates (pitch/yaw)")]
    public float wobbleRotationAmount = 1.5f;
    [Tooltip("How much the camera physically shifts (X/Y)")]
    public float wobblePositionAmount = 0.1f;

    [Header("UI Settings")]
    public CanvasGroup victoryPanel; 
    [Tooltip("How long to wait before the panel starts fading in.")]
    public float victoryPanelDelay = 8f; 
    public float victoryFadeDuration = 2f;

    [Header("Scene Transition")]
    [Tooltip("The scene to load when the player clicks the victory panel.")]
    public string returnSceneName = "GameScene";

    // Tracking variables for multiple cameras
    private Vector3[]    originalPositions;
    private Quaternion[] originalRotations;
    private int          currentCameraIndex = 0;

    // Panel click state
    private bool panelReady      = false;
    private bool transitionFired = false;

    void Start()
    {
        if (victoryPanel != null)     victoryPanel.alpha     = 0f;
        if (cameraFadeOverlay != null) cameraFadeOverlay.alpha = 0f;

        if (cameras != null && cameras.Length > 0)
        {
            originalPositions = new Vector3[cameras.Length];
            originalRotations = new Quaternion[cameras.Length];

            for (int i = 0; i < cameras.Length; i++)
            {
                originalPositions[i] = cameras[i].transform.position;
                originalRotations[i] = cameras[i].transform.rotation;
                cameras[i].gameObject.SetActive(i == 0);
            }

            StartCoroutine(SwitchCamerasRoutine());
            StartCoroutine(AnimateCamerasRoutine());
        }
        else
        {
            Debug.LogWarning("LiquidationCutscene: No cameras assigned in the inspector!");
        }

        if (victoryPanel != null)
            StartCoroutine(FadeInVictoryPanelRoutine());
    }

    void Update()
    {
        if (!panelReady || transitionFired) return;
        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

        transitionFired = true;

        if (GridTransitionManager.Instance != null)
            GridTransitionManager.Instance.LoadScene("MainMenuScene");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(returnSceneName);
    }

    private IEnumerator SwitchCamerasRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, switchInterval - transitionFadeDuration));

            // Fade out
            if (cameraFadeOverlay != null)
            {
                float t = 0f;
                while (t < transitionFadeDuration)
                {
                    cameraFadeOverlay.alpha = t / transitionFadeDuration;
                    t += Time.deltaTime;
                    yield return null;
                }
                cameraFadeOverlay.alpha = 1f;
            }

            // Switch
            cameras[currentCameraIndex].gameObject.SetActive(false);
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            cameras[currentCameraIndex].gameObject.SetActive(true);

            // Fade in
            if (cameraFadeOverlay != null)
            {
                float t = 0f;
                while (t < transitionFadeDuration)
                {
                    cameraFadeOverlay.alpha = 1f - (t / transitionFadeDuration);
                    t += Time.deltaTime;
                    yield return null;
                }
                cameraFadeOverlay.alpha = 0f;
            }
        }
    }

    private IEnumerator AnimateCamerasRoutine()
    {
        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(0f, 100f);

        while (true)
        {
            float noiseX = (Mathf.PerlinNoise(Time.time * wobbleSpeed + seedX, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * wobbleSpeed + seedY) - 0.5f) * 2f;

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].transform.position = originalPositions[i] + new Vector3(noiseX * wobblePositionAmount, noiseY * wobblePositionAmount, 0f);
                cameras[i].transform.rotation = originalRotations[i] * Quaternion.Euler(noiseY * wobbleRotationAmount, noiseX * wobbleRotationAmount, 0f);
            }

            yield return null;
        }
    }

    private IEnumerator FadeInVictoryPanelRoutine()
    {
        yield return new WaitForSeconds(victoryPanelDelay);

        float elapsedTime = 0f;
        while (elapsedTime < victoryFadeDuration)
        {
            victoryPanel.alpha = elapsedTime / victoryFadeDuration;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        victoryPanel.alpha = 1f;
        panelReady = true; // Click is now live
    }
}
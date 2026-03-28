using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonopolyCutscene : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Attach your Quad Prefab here")]
    public GameObject quadPrefab;
    [Tooltip("Attach your URP Unlit Material here")]
    public Material quadMaterial;

    [Header("Camera Settings")]
    public float startFOV = 20f;
    public float endFOV = 150f;

    [Header("Handheld Camera Settings")]
    [Tooltip("How fast the camera drifts/wobbles")]
    public float wobbleSpeed = 0.5f;
    [Tooltip("How much the camera rotates (pitch/yaw)")]
    public float wobbleRotationAmount = 1.5f;
    [Tooltip("How much the camera physically shifts (X/Y)")]
    public float wobblePositionAmount = 0.1f;
    [Tooltip("At what percentage of the zoom should the wobble start? (0.5 = 50%)")]
    [Range(0f, 1f)]
    public float wobbleStartThreshold = 0.5f;

    [Header("UI Settings")]
    public CanvasGroup victoryPanel; 
    [Tooltip("How long to wait before the panel starts fading in. The camera FOV syncs to this.")]
    public float victoryPanelDelay = 8f; 
    public float victoryFadeDuration = 2f;

    [Header("Scene Transition")]
    [Tooltip("The scene to load when the player clicks the victory panel.")]
    public string returnSceneName = "GameScene";

    [Header("Center Beacon Settings")]
    [Tooltip("Create an Empty GameObject in your scene and attach it here")]
    public Transform centerPulseOrigin;
    public Color centerPulseColor = new Color(1f, 0.8f, 0f, 1f); 
    public int centerPulseCount = 10;
    public float centerPulseDelay = 0.2f;
    public float centerPulseLifetime = 2.0f;
    public float centerPulseStartRadius = 0.5f;
    public float centerPulseMaxRadius = 6.0f;

    [Header("Space Settings")]
    public float zDepth = 10f; 
    public float minSpacing = 3f; 

    [Header("Quad Timing Settings")]
    public float totalLifetime = 3f;
    public float spawnInterval = 1f; 
    public float fadeDuration = 0.5f;

    [Header("Quad Pulse Settings")]
    public Color pulseColor = new Color(0f, 0.8f, 1f, 1f);
    public int maxPulseCount = 3; 
    public float pulseDelay = 0.6f; 
    public float pulseLineWidth = 0.05f;
    public float pulseStartRadius = 0.5f; 
    public float pulseMaxRadius = 3.5f; 
    private int pulseSegments = 50; 

    // Object Pools & Tracking
    private List<GameObject> quadPool = new List<GameObject>();
    private List<GameObject> centerPulsePool = new List<GameObject>();
    private List<GameObject> activeQuadsOnScreen = new List<GameObject>();
    
    private int poolSize = 15; 
    private AnimationCurve smoothCurve;

    // Panel click state
    private bool panelReady     = false;
    private bool transitionFired = false;

    void Start()
    {
        smoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (victoryPanel != null)
            victoryPanel.alpha = 0f;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject quad = Instantiate(quadPrefab);
            quad.SetActive(false); 
            
            Renderer rend = quad.GetComponent<Renderer>();
            if (quadMaterial != null) rend.material = quadMaterial;

            for (int p = 0; p < maxPulseCount; p++)
            {
                GameObject ringObj = new GameObject("QuadPulseRing_" + p);
                ringObj.transform.SetParent(quad.transform);
                ringObj.transform.localPosition = Vector3.zero;
                SetupLineRenderer(ringObj);
            }
            quadPool.Add(quad);

            GameObject centerObj = new GameObject("CenterPulseSystem_" + i);
            centerObj.SetActive(false);
            
            for (int c = 0; c < centerPulseCount; c++)
            {
                GameObject centerRing = new GameObject("CenterRing_" + c);
                centerRing.transform.SetParent(centerObj.transform);
                centerRing.transform.localPosition = Vector3.zero;
                SetupLineRenderer(centerRing);
            }
            centerPulsePool.Add(centerObj);
        }

        StartCoroutine(SpawnSequence());
        StartCoroutine(AnimateCameraFOVRoutine());

        if (victoryPanel != null)
            StartCoroutine(FadeInVictoryPanelRoutine());
    }

    void Update()
    {
        // Once the panel is fully visible, any click loads the return scene.
        if (!panelReady || transitionFired) return;
        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

        transitionFired = true;

        if (GridTransitionManager.Instance != null)
            GridTransitionManager.Instance.LoadScene("MainMenuScene");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(returnSceneName);
    }

    private void SetupLineRenderer(GameObject obj)
    {
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; 
        lr.loop = true;
        lr.positionCount = pulseSegments + 1;
        lr.startWidth = pulseLineWidth;
        lr.endWidth = pulseLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default")); 
    }

    private IEnumerator SpawnSequence()
    {
        int totalQuadsToSpawn = Random.Range(5, 11);

        for (int i = 0; i < totalQuadsToSpawn; i++)
        {
            StartCoroutine(QuadLifecycleLoop());
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator QuadLifecycleLoop()
    {
        GameObject quad = GetAvailableQuad();
        GameObject centerObj = GetAvailableCenterObj();

        quadPool.Remove(quad);
        centerPulsePool.Remove(centerObj);

        while (true)
        {
            quad.transform.position = GetValidSpawnPosition();
            quad.SetActive(true);
            activeQuadsOnScreen.Add(quad); 

            int activePulses = Random.Range(1, maxPulseCount + 1);
            
            yield return StartCoroutine(AnimateQuad(quad, activePulses));

            quad.SetActive(false);
            activeQuadsOnScreen.Remove(quad); 

            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(AnimateCenterPulseObj(centerObj));

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator AnimateQuad(GameObject quad, int activePulses)
    {
        Renderer rend = quad.GetComponent<Renderer>();
        LineRenderer[] pulseRings = quad.GetComponentsInChildren<LineRenderer>();
        
        Material mat = rend.material;
        Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
        
        float timeElapsed = 0f;
        float maxScale = 1.5f; 
        
        while (timeElapsed < totalLifetime)
        {
            timeElapsed += Time.deltaTime;
            
            float alpha = 1f;
            Vector3 currentScale = Vector3.one;

            if (timeElapsed < totalLifetime - fadeDuration)
            {
                if (timeElapsed < fadeDuration) alpha = smoothCurve.Evaluate(timeElapsed / fadeDuration);
                float pulseProgress = timeElapsed / (totalLifetime - fadeDuration);
                currentScale = Vector3.Lerp(Vector3.one, Vector3.one * maxScale, smoothCurve.Evaluate(pulseProgress));
            }
            else
            {
                float fadeOutProgress = (timeElapsed - (totalLifetime - fadeDuration)) / fadeDuration;
                float curvedFade = smoothCurve.Evaluate(fadeOutProgress);
                alpha = Mathf.Lerp(1f, 0f, curvedFade);
                currentScale = Vector3.Lerp(Vector3.one * maxScale, Vector3.one * 0.5f, curvedFade);
            }

            quad.transform.localScale = currentScale;
            color.a = alpha;
            mat.SetColor("_BaseColor", color);

            for (int r = 0; r < pulseRings.Length; r++)
            {
                if (r >= activePulses)
                {
                    pulseRings[r].startColor = Color.clear;
                    pulseRings[r].endColor   = Color.clear;
                    continue;
                }

                float pulseTimeElapsed = timeElapsed - (r * pulseDelay);
                if (pulseTimeElapsed < 0) { pulseRings[r].startColor = Color.clear; pulseRings[r].endColor = Color.clear; continue; }

                float pulseLoopTime    = pulseTimeElapsed % (totalLifetime / activePulses);
                float pulseProgress    = pulseLoopTime / (totalLifetime / activePulses);
                float currentRadius    = Mathf.Lerp(pulseStartRadius, pulseMaxRadius, smoothCurve.Evaluate(pulseProgress));

                Vector3 centerPos = quad.transform.position;
                for (int i = 0; i <= pulseSegments; i++)
                {
                    float angle = (i / (float)pulseSegments) * Mathf.PI * 2f;
                    pulseRings[r].SetPosition(i, centerPos + new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f));
                }

                Color currentPulseColor = pulseColor;
                currentPulseColor.a = alpha;
                pulseRings[r].startColor = currentPulseColor;
                pulseRings[r].endColor   = currentPulseColor;
            }

            yield return null;
        }

        color.a = 0f;
        mat.SetColor("_BaseColor", color);
        foreach (LineRenderer lr in pulseRings)
        {
            lr.startColor = Color.clear;
            lr.endColor   = Color.clear;
        }
    }

    private IEnumerator AnimateCenterPulseObj(GameObject centerObj)
    {
        centerObj.SetActive(true);
        if (centerPulseOrigin != null)
            centerObj.transform.position = centerPulseOrigin.position;

        LineRenderer[] rings = centerObj.GetComponentsInChildren<LineRenderer>();
        float timeElapsed = 0f;
        
        float totalTimeRequired = ((centerPulseCount - 1) * centerPulseDelay) + centerPulseLifetime;

        while (timeElapsed < totalTimeRequired)
        {
            timeElapsed += Time.deltaTime;

            for (int r = 0; r < centerPulseCount; r++)
            {
                float ringTimeElapsed = timeElapsed - (r * centerPulseDelay);
                
                if (ringTimeElapsed < 0 || ringTimeElapsed > centerPulseLifetime)
                {
                    rings[r].startColor = Color.clear;
                    rings[r].endColor   = Color.clear;
                    continue;
                }

                float ringProgress    = ringTimeElapsed / centerPulseLifetime;
                float currentRadius   = Mathf.Lerp(centerPulseStartRadius, centerPulseMaxRadius, ringProgress);
                
                float alpha = 1f;
                if (ringTimeElapsed < centerPulseLifetime - fadeDuration)
                {
                    if (ringTimeElapsed < fadeDuration) alpha = smoothCurve.Evaluate(ringTimeElapsed / fadeDuration);
                }
                else
                {
                    float fadeOutProgress = (ringTimeElapsed - (centerPulseLifetime - fadeDuration)) / fadeDuration;
                    alpha = Mathf.Lerp(1f, 0f, smoothCurve.Evaluate(fadeOutProgress));
                }

                Vector3 centerPos = centerObj.transform.position;
                for (int i = 0; i <= pulseSegments; i++)
                {
                    float angle = (i / (float)pulseSegments) * Mathf.PI * 2f;
                    rings[r].SetPosition(i, centerPos + new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f));
                }

                Color c = centerPulseColor;
                c.a = alpha;
                rings[r].startColor = c;
                rings[r].endColor   = c;
            }

            yield return null;
        }

        foreach (LineRenderer lr in rings)
        {
            lr.startColor = Color.clear;
            lr.endColor   = Color.clear;
        }
        centerObj.SetActive(false);
    }

    private IEnumerator AnimateCameraFOVRoutine()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Vector3    originalPos = cam.transform.position;
        Quaternion originalRot = cam.transform.rotation;

        cam.fieldOfView = startFOV;
        float elapsedTime = 0f;

        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(0f, 100f);

        while (elapsedTime < victoryPanelDelay)
        {
            float t = elapsedTime / victoryPanelDelay;
            cam.fieldOfView = Mathf.Lerp(startFOV, endFOV, smoothCurve.Evaluate(t));

            float wobbleIntensity = Mathf.InverseLerp(wobbleStartThreshold, 1.0f, t);
            
            if (wobbleIntensity > 0f)
            {
                float smoothIntensity = Mathf.SmoothStep(0f, 1f, wobbleIntensity);
                float noiseX = (Mathf.PerlinNoise(Time.time * wobbleSpeed + seedX, 0f) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise(0f, Time.time * wobbleSpeed + seedY) - 0.5f) * 2f;

                cam.transform.position = originalPos + new Vector3(noiseX * wobblePositionAmount, noiseY * wobblePositionAmount, 0f) * smoothIntensity;
                cam.transform.rotation = originalRot * Quaternion.Euler(noiseY * wobbleRotationAmount * smoothIntensity, noiseX * wobbleRotationAmount * smoothIntensity, 0f);
            }
            else
            {
                cam.transform.position = originalPos;
                cam.transform.rotation = originalRot;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cam.fieldOfView = endFOV;

        while (true)
        {
            float noiseX = (Mathf.PerlinNoise(Time.time * wobbleSpeed + seedX, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * wobbleSpeed + seedY) - 0.5f) * 2f;

            cam.transform.position = originalPos + new Vector3(noiseX * wobblePositionAmount, noiseY * wobblePositionAmount, 0f);
            cam.transform.rotation = originalRot * Quaternion.Euler(noiseY * wobbleRotationAmount, noiseX * wobbleRotationAmount, 0f);

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

    private GameObject GetAvailableQuad()      { return quadPool.Count      > 0 ? quadPool[0]      : null; }
    private GameObject GetAvailableCenterObj() { return centerPulsePool.Count > 0 ? centerPulsePool[0] : null; }

    private Vector3 GetValidSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 validPos     = Vector3.zero;
        bool    foundValidPos = false;
        int     maxAttempts  = 30;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 worldPos   = cam.ViewportToWorldPoint(new Vector3(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), zDepth));
            bool    isTooClose = false;
            
            foreach (GameObject activeQuad in activeQuadsOnScreen)
            {
                if (activeQuad.activeInHierarchy && Vector3.Distance(worldPos, activeQuad.transform.position) < minSpacing)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (!isTooClose) { validPos = worldPos; foundValidPos = true; break; }
        }

        if (!foundValidPos)
            validPos = cam.ViewportToWorldPoint(new Vector3(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), zDepth));

        return validPos;
    }
}
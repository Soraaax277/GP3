using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpaceCutscene : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject cam1;
    public GameObject cam2;
    public GameObject cam3;

    [Header("Cut 1 Assets")]
    public Transform rocket1;
    public float rocket1Speed = 15f; 
    public float cam1TrailDistance = 5f; 

    [Header("Cut 2 Assets")]
    public Transform rocket2;
    public float cut2Duration = 4f; 

    [Header("Cut 3 Assets")]
    public Transform rocket3;
    public Transform planetSphere;
    public CanvasGroup victoryPanel; 
    public float circleSpeed = 45f; 
    public float planetBobSpeed = 2f;
    public float planetBobHeight = 0.5f;
    public float planetRotationSpeed = 10f; 
    public float planetRotationXSpeed = 5f; 

    [Header("Auto-Trail Settings")]
    public Material customTrailMaterial; 
    public float trailFadeTime = 1.0f;
    public float trailStartWidth = 0.5f; 
    public float rocket3TrailStartWidth = 0.05f; 

    [Header("Skybox Settings")]
    public Material skyboxA; // Default Atmosphere
    public Material skyboxB; // Deep Space

    [Header("Scene Transition")]
    [Tooltip("The scene to load when the player clicks the victory panel.")]
    public string returnSceneName = "GameScene";

    private GameObject cut3Pivot;

    // Panel click state
    private bool panelReady      = false;
    private bool transitionFired = false;

    void Start()
    {
        SetupTrail(rocket1, trailStartWidth);
        SetupTrail(rocket2, trailStartWidth);
        SetupTrail(rocket3, rocket3TrailStartWidth); 

        if (victoryPanel != null) victoryPanel.alpha = 0f;
        DeactivateAllCutsceneAssets();
        StartCoroutine(PlayCutsceneSequence());
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

    void SetupTrail(Transform rocket, float width)
    {
        if (rocket == null) return;

        TrailRenderer trail = rocket.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = rocket.gameObject.AddComponent<TrailRenderer>();

        trail.time = trailFadeTime;
        trail.minVertexDistance = 0.1f;

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, width); 
        curve.AddKey(1.0f, 0.0f);            
        trail.widthCurve = curve;

        if (customTrailMaterial != null)
            trail.material = customTrailMaterial;
        else
            trail.material = new Material(Shader.Find("Sprites/Default"));
    }

    void ClearTrail(Transform rocket)
    {
        TrailRenderer trail = rocket.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.Clear();
    }

    void DeactivateAllCutsceneAssets()
    {
        cam1.SetActive(false);
        cam2.SetActive(false);
        cam3.SetActive(false);
        
        rocket1.gameObject.SetActive(false);
        rocket2.gameObject.SetActive(false);
        rocket3.gameObject.SetActive(false);
        planetSphere.gameObject.SetActive(false);

        if (cut3Pivot != null) Destroy(cut3Pivot);
    }

    IEnumerator PlayCutsceneSequence()
    {
        yield return StartCoroutine(Cut1());
        yield return StartCoroutine(Cut2());
        yield return StartCoroutine(Cut3());
    }

    IEnumerator Cut1()
    {
        if (skyboxA != null) RenderSettings.skybox = skyboxA;

        DeactivateAllCutsceneAssets();
        cam1.SetActive(true);
        rocket1.gameObject.SetActive(true);

        rocket1.position = new Vector3(rocket1.position.x, 0f, rocket1.position.z);
        ClearTrail(rocket1); 

        Vector3    cam1StartPos = cam1.transform.position;
        Quaternion cam1StartRot = cam1.transform.rotation;

        float prepTime = 0f;
        while (prepTime < 2f)
        {
            float shakeX = (Mathf.PerlinNoise(Time.time * 15f, 0f) - 0.5f) * 0.1f; 
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * 15f) - 0.5f) * 0.1f;
            cam1.transform.position = cam1StartPos + new Vector3(shakeX, shakeY, 0f);
            prepTime += Time.deltaTime;
            yield return null;
        }

        cam1.transform.position = cam1StartPos;

        float yVelocity      = 0f;
        float baseCatchUpTime = 0.5f; 

        while (rocket1.position.y < 75f)
        {
            rocket1.Translate(Vector3.up * rocket1Speed * Time.deltaTime, Space.World);

            float flightProgress    = rocket1.position.y / 75f;
            float dynamicCatchUpTime = Mathf.Lerp(baseCatchUpTime, 4f, flightProgress);

            float targetCamY  = rocket1.position.y - cam1TrailDistance;
            float currentCamY = cam1.transform.position.y;
            float newCamY     = Mathf.SmoothDamp(currentCamY, targetCamY, ref yVelocity, dynamicCatchUpTime);

            cam1.transform.position = new Vector3(cam1StartPos.x, newCamY, cam1StartPos.z);

            float pitchJitter = Mathf.Sin(Time.time * 3f) * 1f;
            cam1.transform.rotation = cam1StartRot * Quaternion.Euler(pitchJitter, 0f, 0f);

            yield return null;
        }

        cam1.transform.rotation = cam1StartRot;
    }

    IEnumerator Cut2()
    {
        DeactivateAllCutsceneAssets();
        cam2.SetActive(true);
        rocket2.gameObject.SetActive(true);

        cam2.transform.position = new Vector3(cam2.transform.position.x, 50f, cam2.transform.position.z);
        rocket2.position = Vector3.zero;
        rocket2.rotation = Quaternion.identity;
        ClearTrail(rocket2); 

        float   elapsedTime  = 0f;
        Vector3 cam2StartPos = cam2.transform.position;
        Vector3 cam2EndPos   = new Vector3(cam2.transform.position.x, 5f, cam2.transform.position.z);

        Vector3    rocket2TargetPos = new Vector3(0f, 8f, -2f);
        Quaternion rocket2TargetRot = Quaternion.Euler(-5f, 0f, 0f);

        while (elapsedTime < cut2Duration)
        {
            float t       = elapsedTime / cut2Duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t); 
            
            cam2.transform.position = Vector3.Lerp(cam2StartPos, cam2EndPos, smoothT);
            rocket2.position = Vector3.Lerp(Vector3.zero, rocket2TargetPos, t);
            rocket2.rotation = Quaternion.Lerp(Quaternion.identity, rocket2TargetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator Cut3()
    {
        if (skyboxB != null) RenderSettings.skybox = skyboxB;

        DeactivateAllCutsceneAssets();
        cam3.SetActive(true);
        rocket3.gameObject.SetActive(true);
        planetSphere.gameObject.SetActive(true);

        StartCoroutine(PlanetAnimRoutine());

        rocket3.position   = new Vector3(-1.75f, 0f, -5.5f);
        rocket3.localScale = Vector3.one * 0.05f;
        ClearTrail(rocket3); 

        cut3Pivot = new GameObject("Cut3_OrbitPivot");
        cut3Pivot.transform.position = planetSphere.position;
        rocket3.SetParent(cut3Pivot.transform, true);

        StartCoroutine(OrbitRocketRoutine());

        float   approachDuration = 2f; 
        float   elapsedTime      = 0f;
        Vector3 startLocalPos    = rocket3.localPosition;
        Vector3 endLocalPos      = new Vector3(-1.75f, 0f, -7f) - cut3Pivot.transform.position;

        while (elapsedTime < approachDuration)
        {
            float t       = elapsedTime / approachDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            rocket3.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, smoothT);
            rocket3.localScale    = Vector3.Lerp(Vector3.one * 0.05f, Vector3.one * 0.1f, smoothT);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(1f); 
        
        elapsedTime = 0f;
        float fadeDuration = 2f;

        while (elapsedTime < fadeDuration)
        {
            victoryPanel.alpha = elapsedTime / fadeDuration;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        victoryPanel.alpha = 1f;
        panelReady = true; // Click is now live
    }

    IEnumerator OrbitRocketRoutine()
    {
        while (rocket3.gameObject.activeSelf && cut3Pivot != null)
        {
            cut3Pivot.transform.Rotate(Vector3.up, circleSpeed * Time.deltaTime);

            Vector3 centerToRocket  = rocket3.position - planetSphere.position;
            centerToRocket.y        = 0f; 
            Vector3 tangentDirection = Vector3.Cross(Vector3.up, centerToRocket).normalized;

            rocket3.rotation = Quaternion.LookRotation(tangentDirection) * Quaternion.Euler(90f, 0f, 0f);

            yield return null;
        }
    }

    IEnumerator PlanetAnimRoutine()
    {
        Vector3 startPos = planetSphere.position;
        while (planetSphere.gameObject.activeSelf)
        {
            float newY = startPos.y + (Mathf.Sin(Time.time * planetBobSpeed) * planetBobHeight);
            planetSphere.position = new Vector3(startPos.x, newY, startPos.z);
            
            planetSphere.Rotate(new Vector3(planetRotationXSpeed, planetRotationSpeed, 0f) * Time.deltaTime, Space.World);
            
            yield return null;
        }
    }
}
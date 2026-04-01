using UnityEngine;
using System.Collections;

public class FeedbackController : MonoBehaviour
{
    public static FeedbackController Instance;

    [Header("Colors")]
    public Color technicianColor = new Color(0f, 1f, 0.8f); // Cyan/Teal
    public Color wireSpecialistColor = new Color(0f, 0.5f, 1f); // Blue
    public Color alertColor = new Color(1f, 0.1f, 0.1f); // Red
    public Color levelUpColor = new Color(1f, 0.9f, 0.2f); // Gold/Yellow

    private void Awake()
    {
        Instance = this;
    }

    // --- ACTION JUICE ---

    public void PlayTechnicianAction(Vector3 position)
    {
        // "Holographic beam" effect using a temporary cylinder
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.transform.position = position + Vector3.up * 5f;
        beam.transform.localScale = new Vector3(0.1f, 5f, 0.1f);
        
        Renderer r = beam.GetComponent<Renderer>();
        
        // Fix for URP Build: Replace legend 'Unlit/Transparent' with URP Unlit
        Shader beamShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (beamShader == null) beamShader = Shader.Find("Unlit/Transparent");
        
        Material mat = new Material(beamShader);
        Color color = new Color(technicianColor.r, technicianColor.g, technicianColor.b, 0.6f);
        
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;

        // Force transparency properties for URP
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        r.material = mat;
        
        if (beam.TryGetComponent<Collider>(out Collider col)) Destroy(col);
        
        StartCoroutine(FadeAndDestroy(beam, 0.8f));
    }

    public void PlayWirePlacement(Vector3 position)
    {
        // "Blue sparks" effect using small spheres
        for (int i = 0; i < 5; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.transform.position = position + Random.insideUnitSphere * 0.5f;
            spark.transform.localScale = Vector3.one * 0.15f;
            
            Renderer r = spark.GetComponent<Renderer>();
            
            Shader sparkShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (sparkShader == null) sparkShader = Shader.Find("Unlit/Color");
            
            Material mat = new Material(sparkShader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", wireSpecialistColor);
            else mat.color = wireSpecialistColor;
            
            r.material = mat;
            
            if (spark.TryGetComponent<Collider>(out Collider col)) Destroy(col);
            
            Vector3 jumpDir = (Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
            StartCoroutine(SparkRoutine(spark, jumpDir));
        }
    }

    public void PlayTowerDestroyed(Vector3 position)
    {
        // Red visual flair flash
        // CameraShake(0.4f, 0.3f); // Disabled as requested
        
        // Large red flash sphere
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.transform.position = position;
        flash.transform.localScale = Vector3.one * 2f;
        
        Renderer r = flash.GetComponent<Renderer>();
        
        Shader flashShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (flashShader == null) flashShader = Shader.Find("Unlit/Transparent");
        
        Material mat = new Material(flashShader);
        Color flashColor = new Color(1f, 0f, 0f, 0.5f);
        
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", flashColor);
        else mat.color = flashColor;

        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        r.material = mat;
        
        if (flash.TryGetComponent<Collider>(out Collider col)) Destroy(col);
        StartCoroutine(FadeAndDestroy(flash, 0.5f));
    }

    public void PlayLevelUpEffect(Vector3 position)
    {
        // "Golden Ring" burst using multiple spheres
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 ringPos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.transform.position = position + Vector3.up * 1f;
            star.transform.localScale = Vector3.one * 0.25f;
            
            Renderer r = star.GetComponent<Renderer>();
            
            Shader starShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (starShader == null) starShader = Shader.Find("Unlit/Color");
            
            Material mat = new Material(starShader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", levelUpColor);
            else mat.color = levelUpColor;
            
            r.material = mat;
            
            if (star.TryGetComponent<Collider>(out Collider col)) Destroy(col);
            StartCoroutine(SparkRoutine(star, (ringPos + Vector3.up * 0.5f).normalized));
        }
        
        // Vertical beam
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.transform.position = position + Vector3.up * 3f;
        beam.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
        Renderer beamR = beam.GetComponent<Renderer>();
        
        Shader levelShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (levelShader == null) levelShader = Shader.Find("Unlit/Transparent");
        
        Material matB = new Material(levelShader);
        Color beamCol = new Color(levelUpColor.r, levelUpColor.g, levelUpColor.b, 0.4f);
        
        if (matB.HasProperty("_BaseColor")) matB.SetColor("_BaseColor", beamCol);
        else matB.color = beamCol;

        matB.SetFloat("_Surface", 1);
        matB.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matB.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matB.SetInt("_ZWrite", 0);
        matB.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        matB.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        beamR.material = matB;
        if (beam.TryGetComponent<Collider>(out Collider colB)) Destroy(colB);
        StartCoroutine(FadeAndDestroy(beam, 1.2f));
    }

    // --- ERA TRANSITION ---

    public void PlayEraTransition(string eraName)
    {
        Debug.Log($"[VFX] Visual flair for transition to {eraName}!");
        // Glitch effect removed as requested
    }

    // --- HELPERS ---

    public void CameraShake(float duration, float intensity)
    {
        // Screen shake disabled as requested
        /*
        if (CameraController.Instance != null)
        {
            StartCoroutine(ShakeRoutine(duration, intensity));
        }
        */
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        Vector3 originalPos = CameraController.Instance.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            
            CameraController.Instance.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        CameraController.Instance.transform.localPosition = originalPos;
    }

    private IEnumerator FadeAndDestroy(GameObject obj, float duration)
    {
        float elapsed = 0f;
        Renderer r = obj.GetComponent<Renderer>();
        Color startColor = r.material.color;
        Vector3 startScale = obj.transform.localScale;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            if (r != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                r.material.color = c;
            }
            obj.transform.localScale = Vector3.Lerp(startScale, startScale * 1.5f, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(obj);
    }

    private IEnumerator SparkRoutine(GameObject spark, Vector3 direction)
    {
        float elapsed = 0f;
        float duration = 0.5f;
        Vector3 startPos = spark.transform.position;

        while (elapsed < duration)
        {
            spark.transform.position = startPos + direction * (elapsed * 4f) + Vector3.down * (elapsed * elapsed * 10f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(spark);
    }
}

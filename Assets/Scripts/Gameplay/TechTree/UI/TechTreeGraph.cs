using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TechTreeGraph : MonoBehaviour
{
    [Header("Settings")]
    public GameObject linePrefab;
    public RectTransform lineContainer; 
    
    [SerializeField] private List<GameObject> spawnedLines = new List<GameObject>();

    [ContextMenu("Regenerate Connections")] 
    public void GenerateConnections()
    {
        // Clear old lines
        for (int i = spawnedLines.Count - 1; i >= 0; i--)
        {
            if (spawnedLines[i] != null) DestroyImmediate(spawnedLines[i]);
        }
        spawnedLines.Clear();

        // Find Buttons
        TechButton[] allButtons = GetComponentsInChildren<TechButton>();
        Dictionary<TechNode, RectTransform> nodeToRect = new Dictionary<TechNode, RectTransform>();
        
        foreach (var btn in allButtons)
        {
            if (btn.tech != null) nodeToRect[btn.tech] = btn.GetComponent<RectTransform>();
        }

        // Connect
        foreach (var childBtn in allButtons)
        {
            if (childBtn.tech == null) continue;

            foreach (var parentNode in childBtn.tech.preReqs)
            {
                if (nodeToRect.ContainsKey(parentNode))
                {
                    CreateLine(nodeToRect[parentNode], childBtn.GetComponent<RectTransform>(), parentNode, childBtn.tech);
                }
            }
        }
        
        // Force Save the Scene
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        #endif
    }

    private void CreateLine(RectTransform startNode, RectTransform endNode, TechNode source, TechNode target)
    {
        if (linePrefab == null || lineContainer == null) return;

        // Create the line linked to the prefab
        GameObject newLine;
        #if UNITY_EDITOR
        if (!Application.isPlaying) newLine = (GameObject)PrefabUtility.InstantiatePrefab(linePrefab, lineContainer);
        else newLine = Instantiate(linePrefab, lineContainer);
        #else
        newLine = Instantiate(linePrefab, lineContainer);
        #endif

        RectTransform lineRect = newLine.GetComponent<RectTransform>();

        // Coordinate Conversion
        Vector3 startPosLocal = lineContainer.InverseTransformPoint(startNode.position);
        Vector3 endPosLocal = lineContainer.InverseTransformPoint(endNode.position);

        lineRect.localPosition = startPosLocal;

        Vector3 dir = endPosLocal - startPosLocal;
        float dist = dir.magnitude;
        
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
        lineRect.sizeDelta = new Vector2(dist, linePrefab.GetComponent<RectTransform>().sizeDelta.y);

        //  ASSIGN AND MARK DIRTY 
        TechLine lineScript = newLine.GetComponent<TechLine>();
        if (lineScript != null)
        {
            lineScript.sourceNode = source;
            lineScript.targetNode = target;
            lineScript.targetWidth = dist;
            
            // TELL UNITY THIS COMPONENT CHANGED SO IT SAVES
            #if UNITY_EDITOR
            EditorUtility.SetDirty(lineScript);
            EditorUtility.SetDirty(newLine);
            #endif
        }

        spawnedLines.Add(newLine);
    }
}
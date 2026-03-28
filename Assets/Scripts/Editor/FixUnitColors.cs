using UnityEditor;
using UnityEngine;
using System.IO;

public class FixUnitColors : EditorWindow
{
    [MenuItem("Tools/Automate Unit Colors")]
    public static void ShowWindow()
    {
        GetWindow<FixUnitColors>("Unit Color Automation");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Fix Unit Colors / Meshes"))
        {
            FixUnits();
        }
    }

    private void FixUnits()
    {
        string fbxPath = "Assets/Art/Units/Vocations and Uniforms.fbx";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError("FBX not found at " + fbxPath);
            return;
        }

        // Just dump the FBX structure to Unity Console so the user can tell us what's in it
        Debug.Log("FBX Root: " + fbxAsset.name);
        foreach (Transform child in fbxAsset.transform)
        {
            Debug.Log(" - Child: " + child.name);
        }

        Debug.Log("Please check the Console to see the structure, or wait for the AI to parse it if we output to a file!");
        
        // Output to a file for AI to read
        string logPath = Application.dataPath + "/../fbx_structure.txt";
        using (StreamWriter writer = new StreamWriter(logPath))
        {
            writer.WriteLine("FBX Structure:");
            foreach (Transform t in fbxAsset.GetComponentsInChildren<Transform>(true))
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
                string meshInfo = "";
                if (mf && mf.sharedMesh) meshInfo += " Mesh=" + mf.sharedMesh.name;
                if (smr && smr.sharedMesh) meshInfo += " SkinnedMesh=" + smr.sharedMesh.name;

                Renderer r = t.GetComponent<Renderer>();
                if (r && r.sharedMaterial) meshInfo += " Mat=" + r.sharedMaterial.name;

                writer.WriteLine(t.name + meshInfo);
            }
        }
    }
}

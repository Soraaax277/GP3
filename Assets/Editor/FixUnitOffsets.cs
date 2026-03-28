using UnityEngine;
using UnityEditor;

public class FixUnitOffsets : EditorWindow
{
    [MenuItem("Tools/FINAL Fix Local Positions & Animations")]
    public static void FixEverything()
    {
        // 1. Force the two animations to guarantee they are Humanoid
        string[] animPaths = new string[] { "Assets/Prefabs/Idle (1).fbx", "Assets/Prefabs/Walking (1).fbx" };
        foreach (string path in animPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                // Auto create the avatar for the dummy animations if not doing copy
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
        }

        // 2. Loop through all your Unit Prefabs and zero out the local offsets!
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents.GetComponent<Unit>() != null)
            {
                // Find all child meshes (the FBX models you dragged in)
                Animator childAnim = contents.GetComponentInChildren<Animator>();
                if (childAnim != null && childAnim.gameObject != contents)
                {
                    // Zero out the completely crooked local offsets
                    childAnim.transform.localPosition = Vector3.zero;
                    childAnim.transform.localRotation = Quaternion.identity;
                }
                
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ALL PREFAB OFFSETS AND ANIMATIONS FIXED!");
    }
}

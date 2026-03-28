using UnityEngine;
using UnityEditor;

public class FixRiggingAndAnimators : EditorWindow
{
    [MenuItem("Tools/1-Click Fix Characters")]
    public static void FixEverything()
    {
        // 1. Force your specific models to import as Humanoid Rigs
        string[] modelGuids = AssetDatabase.FindAssets("model_ t:Model", new[] { "Assets/Art/Units" });
        string[] modelPaths = new string[modelGuids.Length];
        for (int i = 0; i < modelGuids.Length; i++)
        {
            modelPaths[i] = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
        }
        
        Avatar mainAvatar = null;
        
        foreach (string modelPath in modelPaths)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.SaveAndReimport();

                // Just grab the Avatar from the first one to share with the animations
                if (mainAvatar == null)
                {
                    Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                    foreach (Object obj in allAssets)
                    {
                        if (obj is Avatar)
                        {
                            mainAvatar = obj as Avatar;
                            break;
                        }
                    }
                }
            }
        }

        if (mainAvatar == null)
        {
            Debug.LogError("Could not find generated Humanoid Avatar! Check if model paths are correct.");
            return;
        }

        // 2. Force your two new Animation files to also be Humanoid, copying the Avatar
        string[] animPaths = new string[] { "Assets/Prefabs/Idle (1).fbx", "Assets/Prefabs/Walking (1).fbx" };
        foreach (string path in animPaths)
        {
            ModelImporter animImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (animImporter != null)
            {
                animImporter.animationType = ModelImporterAnimationType.Human;
                animImporter.sourceAvatar = mainAvatar;
                animImporter.SaveAndReimport();
            }
        }

        // 3. Find the UnitsAnimation.controller you set up
        string[] ctrlGuids = AssetDatabase.FindAssets("UnitsAnimation t:AnimatorController");
        UnityEditor.Animations.AnimatorController ctrl = null;
        if (ctrlGuids.Length > 0)
        {
            ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(AssetDatabase.GUIDToAssetPath(ctrlGuids[0]));
        }

        // 4. Clean up every unit prefab perfectly
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents.GetComponent<Unit>() != null)
            {
                // Delete the accidental Animator from the root capsule
                Animator rootAnim = contents.GetComponent<Animator>();
                if (rootAnim != null)
                {
                    DestroyImmediate(rootAnim);
                }

                // Place the Animator Controller exactly on the 3D model
                Animator childAnim = contents.GetComponentInChildren<Animator>();
                if (childAnim != null)
                {
                    // Unity auto-assigns the Avatar when applying a Humanoid model to a prefab, 
                    // but we link the controller manually here:
                    if (ctrl != null) childAnim.runtimeAnimatorController = ctrl;
                }
                
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ALL CHARACTERS AND ANIMATIONS FIXED! Hit Play!");
    }
}

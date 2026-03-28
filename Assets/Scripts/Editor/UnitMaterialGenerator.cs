using System.IO;
using UnityEditor;
using UnityEngine;

public class UnitMaterialGenerator : EditorWindow
{
    private string unitName = "Builder";
    private Color hatColor = new Color(1f, 0.8f, 0.1f);      // Yellow Hardhat
    private Color shirtColor = new Color(1f, 0.5f, 0f);      // Orange Vest
    private Color pantsColor = new Color(0.2f, 0.3f, 0.5f);  // Blue Jeans
    private Color skinColor = new Color(1f, 0.8f, 0.6f);     // Light Skin
    private Color shoeColor = new Color(0.3f, 0.2f, 0.1f);   // Brown Boots

    [MenuItem("Tools/Unit Material Generator")]
    public static void ShowWindow()
    {
        GetWindow<UnitMaterialGenerator>("Unit Painter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Procedural Unit Texture Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use this to generate clean, solid-color palette materials for your units, matching whatever outfit they need! The colors map to the vertical strips on the Ochi-style meshes.", MessageType.Info);

        unitName = EditorGUILayout.TextField("Unit Name (e.g. Builder)", unitName);

        EditorGUILayout.Space();
        hatColor = EditorGUILayout.ColorField("Hat / Hair Color", hatColor);
        shirtColor = EditorGUILayout.ColorField("Shirt / Torso Color", shirtColor);
        pantsColor = EditorGUILayout.ColorField("Pants / Legs Color", pantsColor);
        skinColor = EditorGUILayout.ColorField("Skin Color", skinColor);
        shoeColor = EditorGUILayout.ColorField("Shoes / Belts Color", shoeColor);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Texture & Material"))
        {
            GenerateMaterial();
        }
    }

    private void GenerateMaterial()
    {
        string folderPath = "Assets/Art/Units/GeneratedMaterials";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/Art/Units", "GeneratedMaterials");
        }

        // 1. Create the Texture2D
        int texWidth = 256;
        int texHeight = 256;
        Texture2D texture = new Texture2D(texWidth, texHeight, TextureFormat.RGB24, false);
        
        // We divide the width into 5 vertical stripes
        int sections = 5;
        int sectionWidth = texWidth / sections;

        Color[] colors = { hatColor, shirtColor, pantsColor, skinColor, shoeColor };

        for (int x = 0; x < texWidth; x++)
        {
            int currentColorIndex = Mathf.Clamp(x / sectionWidth, 0, colors.Length - 1);
            Color col = colors[currentColorIndex];

            for (int y = 0; y < texHeight; y++)
            {
                texture.SetPixel(x, y, col);
            }
        }
        texture.Apply();

        // 2. Save Texture to PNG
        byte[] pngData = texture.EncodeToPNG();
        string texPath = $"{folderPath}/{unitName}_Palette.png";
        File.WriteAllBytes(texPath, pngData);
        AssetDatabase.ImportAsset(texPath);

        // Configure Texture Import Settings (No filter, no compression)
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.filterMode = FilterMode.Point; // Crucial for solid color palettes
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        // 3. Create the Material
        Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Material newMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        newMat.SetTexture("_BaseMap", importedTex);
        newMat.mainTexture = importedTex;
        newMat.SetInt("_Cull", 0); // Double-Sided for ripped mesh support

        string matPath = $"{folderPath}/{unitName}_Mat.mat";
        AssetDatabase.CreateAsset(newMat, matPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Successfully generated Material and Texture for {unitName} at {folderPath}!");
        Selection.activeObject = newMat; // Highlight it in the editor
    }
}

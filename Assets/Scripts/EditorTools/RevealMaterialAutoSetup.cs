using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class RevealMaterialAutoSetup : MonoBehaviour
{
    [Header("Reveal Shader")]
    public Shader revealShader;

    [Header("Generated Material Output")]
    public string outputFolder = "Assets/GeneratedRevealMaterials";

    [Header("Scan Settings")]
    public bool includeInactive = true;
    public bool replaceExistingRevealMaterials = false;

    [Header("Target Reveal Shader Properties")]
    public string targetBaseMapProperty = "_BaseMap";
    public string targetBaseColorProperty = "_BaseColor";
    public string targetNormalMapProperty = "_BumpMap";
    public string targetMetallicProperty = "_Metallic";
    public string targetSmoothnessProperty = "_Smoothness";
    public string targetEmissionMapProperty = "_EmissionMap";
    public string targetEmissionColorProperty = "_EmissionColor";
    public string revealHeightProperty = "_RevealHeight";

    [Header("Default Reveal Value On Material")]
    public float defaultRevealHeight = -10f;

    [ContextMenu("Generate And Assign Reveal Materials")]
    public void GenerateAndAssignRevealMaterials()
    {
#if UNITY_EDITOR
        if (revealShader == null)
        {
            Debug.LogError("Reveal shader is null.", this);
            return;
        }

        EnsureFolder(outputFolder);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive);

        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found.", this);
            return;
        }

        Dictionary<Material, Material> convertedMaterialMap = new Dictionary<Material, Material>();

        int rendererCount = 0;
        int materialSlotCount = 0;
        int generatedCount = 0;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] originalMaterials = renderer.sharedMaterials;

            if (originalMaterials == null || originalMaterials.Length == 0)
                continue;

            bool changed = false;
            Material[] newMaterials = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material sourceMaterial = originalMaterials[i];

                if (sourceMaterial == null)
                {
                    newMaterials[i] = null;
                    continue;
                }

                materialSlotCount++;

                if (sourceMaterial.shader == revealShader && !replaceExistingRevealMaterials)
                {
                    newMaterials[i] = sourceMaterial;
                    continue;
                }

                if (!convertedMaterialMap.TryGetValue(sourceMaterial, out Material revealMaterial))
                {
                    revealMaterial = GetOrCreateRevealMaterial(sourceMaterial, out bool wasGenerated);

                    if (wasGenerated)
                        generatedCount++;

                    convertedMaterialMap.Add(sourceMaterial, revealMaterial);
                }

                newMaterials[i] = revealMaterial;
                changed = true;
            }

            if (changed)
            {
                Undo.RecordObject(renderer, "Assign Reveal Materials");

                renderer.sharedMaterials = newMaterials;

                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

                rendererCount++;
            }
        }

        EditorUtility.SetDirty(gameObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Reveal material setup complete on {gameObject.name}.\n" +
            $"Renderers changed: {rendererCount}\n" +
            $"Material slots scanned: {materialSlotCount}\n" +
            $"Reveal materials generated: {generatedCount}",
            this
        );
#else
        Debug.LogWarning("GenerateAndAssignRevealMaterials only works in Unity Editor.", this);
#endif
    }

#if UNITY_EDITOR
    private Material GetOrCreateRevealMaterial(Material sourceMaterial, out bool wasGenerated)
    {
        wasGenerated = false;

        string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
        string sourceName = SanitizeFileName(sourceMaterial.name);

        string revealMaterialPath = $"{outputFolder}/{sourceName}_Reveal.mat";

        Material revealMaterial = AssetDatabase.LoadAssetAtPath<Material>(revealMaterialPath);

        if (revealMaterial == null)
        {
            revealMaterial = new Material(revealShader);
            AssetDatabase.CreateAsset(revealMaterial, revealMaterialPath);
            wasGenerated = true;
        }
        else
        {
            revealMaterial.shader = revealShader;
        }

        CopyCommonProperties(sourceMaterial, revealMaterial);

        EditorUtility.SetDirty(revealMaterial);

        Debug.Log(
            $"Converted material:\n" +
            $"Source: {sourceMaterial.name}\n" +
            $"Source path: {sourcePath}\n" +
            $"Reveal: {revealMaterialPath}",
            this
        );

        return revealMaterial;
    }

    private void CopyCommonProperties(Material source, Material target)
    {
        CopyBaseMap(source, target);
        CopyBaseColor(source, target);
        CopyNormalMap(source, target);
        CopyMetallic(source, target);
        CopySmoothness(source, target);
        CopyEmission(source, target);
        SetRevealHeight(target);
    }

    private void CopyBaseMap(Material source, Material target)
    {
        Texture texture = null;

        if (source.HasProperty("_BaseMap"))
            texture = source.GetTexture("_BaseMap");
        else if (source.HasProperty("_MainTex"))
            texture = source.GetTexture("_MainTex");
        else if (source.HasProperty("_Albedo"))
            texture = source.GetTexture("_Albedo");

        if (target.HasProperty(targetBaseMapProperty))
            target.SetTexture(targetBaseMapProperty, texture);
    }

    private void CopyBaseColor(Material source, Material target)
    {
        Color color = Color.white;

        if (source.HasProperty("_BaseColor"))
            color = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color"))
            color = source.GetColor("_Color");

        if (target.HasProperty(targetBaseColorProperty))
            target.SetColor(targetBaseColorProperty, color);
    }

    private void CopyNormalMap(Material source, Material target)
    {
        Texture normal = null;

        if (source.HasProperty("_BumpMap"))
            normal = source.GetTexture("_BumpMap");
        else if (source.HasProperty("_NormalMap"))
            normal = source.GetTexture("_NormalMap");

        if (normal == null)
            return;

        if (target.HasProperty(targetNormalMapProperty))
        {
            target.SetTexture(targetNormalMapProperty, normal);
            target.EnableKeyword("_NORMALMAP");
        }
    }

    private void CopyMetallic(Material source, Material target)
    {
        float metallic = 0f;

        if (source.HasProperty("_Metallic"))
            metallic = source.GetFloat("_Metallic");

        if (target.HasProperty(targetMetallicProperty))
            target.SetFloat(targetMetallicProperty, metallic);
    }

    private void CopySmoothness(Material source, Material target)
    {
        float smoothness = 0.5f;

        if (source.HasProperty("_Smoothness"))
            smoothness = source.GetFloat("_Smoothness");
        else if (source.HasProperty("_Glossiness"))
            smoothness = source.GetFloat("_Glossiness");

        if (target.HasProperty(targetSmoothnessProperty))
            target.SetFloat(targetSmoothnessProperty, smoothness);
    }

    private void CopyEmission(Material source, Material target)
    {
        Texture emissionMap = null;
        Color emissionColor = Color.black;

        if (source.HasProperty("_EmissionMap"))
            emissionMap = source.GetTexture("_EmissionMap");

        if (source.HasProperty("_EmissionColor"))
            emissionColor = source.GetColor("_EmissionColor");

        if (target.HasProperty(targetEmissionMapProperty))
            target.SetTexture(targetEmissionMapProperty, emissionMap);

        if (target.HasProperty(targetEmissionColorProperty))
            target.SetColor(targetEmissionColorProperty, emissionColor);

        if (emissionMap != null || emissionColor.maxColorComponent > 0f)
            target.EnableKeyword("_EMISSION");
    }

    private void SetRevealHeight(Material target)
    {
        if (target.HasProperty(revealHeightProperty))
            target.SetFloat(revealHeightProperty, defaultRevealHeight);
    }

    private void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');

        if (parts.Length == 0 || parts[0] != "Assets")
        {
            Debug.LogError("Output folder must start with Assets/");
            return;
        }

        string currentPath = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }
#endif
}
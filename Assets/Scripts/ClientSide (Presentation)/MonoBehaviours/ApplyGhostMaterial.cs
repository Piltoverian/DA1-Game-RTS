using UnityEngine;

public class ApplyGhostMaterial : MonoBehaviour
{
    public Material ghostMaterial;

    [ContextMenu("Apply Ghost Material To Children")]
    public void Apply()
    {
        if (ghostMaterial == null)
        {
            Debug.LogWarning("Ghost material is null.");
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = ghostMaterial;
            }

            renderer.sharedMaterials = materials;
        }

        Debug.Log("Applied ghost material to " + renderers.Length + " renderers.");
    }
}
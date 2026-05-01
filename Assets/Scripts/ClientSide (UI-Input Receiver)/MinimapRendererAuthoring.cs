using Unity.Entities;
using UnityEngine;

public class MinimapRendererAuthoring : MonoBehaviour
{
    [SerializeField] GameObject SelectedIcon;
    [SerializeField] GameObject NonSelectIcon;
    [SerializeField] int Scale;

    private void OnValidate()
    {
        if (SelectedIcon == null || NonSelectIcon == null)
        {
            Debug.LogError("SelectedIcon and NonSelectIcon must be assigned.");
            return;
        }
        SelectedIcon.transform.localScale = Vector3.one * Scale;
        NonSelectIcon.transform.localScale = Vector3.one * Scale;
    }
    public class MinimapRendererBaker : Baker<MinimapRendererAuthoring>
    {
        public override void Bake(MinimapRendererAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new MinimapRenderer
            {
                selectedIcon = GetEntity(authoring.SelectedIcon, TransformUsageFlags.Dynamic),
                nonSelectIcon = GetEntity(authoring.NonSelectIcon, TransformUsageFlags.Dynamic),
            });
        }
    }
}
public struct MinimapRenderer : IComponentData
{
    public Entity selectedIcon;
    public Entity nonSelectIcon;
}
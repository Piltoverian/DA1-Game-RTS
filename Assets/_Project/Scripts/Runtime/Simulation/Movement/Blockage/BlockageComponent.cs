using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Dữ liệu kích thước vật cản 2D trên mặt phẳng XZ.
/// QUY CHUẨN KIẾN TRÚC: Collider phải luôn nằm ở Root GameObject của Prefab
/// để BlockageData được bake trực tiếp vào Root Entity, làm nguồn chân lý duy nhất cho diện tích công trình.
/// </summary>
public struct BlockageData : IComponentData
{
    public StartEndRect LocalRect;
    public int CustomCost;

    public readonly float3 GetWorldCenter(in float3 entityPos)
    {
        float2 centerOffset = (LocalRect.MinPoint + LocalRect.MaxPoint) * 0.5f;
        return new float3(entityPos.x + centerOffset.x, entityPos.y, entityPos.z + centerOffset.y);
    }
}

/// <summary>
/// Cleanup component để tự động phục hồi Grid Cost khi vật cản bị xóa khỏi thế giới (Destroy).
/// </summary>
public struct BlockageCleanupData : ICleanupComponentData
{
    public float3 Position;
    public StartEndRect LocalRect;
}

/// <summary>
/// Tag đánh dấu Blockage mới sinh ra cần được hệ thống Grid tự động nạp Cost.
/// </summary>
public struct BlockageNeedBakeTag : IComponentData, IEnableableComponent
{
}

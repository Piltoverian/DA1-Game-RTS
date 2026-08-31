using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Dữ liệu kích thước vật cản 2D trên mặt phẳng XZ.
/// Tọa độ vị trí luôn được đọc từ LocalTransform.Position thời gian thực.
/// </summary>
public struct BlockageData : IComponentData
{
    public StartEndRect LocalRect; // Vùng chữ nhật tương đối (offset so với Position của Entity)
    public int CustomCost;         // Giá trị Cost áp lên Grid (Mặc định = 255: vật cản cứng)
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

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BlockageBoxColliderBaker : Baker<BoxCollider>
{
    public override void Bake(BoxCollider authoring) => BlockageBakerHelper.BakeBlockage(this, authoring);
}

public class BlockageCapsuleColliderBaker : Baker<CapsuleCollider>
{
    public override void Bake(CapsuleCollider authoring) => BlockageBakerHelper.BakeBlockage(this, authoring);
}

public class BlockageSphereColliderBaker : Baker<SphereCollider>
{
    public override void Bake(SphereCollider authoring) => BlockageBakerHelper.BakeBlockage(this, authoring);
}

public class BlockageMeshColliderBaker : Baker<MeshCollider>
{
    public override void Bake(MeshCollider authoring) => BlockageBakerHelper.BakeBlockage(this, authoring);
}

public class BlockageTerrainColliderBaker : Baker<TerrainCollider>
{
    public override void Bake(TerrainCollider authoring) => BlockageBakerHelper.BakeBlockage(this, authoring);
}

public static class BlockageBakerHelper
{
    public static void BakeBlockage<T>(Baker<T> baker, Collider collider) where T : Component
    {
        // 1. Bỏ qua nếu là Unit di chuyển HOẶC là mặt sàn Grid bản đồ
        if (baker.GetComponent<MovementAgentAuthoring>() != null || baker.GetComponent<GridAuthoring>() != null)
            return;

        Entity entity = baker.GetEntity(TransformUsageFlags.Dynamic);

        Vector3 scale = collider.transform.lossyScale;
        Vector3 localCenter = Vector3.zero;
        Vector3 localSize = Vector3.one;

        if (collider is BoxCollider box)
        {
            localCenter = Vector3.Scale(box.center, scale);
            localSize = Vector3.Scale(box.size, scale);
        }
        else if (collider is SphereCollider sphere)
        {
            float maxRadiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            localCenter = Vector3.Scale(sphere.center, scale);
            float diam = sphere.radius * 2f * maxRadiusScale;
            localSize = new Vector3(diam, diam, diam);
        }
        else if (collider is CapsuleCollider capsule)
        {
            float maxRadiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            localCenter = Vector3.Scale(capsule.center, scale);
            localSize = new Vector3(capsule.radius * 2f * maxRadiusScale, capsule.height * Mathf.Abs(scale.y), capsule.radius * 2f * maxRadiusScale);
        }
        else if (collider is MeshCollider meshCol && meshCol.sharedMesh != null)
        {
            Bounds mb = meshCol.sharedMesh.bounds;
            localCenter = Vector3.Scale(mb.center, scale);
            localSize = Vector3.Scale(mb.size, scale);
        }
        else
        {
            Bounds bounds = collider.bounds;
            Vector3 pos = collider.transform.position;
            localCenter = bounds.center - pos;
            localSize = bounds.size;
        }

        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        float halfX = localSize.x * 0.5f;
        float halfZ = localSize.z * 0.5f;

        float2 minOffset = new float2(localCenter.x - halfX, localCenter.z - halfZ);
        float2 maxOffset = new float2(localCenter.x + halfX, localCenter.z + halfZ);

        StartEndRect localRect = new StartEndRect(minOffset);
        localRect.ExpandTo(maxOffset);

        baker.AddComponent(entity, new BlockageData
        {
            LocalRect = localRect,
            CustomCost = 255
        });

        baker.AddComponent(entity, new BlockageCleanupData
        {
            Position = float3.zero,
            LocalRect = localRect
        });

        baker.AddComponent<BlockageNeedBakeTag>(entity);
    }
}

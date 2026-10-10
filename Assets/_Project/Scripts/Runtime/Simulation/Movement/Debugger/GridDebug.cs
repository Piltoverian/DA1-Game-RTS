using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public enum FlowFieldDebugMode
{
    None,
    Grid,
    Islands,      // Chế độ xem các hòn đảo
    Integration,  // Chi phí tích lũy
    Direction,    // Hướng di chuyển
    Walkability   // Actual movement cost cells at logical Y=0
}

public class FlowFieldDebugDrawer : MonoBehaviour
{
    public FlowFieldDebugMode mode = FlowFieldDebugMode.Grid;

    [Header("Settings")]
    public int drawStep = 1;      // Vẽ chi tiết hơn (mặc định = 1)
    public float arrowScale = 0.5f;
    public bool showLabel = false; // Hiển thị số liệu (Cost/IslandID)

    [Header("Multi-Field Selection")]
    public int targetFieldIndex = 0; // Chọn Field thứ mấy để hiển thị
    [Header("Movement grid (green=open, red=blocked, cyan=ramp, magenta=mismatch)")]
    [Range(4, 64)] public int movementViewRadius = 24;
    public bool drawThroughTerrain = true;
    public float movementPlaneY = 0.03f;
    [Tooltip("Lift the same movement cells onto the visible terrain for alignment. Disable to inspect the logical Y=0 plane.")]
    public bool alignToVisibleTerrain = true;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("RTS/Debug/Audit Movement Navigation (Play Mode)")]
    static void AuditNavigation()
    {
        var world=World.DefaultGameObjectInjectionWorld;
        if(!Application.isPlaying||world==null||!world.IsCreated){Debug.LogWarning("Enter Play Mode to audit the live movement grid.");return;}
        var em=world.EntityManager;
        using var query=em.CreateEntityQuery(typeof(GridComponent),typeof(GridNodeCost),typeof(GridTerrain));
        if(query.CalculateEntityCount()!=1){Debug.LogWarning("Expected one movement grid with terrain.");return;}
        var entity=query.GetSingletonEntity();var grid=em.GetComponentData<GridComponent>(entity);
        var costs=em.GetBuffer<GridNodeCost>(entity,true);var terrain=em.GetBuffer<GridTerrain>(entity,true);
        if(costs.Length!=grid.width*grid.height||terrain.Length!=costs.Length){Debug.LogError("Movement grid/terrain buffer dimensions do not match.");return;}
        int blocked=0,openedTerrain=0,illegalTierEdges=0,agentsBlocked=0,unsafeRawDirections=0,unsafeStoredDirections=0,unfinishedFields=0;
        bool Open(int x,int z)=>x>=0&&z>=0&&x<grid.width&&z<grid.height&&costs[z*grid.width+x].cost<255;
        bool Legal(int x,int z,int dx,int dz)=>Open(x,z)&&Open(x+dx,z+dz)&&(dx==0||dz==0||Open(x+dx,z)&&Open(x,z+dz));
        for(int i=0;i<costs.Length;i++){
            if(costs[i].cost>=255)blocked++;
            if(!terrain[i].walkable&&costs[i].cost<255)openedTerrain++;
            int x=i%grid.width,z=i/grid.width;
            for(int d=0;d<2;d++){
                int nx=x+(d==0?1:0),nz=z+(d==1?1:0);
                if(!Open(x,z)||!Open(nx,nz))continue;
                var a=terrain[i];var b=terrain[nz*grid.width+nx];
                if(a.heightLevel!=b.heightLevel&&(math.abs(a.heightLevel-b.heightLevel)!=1||a.RampId<0||b.RampId<0))illegalTierEdges++;
            }
        }
        using var agents=em.CreateEntityQuery(typeof(MovementAgentComponent),typeof(LocalTransform));
        using var positions=agents.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        foreach(var position in positions){var p=GridHelper.WorldToGrid(position.Position,grid);if(!Open(p.x,p.y))agentsBlocked++;}
        using var fields=em.CreateEntityQuery(typeof(FlowField),typeof(FieldNode),typeof(FlowFieldStatus));
        using var fieldEntities=fields.ToEntityArray(Allocator.Temp);
        foreach(var field in fieldEntities){
            if(em.GetComponentData<FlowFieldStatus>(field).Value!=FieldState.Ready){unfinishedFields++;continue;}
            var nodes=em.GetBuffer<FieldNode>(field,true);if(nodes.Length!=costs.Length)continue;
            for(int i=0;i<nodes.Length;i++){
                int x=i%grid.width,z=i/grid.width;if(!Open(x,z)||nodes[i].bestcost==int.MaxValue)continue;
                float2 raw=UnitMovementMath.GetRawDirection(new int2(x,z),nodes.AsNativeArray(),grid.width,grid.height);
                int dx=(int)math.sign(raw.x),dz=(int)math.sign(raw.y);
                if((dx!=0||dz!=0)&&!Legal(x,z,dx,dz))unsafeRawDirections++;
                dx=(int)math.sign(nodes[i].direction.x);dz=(int)math.sign(nodes[i].direction.y);
                if((dx!=0||dz!=0)&&!Legal(x,z,dx,dz))unsafeStoredDirections++;
            }
        }
        int missingResourceBlockers=0,openResourceCells=0,pendingResourceBakes=0,rampHeightSeams=0;
        using var resources=em.CreateEntityQuery(typeof(ResourceNodeTag),typeof(LocalTransform));
        using var resourceEntities=resources.ToEntityArray(Allocator.Temp);
        foreach(var resource in resourceEntities){
            if(!em.HasComponent<BlockageData>(resource)){missingResourceBlockers++;continue;}
            if(em.HasComponent<BlockageNeedBakeTag>(resource)&&em.IsComponentEnabled<BlockageNeedBakeTag>(resource))pendingResourceBakes++;
            var position=em.GetComponentData<LocalTransform>(resource).Position;
            var blockage=em.GetComponentData<BlockageData>(resource);
            if(!PlayerSpawnPlacement.TryFootprint(grid,position,blockage.LocalRect,out var min,out var max)){missingResourceBlockers++;continue;}
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)if(Open(x,z))openResourceCells++;
        }
        var surface=TerrainVisualSurface.Active;
        if(surface!=null&&surface.map.grid.width==grid.width&&surface.map.grid.height==grid.height&&surface.map.cells.Length==costs.Length){
            for(int i=0;i<costs.Length;i++){
                int x=i%grid.width,z=i/grid.width;if(!Open(x,z))continue;
                for(int d=0;d<2;d++){
                    int nx=x+(d==0?1:0),nz=z+(d==1?1:0);if(!Open(nx,nz))continue;
                    int j=nz*grid.width+nx;if(!surface.IsRamp(i)&&!surface.IsRamp(j))continue;
                    bool seam=false;
                    foreach(float t in new[]{0f,.5f,1f}){
                        float a=d==0?surface.CellHeight(i,1,t):surface.CellHeight(i,t,1);
                        float b=d==0?surface.CellHeight(j,0,t):surface.CellHeight(j,t,0);
                        if(math.abs(a-b)>.001f)seam=true;
                    }
                    if(seam)rampHeightSeams++;
                }
            }
        }
        Debug.Log($"MOVEMENT AUDIT {grid.width}x{grid.height}, cell={grid.cellsize}: blocked={blocked}; terrain opened incorrectly={openedTerrain}; illegal tier edges={illegalTierEdges}; agents in blocked/outside cells={agentsBlocked}; unsafe raw directions={unsafeRawDirections}; unsafe stored directions={unsafeStoredDirections}; unfinished fields={unfinishedFields}; resources missing blocker/invalid footprint={missingResourceBlockers}; resource footprint cells still open={openResourceCells}; pending resource bakes={pendingResourceBakes}; open ramp edges with height discontinuity={rampHeightSeams}. Raw/stored counts are across Ready fields. Run after bootstrap and cost updates finish.");
    }
#endif

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 1. Lấy Grid Entity
        using var gridQuery = em.CreateEntityQuery(typeof(GridComponent));
        if (gridQuery.CalculateEntityCount()!=1) return;
        Entity gridEntity = gridQuery.GetSingletonEntity();
        GridComponent grid = em.GetComponentData<GridComponent>(gridEntity);

        // 2. Xử lý theo Mode
        switch (mode)
        {
            case FlowFieldDebugMode.Walkability:
                if(em.HasBuffer<GridNodeCost>(gridEntity))DrawWalkability(em,gridEntity,grid);
                break;
            case FlowFieldDebugMode.Grid:
                DrawGrid(grid);
                break;

            case FlowFieldDebugMode.Islands:
                if (em.HasBuffer<GridIsland>(gridEntity))
                {
                    DrawIslands(grid, em.GetBuffer<GridIsland>(gridEntity));
                }
                break;

            case FlowFieldDebugMode.Integration:
            case FlowFieldDebugMode.Direction:
                DrawFlowField(em, grid);
                break;
        }
    }

    void DrawWalkability(EntityManager em, Entity entity, GridComponent grid)
    {
        var costs=em.GetBuffer<GridNodeCost>(entity,true);
        if(grid.cellsize<=0 || costs.Length!=grid.width*grid.height)return;
        bool hasTerrain=em.HasBuffer<GridTerrain>(entity);
        var terrain=hasTerrain?em.GetBuffer<GridTerrain>(entity,true):default;
        hasTerrain=hasTerrain&&terrain.Length==costs.Length;
        var surface=alignToVisibleTerrain?TerrainVisualSurface.Active:null;
        if(surface!=null&&(surface.map.grid.width!=grid.width||surface.map.grid.height!=grid.height||surface.map.grid.cellsize!=grid.cellsize||!surface.map.grid.origin.Equals(grid.origin)))surface=null;
        Camera camera=Camera.current?Camera.current:Camera.main;
        var focus=GridHelper.GridToWorld(new int2(grid.width/2,grid.height/2),grid);
        if(camera){var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));if(surface!=null&&surface.Raycast(ray,out var logical,out _))focus=logical;else if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))focus=ray.GetPoint(distance);}
        int2 center=GridHelper.WorldToGrid(focus,grid);
        int radius=Mathf.Clamp(movementViewRadius,4,64),step=Mathf.Max(1,drawStep);
        var corners=new Vector3[4];
        for(int z=Mathf.Max(0,center.y-radius);z<=Mathf.Min(grid.height-1,center.y+radius);z+=step)
        for(int x=Mathf.Max(0,center.x-radius);x<=Mathf.Min(grid.width-1,center.x+radius);x+=step){
            int i=z*grid.width+x;bool open=costs[i].cost<255;
            Color color=open?Color.green:Color.red;
            if(hasTerrain){if(!terrain[i].walkable&&open)color=Color.magenta;else if(open&&terrain[i].RampId>=0)color=Color.cyan;}
            Vector3 p=GridHelper.GridToWorld(new int2(x,z),grid);p.y=movementPlaneY;
            float h=grid.cellsize*.46f;
            corners[0]=p+new Vector3(-h,0,-h);corners[1]=p+new Vector3(h,0,-h);
            corners[2]=p+new Vector3(h,0,h);corners[3]=p+new Vector3(-h,0,h);
            if(surface!=null){
                // Evaluate inside this cell, rather than sampling the neighbour
                // at a shared cliff boundary. Coordinates and costs stay unchanged.
                const float low=.04f,high=.96f;
                corners[0].y+=surface.CellHeight(i,low,low);corners[1].y+=surface.CellHeight(i,high,low);
                corners[2].y+=surface.CellHeight(i,high,high);corners[3].y+=surface.CellHeight(i,low,high);
                p.y+=surface.CellHeight(i,.5f,.5f);
            }
            // Handles' filled-rectangle path crashes in URP GameView gizmo
            // rendering. Use engine wire primitives, without Editor GL calls.
            Gizmos.color=color;
            for(int k=0;k<4;k++){
                Gizmos.DrawLine(corners[k],corners[(k+1)%4]);
                if(drawThroughTerrain)Debug.DrawLine(corners[k],corners[(k+1)%4],color,0,false);
            }
        }
    }

    void DrawGrid(GridComponent grid)
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        for (int x = 0; x <= grid.width; x++)
        {
            Vector3 start = grid.origin + new float3(x * grid.cellsize, 0, 0);
            Vector3 end = grid.origin + new float3(x * grid.cellsize, 0, grid.height * grid.cellsize);
            Gizmos.DrawLine(start, end);
        }
        for (int y = 0; y <= grid.height; y++)
        {
            Vector3 start = grid.origin + new float3(0, 0, y * grid.cellsize);
            Vector3 end = grid.origin + new float3(grid.width * grid.cellsize, 0, y * grid.cellsize);
            Gizmos.DrawLine(start, end);
        }
    }

    void DrawIslands(GridComponent grid, DynamicBuffer<GridIsland> islands)
    {
        for (int i = 0; i < islands.Length; i++)
        {
            int id = islands[i].islandID;
            if (id <= 0) continue;

            int2 pos = GridHelper.GetGridPosFromIndex(i, grid);
            if (pos.x % math.max(1,drawStep) != 0 || pos.y % math.max(1,drawStep) != 0) continue;

            // Tạo màu ngẫu nhiên nhưng cố định theo ID
            Unity.Mathematics.Random r = new Unity.Mathematics.Random((uint)id * 7823); 
            Gizmos.color = new Color(r.NextFloat(0.2f, 1f), r.NextFloat(0.2f, 1f), r.NextFloat(0.2f, 1f), 0.5f);

            float3 worldPos = GridHelper.GridToWorld(pos, grid);
            Gizmos.DrawCube(worldPos + new float3(0, 0.05f, 0), new float3(grid.cellsize * 0.9f, 0.1f, grid.cellsize * 0.9f));
        }
    }

    void DrawFlowField(EntityManager em, GridComponent grid)
    {
        // Tìm tất cả các thực thể có FlowField
        using var fieldQuery = em.CreateEntityQuery(typeof(FlowField), typeof(FieldNode));
        using var entities = fieldQuery.ToEntityArray(Allocator.Temp);
        
        if (entities.Length == 0) return;

        // Giới hạn index hợp lệ
        int index = math.clamp(targetFieldIndex, 0, entities.Length - 1);
        Entity targetEntity = entities[index];
        var buffer = em.GetBuffer<FieldNode>(targetEntity);

        for (int i = 0; i < buffer.Length; i++)
        {
            int2 pos = GridHelper.GetGridPosFromIndex(i, grid);
            if (pos.x % math.max(1,drawStep) != 0 || pos.y % math.max(1,drawStep) != 0) continue;

            float3 worldPos = GridHelper.GridToWorld(pos, grid);
            FieldNode node = buffer[i];

            if (mode == FlowFieldDebugMode.Integration)
            {
                if (node.bestcost == int.MaxValue) continue;
                float t = math.saturate(node.bestcost / 1000f);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, t);
                Gizmos.DrawCube(worldPos + new float3(0, 0.1f, 0), new float3(grid.cellsize * 0.8f, 0.05f, grid.cellsize * 0.8f));
            }
            else if (mode == FlowFieldDebugMode.Direction)
            {
                if (math.lengthsq(node.direction) < 0.001f) continue;
                Gizmos.color = Color.yellow;
                Vector3 end = (Vector3)worldPos + new Vector3(node.direction.x, 0, node.direction.y) * arrowScale;
                Gizmos.DrawLine(worldPos + new float3(0, 0.2f, 0), (float3)end + new float3(0, 0.2f, 0));
            }
        }
    }
}

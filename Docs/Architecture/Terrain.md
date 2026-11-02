# Terrain: map, height và render

## Cấu hình và entry point

Main dùng [MapGenConfig.asset](../../Assets/_Project/Tests/Fixtures/MapGeneration/MapGenConfig.asset) và [LunarTheme](../../Assets/_Project/Data/Terrain/Generated/MoonTerrain/LunarTheme.asset). GridAuthoring/PlayerBootstrapAuthoring nằm trong EntitySubscene; không tạo generator thứ hai trong renderer. Các asset có tên Fixtures vẫn là dữ liệu runtime.

[MapGenConfig.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapGenConfig.cs) gom TerrainGenerationSettings, bootstrap và Resources. Đổi seed/players/protectedRadius/height/ramp/mountain qua config, không sửa một preset song song ở presentation. Default và giới hạn số cụ thể nằm trong SO/source validator, tránh sao chép bảng default dễ lệch.

## Pipeline authoritative

```text
GridAuthoring → MapGenerationRecipe + GridTerrain/Spawn buffers rỗng
    → MapTerrainGenerationSystem → TerrainGeneration.Generate
    → SharedVertexTerrainGeneration → topology/ramps/validation
    → MapTerrainSnapshot.Apply → TerrainReady
    → GridInit/cost/islands → NavigationReady
    → PlayerBootstrapSystem → GameplayReady
```

Nguồn: [MapPipeline.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapPipeline.cs), [TerrainGeneration.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainGeneration.cs), [SharedVertexTerrainGeneration.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/SharedVertexTerrainGeneration.cs), [TerrainTopologyOptimizer.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainTopologyOptimizer.cs).

MapBuildState phase là readiness của map/navigation/bootstrap, không phải MatchPhase.Running. Revision/hash xác định source map; snapshot cùng revision/hash là retransmission, không reset live blockers/navigation. Validate kích thước, spawn, revision và hash trước publish buffer. Không serialize map source đầy đủ ra client chỉ vì API nội bộ có chữ Snapshot.

## Invariant của generator

- Grid cell index theo width; shared vertex grid theo width+1. RNG/permutation C# là chuẩn của project, không hứa cùng seed ra kết quả giống JS hoặc mọi nền tảng float.
- Lấy mẫu height ở shared vertices, lượng tử hóa/relax, xử lý saddle và dựng terraces. LimitHeightSteps hạn chế chênh tầng ngoài núi.
- Spawn có vùng protected; base geometry cần giữ phẳng, đi được và không bị mountain/ramp xâm phạm. Không coi chia sector là bảo đảm cân bằng tuyệt đối giữa player.
- Núi là vùng blocked; không đào ramp xuyên mountain. Nhóm núi và mức height được thống nhất trước topology cuối.
- Cliff đánh phía cao; ramp nối low/high hợp lệ và không mở crossing ngoài ý định. RampDirection là hướng từ thấp lên cao, RampId nhóm corridor; không suy hướng từ mọi ô trong corridor.
- Connectivity là điều kiện bắt buộc. Pocket nhỏ ngoài protected/spawn có thể bị seal theo budget; thiếu ramp quota không tự cho phép phá luật connectivity/protection.
- Candidate sai phải bị loại/thử lại có giới hạn; hết lượt phải Failed, không trả map vi phạm invariant.

Khi đổi thuật toán, lần theo Generate → TryFinishTerraces → topology Bake/OpenRamps/repair/seal → Validate. Không chuyển các bước sửa height ra ngoài chỉ để tên hàm ngắn mà làm đổi thứ tự validation.

## Bootstrap, resource và blocker

[PlayerBootstrapAuthoring.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerBootstrapAuthoring.cs) bake roster/settings và liên kết registry/Civ. [PlayerBootstrapSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Players/PlayerBootstrapSystem.cs) chờ navigation, kiểm tra prefab/placement, lập kế hoạch rồi instantiate bằng ECB, giữ ledger để dọn/rollback và finalize trước Ready.

Worker/townhall do CivBlob resolve từ civ/default registry, xem [GameData](../Guides/GameData.md). PlayerContextAuthoring ở chế độ bootstrap liên kết context roster. Không tạo player/resource thứ hai vì renderer hoặc TestPlayer cần context.

[MapResourcePlanner.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/MapResourcePlanner.cs) dùng catalog/quota/role Main, Secondary, Advantage, Contested. AmountPerMine thuộc config node; weight là xác suất chọn, không phải stock/income. Khoảng cách config theo ô hoặc tỷ lệ map như tooltip; không đổi thành world distance bằng hằng số khác. Placement xét footprint, overlap, protected và connectivity; thiếu layout hợp lệ phải phản ánh failure/report của planner.

GridSnapper/ModelSnapper căn hình/prefab; BlockageData dùng anchor logic. Model centering không được làm lệch footprint navigation. Bake blocker và CostChange chịu trách nhiệm occupancy, không lấy pixel atlas làm collider.

## Height và picking

[TerrainSurfaceData.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainSurfaceData.cs) tính bốn corner height, dùng minimumMapLevel của map đầy đủ và canonical tier rise. [TerrainHeightSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/Movement/Height/TerrainHeightSystem.cs) cache geometry simulation độc lập renderer.

PendingHeight được bật khi XZ đổi/spawn; pass height đặt Y = Sample(XZ) + TerrainHeightOffset rồi tắt pending, không cộng dồn. Navigation/avoidance quyết định XZ; entity Y vẫn có thể cần cho render/collider/projectile. Mesh terrain cố ý có vertex local/world Y=0; shader/projection thể hiện độ cao.

[TerrainVisualSurface.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainVisualSurface.cs) chỉ sample/pick từ visual được cấp. Raycast đi qua ô và hai triangle top; ô không có visual không được đọc geometry thật. Move vào Unknown vẫn có đường logic theo input hiện có; không lấy hình terrain làm quyền biết occupancy.

## Geometry dùng chung cho local và network

```text
TerrainVisualSourceSystem compile map đầy đủ một lần theo revision/hash
    ├─ LocalTerrainView lọc index Explored
    └─ server lọc theo PlayerConnection → chunk Ghost → receiver decode
                        ↓
                   TerrainView
                        ↓
             MapTerrainPresentationSystem
                        ↓
              TerrainChunkRenderer.Build(grid, visual)
```

[TerrainVisualSourceSystem.cs](../../Assets/_Project/Scripts/Runtime/Simulation/MapGeneration/TerrainVisualSourceSystem.cs) sở hữu full compiled source. [LocalTerrainView.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/LocalTerrainView.cs) không có compiler fallback cho scene cũ; thiếu cấu hình/source thì chờ. Preview tự compile sample rồi đi cùng đầu vào renderer, không còn overload nhận GeneratedTerrain ở renderer.

[TerrainView.cs](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainView.cs) chứa grid metadata, authorized visual cells, settings, GeometryVersion và mask. Source/scope/Explored membership đổi thì geometry version đổi; chỉ Visible đổi cập nhật mask, không rebuild mesh. Thêm exploration hiện vẫn rebuild toàn tập hình đã biết, chưa incremental mesh. [FOW](FogOfWarRendering.md) sở hữu luật quyền biết; [Multiplayer](../Multiplayer/README.md) sở hữu chunk/session/ACK.

Binding.Release chỉ dọn renderer/mesh/surface; Binding.Dispose dọn thêm local view. Network receiver sở hữu view/mask của nó; binding chỉ mượn. TerrainMapRenderer đổi binding theo World, ưu tiên client trên host, không render full server source.

## Atlas tool và hợp đồng hình

Entry points đã có: `RTS → Terrain → Material Atlas`, `RTS → Terrain → Create JadeSlate Atlas Example`; source [TerrainAtlasWindow.cs](../../Assets/_Project/Scripts/Editor/Terrain/TerrainAtlasWindow.cs), [TerrainAtlasAuthoring.cs](../../Assets/_Project/Scripts/Editor/Terrain/TerrainAtlasAuthoring.cs), [TerrainCanonicalBaker.cs](../../Assets/_Project/Scripts/Editor/Terrain/TerrainCanonicalBaker.cs), [TerrainAtlasPreview.cs](../../Assets/_Project/Scripts/Editor/Terrain/TerrainAtlasPreview.cs).

- Input vật liệu phẳng layout 3×2, sáu panel vuông: ground lush/sparse, ramp earth, mountain slate, cliff slate, ground detail. Không đưa phối cảnh/diamond/bóng đổ baked thành material source.
- Baker tạo bộ top/cap/wall và geometry-depth cặp tương ứng; top/wall canvas 128×128, bank cap có cùng chiều cao. Không crop canvas để chữa pivot.
- Geometry texture phải readable, linear, giữ kích thước và pairing; runtime pack color/geometry đồng bộ. Generated material dùng source atlas/UV world; manual color phải có geometry hợp lệ đi kèm.
- Reimport vào cùng TerrainTheme để giữ GUID/reference/settings. Ownership/override/rollback của importer bảo vệ asset do người dùng sửa; receipt hợp đồng importer có nhiệm vụ vận hành, khác log/receipt kiểm chứng một lần.
- Source art JadeSlate và asset runtime MoonTerrain/LunarTheme có vai trò khác nhau; đừng xóa theme Main vì reference art chỉ còn một bộ. README cạnh asset giữ import/license tại chỗ.

Preview tạo ảnh trong bộ nhớ, không lưu scene. Kiểm tra seam, ramp/wall, mountain shape, sorting, fade và ray picking ở nhiều mức zoom. [TerrainChunkRenderer](../../Assets/_Project/Scripts/Runtime/Presentation/Terrain/TerrainChunkRenderer.cs) giữ quads phẳng Y=0 và camera canonical pitch/yaw; pan/orthographic zoom được phép, tự đổi góc render sẽ phá hợp đồng projection/depth.

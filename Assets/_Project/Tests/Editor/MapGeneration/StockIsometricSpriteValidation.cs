using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

public static class StockIsometricSpriteValidation
{
    public const string ThemePath="Assets/_Project/Data/Theme/SO/ReferenceIsometricTheme.asset";
    public static void ValidateSpriteScale(){
        var theme=ScriptableObject.CreateInstance<TerrainTheme>();var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);
        var config=ScriptableObject.CreateInstance<MapGenConfig>();
        try{
            config.MapSize=GridAuthoring.MapType.Extreme;config.MinimumCellSize=1;
            plane.transform.localScale=new Vector3(100,1,100);
            var authoring=plane.AddComponent<GridAuthoring>();authoring.Config=config;
            var grid=authoring.GetGridDefinition();
            if(Mathf.Abs(grid.cellsize-1000f/512)>.0001f)throw new Exception("Grid cell size must come from mesh bounds/config resolution, not minimum size");
            var forward=Quaternion.Euler(30,45,0)*Vector3.forward;
            float expected=grid.cellsize*.25f*Mathf.Sqrt(2)/Mathf.Cos(30*Mathf.Deg2Rad);
            float rise=theme.ResolveTierRise(grid.cellsize,forward);
            if(Mathf.Abs(rise-expected)>.0001f)throw new Exception("Sprite pitch/scale formula incorrect");
            if(Mathf.Abs(theme.ResolveTierRise(grid.cellsize,forward)-rise)>.0001f||Mathf.Abs(theme.ResolveTierRise(grid.cellsize*2,forward)-rise*2)>.0001f)
                throw new Exception("Canonical rise must scale with actual cell size");
            Debug.Log($"TERRAIN_SPRITE_SCALE_PASS: mesh/config cellsize={grid.cellsize}; locked rise={rise}; 2x cell size gives 2x rise.");
        }finally{UnityEngine.Object.DestroyImmediate(plane);UnityEngine.Object.DestroyImmediate(theme);UnityEngine.Object.DestroyImmediate(config);}
    }
    [MenuItem("RTS/Terrain/Use Reference Sprites Stock Unlit")]
    public static void UseInScene(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var theme=LoadTheme();
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<TerrainMapRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
            Undo.RecordObject(renderer,"Use reference sprites");renderer.theme=theme;
            EditorUtility.SetDirty(renderer);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
        }
        if(Camera.main) { Undo.RecordObject(Camera.main,"Set isometric camera"); Undo.RecordObject(Camera.main.transform,"Set isometric angle"); IsometricTerrainCamera.Configure(Camera.main); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(Camera.main.gameObject.scene); }
        Debug.Log("Reference isometric sprites: Unity stock Unlit, custom terrain shader disabled; 3D occlusion behind cliff is approximate.");
    }
    public static void ValidateNamedSpriteSlots()
    {
        var source=LoadTheme();var copy=UnityEngine.Object.Instantiate(source);
        var marker=new Texture2D(1,1);
        try
        {
            const string folder="Assets/_Project/Data/Terrain/Sprites/ReferenceIsometricSprites/Canonical/";
            for(int mask=0;mask<16;mask++)
            {
                string expected=folder+"top-"+mask.ToString("X1")+".png";
                if(AssetDatabase.GetAssetPath(source.GetTopSprite(mask))!=expected)throw new Exception("Named top slot mapping mismatch at "+mask);
                copy.SetTopSprite(mask,marker);
                for(int other=0;other<16;other++)if(copy.GetTopSprite(other)!=(other==mask?marker:source.GetTopSprite(other)))throw new Exception("Top setter changed the wrong slot");
                copy.SetTopSprite(mask,source.GetTopSprite(mask));
            }
            for(int side=0;side<2;side++)for(int mask=0;mask<16;mask++)
            {
                if(!TerrainTheme.IsVisibleWallMask(mask))
                {
                    if(source.GetWallSprite(side,mask))throw new Exception("Empty wall mask still references an image");
                    copy.SetWallSprite(side,mask,null);
                    bool rejected=false;try{copy.SetWallSprite(side,mask,marker);}catch(ArgumentException){rejected=true;}
                    if(!rejected)throw new Exception("Empty wall mask accepted a texture");
                    continue;
                }
                string expected=folder+"cliff-"+side+"-"+mask.ToString("X1")+".png";
                if(AssetDatabase.GetAssetPath(source.GetWallSprite(side,mask))!=expected)throw new Exception("Named cliff slot mapping mismatch at "+side+"/"+mask);
                copy.SetWallSprite(side,mask,marker);
                for(int s=0;s<2;s++)for(int m=0;m<16;m++)if(copy.GetWallSprite(s,m)!=(s==side&&m==mask?marker:source.GetWallSprite(s,m)))throw new Exception("Wall setter changed the wrong slot");
                copy.SetWallSprite(side,mask,source.GetWallSprite(side,mask));
            }
            void Reject(Action action){try{action();}catch(ArgumentOutOfRangeException){return;}throw new Exception("Invalid sprite key was accepted");}
            foreach(int mask in new[]{-1,16}){Reject(()=>copy.GetTopSprite(mask));Reject(()=>copy.SetTopSprite(mask,marker));Reject(()=>copy.GetWallSprite(0,mask));Reject(()=>copy.SetWallSprite(0,mask,marker));}
            foreach(int side in new[]{-1,2}){Reject(()=>copy.GetWallSprite(side,0));Reject(()=>copy.SetWallSprite(side,0,marker));}
            System.IO.Directory.CreateDirectory("Artifacts/TerrainValidation/NamedSprites");
            System.IO.File.WriteAllText("Artifacts/TerrainValidation/NamedSprites/validation.txt","PASS: all 26 visible tile fields map to their original PNGs; 22 empty wall masks return null; setters affect only the selected field; invalid masks/sides rejected.");
            Debug.Log("TERRAIN_NAMED_SPRITES_PASS: 26 tile fields preserve mapping; 22 empty wall masks omitted.");
        }
        finally{UnityEngine.Object.DestroyImmediate(marker);UnityEngine.Object.DestroyImmediate(copy);}
    }
    public static TerrainTheme LoadTheme(){
        var theme=AssetDatabase.LoadAssetAtPath<TerrainTheme>(ThemePath);
        if(!theme)throw new Exception("Missing canonical ReferenceIsometricTheme; restore the asset instead of recreating a prototype");
        return theme;
    }
    [MenuItem("RTS/Tests/Validate Terrain Offset Reload Recovery")]
    public static void ValidateOffset(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var previousWorld=World.DefaultGameObjectInjectionWorld;var previousSurface=TerrainVisualSurface.Active;
        using(var world=new World("Terrain offset lifecycle regression")){
            try{
                World.DefaultGameObjectInjectionWorld=world;
                var map=new GeneratedTerrain{grid=new GridComponent{width=2,height=1,cellsize=1,origin=Vector3.zero},cells=new[]{new GridTerrain{heightLevel=0},new GridTerrain{heightLevel=2}}};
                new TerrainVisualSurface(map,1).Activate();
                var system=world.CreateSystemManaged<TerrainVisualOffsetSystem>();
                var entity=world.EntityManager.CreateEntity(typeof(LocalTransform),typeof(LocalToWorld),typeof(MaterialMeshInfo));
                world.EntityManager.SetComponentData(entity,LocalTransform.FromPosition(new float3(1.5f,0,.5f)));
                world.EntityManager.SetComponentData(entity,new LocalToWorld{Value=float4x4.Translate(new float3(1.5f,0,.5f))});
                // Reproduce the reported failure: managed caches absent after reload.
                foreach(var name in new[]{"baseline","displayed"})typeof(TerrainVisualOffsetSystem).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(system,null);
                system.Update();
                if(Mathf.Abs(world.EntityManager.GetComponentData<LocalToWorld>(entity).Position.y-2)>.001f)throw new Exception("Visual height not applied");
                if(world.EntityManager.GetComponentData<LocalTransform>(entity).Position.y!=0)throw new Exception("Simulation Y changed");
                system.RestoreLogicalMatrices();
                if(world.EntityManager.GetComponentData<LocalToWorld>(entity).Position.y!=0)throw new Exception("Logical matrix not restored");
                system.Update();world.EntityManager.DestroyEntity(entity);system.Update();
                // Exercise the hierarchy path used by nested imported render meshes,
                // including the former helper's stack-buffer boundary (16 ancestors).
                var leaf=world.EntityManager.CreateEntity(typeof(LocalTransform),typeof(LocalToWorld),typeof(MaterialMeshInfo),typeof(PostTransformMatrix));
                world.EntityManager.SetComponentData(leaf,LocalTransform.FromPosition(new float3(.5f,0,.5f)));
                world.EntityManager.SetComponentData(leaf,new PostTransformMatrix{Value=float4x4.Scale(new float3(2,3,4))});
                var child=leaf;
                for(int i=0;i<20;i++) {
                    var parent=world.EntityManager.CreateEntity(typeof(LocalTransform));
                    world.EntityManager.SetComponentData(parent,LocalTransform.FromPosition(new float3(0,.1f,0)));
                    world.EntityManager.AddComponentData(child,new Parent{Value=parent});child=parent;
                }
                system.Update();
                var nested=world.EntityManager.GetComponentData<LocalToWorld>(leaf).Value;
                if(Mathf.Abs(nested.c3.y-2)>.001f||Mathf.Abs(nested.c1.y-3)>.001f)throw new Exception("Nested transform or post scale changed");
                // A cycle must terminate and keep a valid fallback matrix.
                world.EntityManager.AddComponentData(child,new Parent{Value=leaf});system.Update();
                TerrainVisualSurface.Active.Deactivate();system.Update();
                if(!world.EntityManager.GetComponentData<LocalToWorld>(leaf).Value.Equals(nested))throw new Exception("Inactive surface changed logical hierarchy fallback");
                Debug.Log("TERRAIN_OFFSET_RELOAD_PASS: null managed caches recovered; visual height applies; simulation Y=0; restore and entity destruction safe.");
            }finally{TerrainVisualSurface.Active?.Deactivate();previousSurface?.Activate();World.DefaultGameObjectInjectionWorld=previousWorld;}
        }
    }
}

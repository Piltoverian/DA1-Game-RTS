using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Enable to request snapping; disabled (consumed) only after successful application.
public struct Snapping : IComponentData,IEnableableComponent {}
public struct ModelSnapSource : IComponentData
{
    public float3 Size,Center,RootScale;
    public float3 VisualSize,VisualCenter;
    public float DiameterCells;
    public int Cells;
    public byte Shape; // Box=0, sphere=1, vertical capsule=2.
    public Unity.Physics.Material.CombinePolicy FrictionCombine,RestitutionCombine;
    static Unity.Physics.Material.CombinePolicy Combine(PhysicsMaterialCombine value)=>value switch{
        PhysicsMaterialCombine.Maximum=>Unity.Physics.Material.CombinePolicy.Maximum,
        PhysicsMaterialCombine.Minimum=>Unity.Physics.Material.CombinePolicy.Minimum,
        _=>Unity.Physics.Material.CombinePolicy.ArithmeticMean}; // Matches the Unity Physics baker's supported policies.
    public static ModelSnapSource Capture(GameObject owner,int cells,float diameterCells)
    {
        var collider=owner.GetComponent<UnityEngine.Collider>();
        float3 size,center;byte shape;
        if(collider is UnityEngine.BoxCollider box){size=box.size;center=box.center;shape=0;}
        else if(collider is UnityEngine.SphereCollider sphere){size=new float3(sphere.radius*2);center=sphere.center;shape=1;}
        else if(collider is UnityEngine.CapsuleCollider capsule&&capsule.direction==1){size=new float3(capsule.radius*2,capsule.height,capsule.radius*2);center=capsule.center;shape=2;}
        else throw new InvalidOperationException("Model snapping requires a root BoxCollider, SphereCollider or vertical CapsuleCollider.");
        float3 scale=owner.transform.lossyScale;
        if(cells<1||math.any(size<=0)||math.any(scale<=0)||!math.all(math.isfinite(size))||!math.all(math.isfinite(scale)))throw new InvalidOperationException("Invalid model snapping dimensions");
        var material=collider.sharedMaterial;
        var visual=CaptureVisualBounds(owner,new Bounds(center,size));
        return new ModelSnapSource{Size=size*scale,Center=center*scale,RootScale=scale,Cells=cells,DiameterCells=diameterCells,Shape=shape,
            VisualSize=(float3)visual.size*scale,VisualCenter=(float3)visual.center*scale,
            FrictionCombine=Combine(material?material.frictionCombine:PhysicsMaterialCombine.Average),
            RestitutionCombine=Combine(material?material.bounceCombine:PhysicsMaterialCombine.Average)};
    }

    // Mesh geometry only: selection sprites and health bars must not define the footprint.
    public static Bounds CaptureVisualBounds(GameObject owner,Bounds fallback)
    {
        Bounds result=default;
        bool found=false;
        var toRoot=owner.transform.worldToLocalMatrix;
        foreach(var filter in owner.GetComponentsInChildren<MeshFilter>(true)){
            var renderer=filter.GetComponent<MeshRenderer>();
            if(!renderer||!renderer.enabled||!filter.sharedMesh||filter.GetComponentInParent<HealthBarAuthoring>())continue;
            Include(filter.sharedMesh.bounds,toRoot*filter.transform.localToWorldMatrix);
        }
        foreach(var renderer in owner.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            if(!renderer.enabled||!renderer.sharedMesh||renderer.GetComponentInParent<HealthBarAuthoring>())continue;
            Include(renderer.localBounds,toRoot*renderer.transform.localToWorldMatrix);
        }
        return found?result:fallback;

        void Include(Bounds bounds,Matrix4x4 matrix){
            for(int i=0;i<8;i++){
                var point=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(!found){result=new Bounds(point,Vector3.zero);found=true;}else result.Encapsulate(point);
            }
        }
    }
}

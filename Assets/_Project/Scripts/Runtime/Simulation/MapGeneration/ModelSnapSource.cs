using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Enable to request snapping; disabled (consumed) only after successful application.
public struct Snapping : IComponentData,IEnableableComponent {}
public struct ModelSnapSource : IComponentData
{
    public float3 Size,Center,RootScale;
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
        return new ModelSnapSource{Size=size*scale,Center=center*scale,RootScale=scale,Cells=cells,DiameterCells=diameterCells,Shape=shape,
            FrictionCombine=Combine(material?material.frictionCombine:PhysicsMaterialCombine.Average),
            RestitutionCombine=Combine(material?material.bounceCombine:PhysicsMaterialCombine.Average)};
    }
}

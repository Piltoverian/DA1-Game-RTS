using Unity.Entities;
using UnityEngine;

public interface ITechEffect
{
    void Conduct(EntityManager entityManager, Entity player);
}

// Shared asset containing behavior only. Per-player state belongs to ECS components.
public abstract class TechEffect : ScriptableObject, ITechEffect
{
    public abstract void Conduct(EntityManager entityManager, Entity player);
}

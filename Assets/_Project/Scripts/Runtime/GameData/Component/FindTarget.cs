using Unity.Entities;
public struct FindTarget : IComponentData
{
    public float range;
    public float timer;
    public float timerMax;
}


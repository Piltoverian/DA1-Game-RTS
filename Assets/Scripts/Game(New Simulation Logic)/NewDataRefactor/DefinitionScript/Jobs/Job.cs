using UnityEngine;


public abstract class Job : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public Sprite Icon;
    [Tooltip("The amount of work required to complete this job.")]
    [Min(0.001f)] public float WorkLoad = 1f;
}

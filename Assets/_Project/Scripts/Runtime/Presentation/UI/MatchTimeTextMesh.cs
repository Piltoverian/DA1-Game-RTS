using TMPro;
using Unity.Entities;
using UnityEngine;

public class MatchTimeTextMesh : MonoBehaviour
{
    TextMeshProUGUI textMesh;

    void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh != null) textMesh.text = "Time: --";
    }

    void FixedUpdate()
    {
        if (textMesh == null) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            textMesh.text = "Time: --";
            return;
        }

        using var query = world.EntityManager.CreateEntityQuery(typeof(MatchTimeComponent));
        textMesh.text = query.TryGetSingleton<MatchTimeComponent>(out var matchTime)
            ? "Time: " + Mathf.Round(matchTime.Value).ToString()
            : "Time: --";
    }
}

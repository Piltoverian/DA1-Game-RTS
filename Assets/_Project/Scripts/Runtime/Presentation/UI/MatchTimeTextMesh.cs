using System.Globalization;
using TMPro;
using Unity.Entities;
using UnityEngine;

public class MatchTimeTextMesh : MonoBehaviour
{
    [SerializeField] private string timeFormat = "Time: {0:00}:{1:00}";
    [SerializeField] private string unavailableText = "Time: --:--";
    private TextMeshProUGUI textMesh;
    private World world;
    private EntityQuery query;
    private int lastSecond = -1;

    private void Awake() => textMesh = GetComponent<TextMeshProUGUI>();
    private void OnEnable() => ShowUnavailable();

    private void Update()
    {
        if (textMesh == null) return;
        var currentWorld = World.DefaultGameObjectInjectionWorld;
        if (currentWorld != world)
        {
            ReleaseQuery();
            ShowUnavailable();
            if (currentWorld != null && currentWorld.IsCreated)
            {
                world = currentWorld;
                query = world.EntityManager.CreateEntityQuery(typeof(MatchTimeComponent));
            }
        }

        if (world == null || !world.IsCreated || query.CalculateEntityCount() != 1)
        {
            ShowUnavailable();
            return;
        }

        int seconds = Mathf.FloorToInt(Mathf.Max(0f, query.GetSingleton<MatchTimeComponent>().Value));
        if (seconds == lastSecond) return;
        lastSecond = seconds;
        textMesh.text = string.Format(CultureInfo.CurrentCulture, timeFormat, seconds / 60, seconds % 60);
    }

    private void ShowUnavailable()
    {
        lastSecond = -1;
        if (textMesh != null && textMesh.text != unavailableText) textMesh.text = unavailableText;
    }

    private void ReleaseQuery()
    {
        if (world != null && world.IsCreated) query.Dispose();
        world = null;
    }

    private void OnDisable()
    {
        ReleaseQuery();
        ShowUnavailable();
    }
}

using System;
using TMPro;
using UnityEngine;

public class MatchTimeTextMesh : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        var query = world.EntityManager.CreateEntityQuery(typeof(MatchTimeComponent));
        var matchTime = query.GetSingleton<MatchTimeComponent>();
        TextMeshProUGUI textMesh = GetComponent<TextMeshProUGUI>();
        textMesh.text = "Time: "+Mathf.Round(matchTime.Value).ToString();
    }
}

using UnityEngine;

public class MinimapCam : MonoBehaviour
{
    void Update()
    {
        var maincam = Camera.main;
        transform.position = new Vector3(maincam.transform.position.x, transform.position.y, maincam.transform.position.z);
    }
}

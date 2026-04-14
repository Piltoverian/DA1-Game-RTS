using UnityEngine;

public static class Utility
{
    public static Vector3 ConvertCamToWorld(Vector2 vector)
    {
        Ray ray = Camera.main.ScreenPointToRay(vector);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, LayerMask.GetMask("Ground")))
        {
            return hitInfo.point;
        }
        return Vector3.zero;
    }
}

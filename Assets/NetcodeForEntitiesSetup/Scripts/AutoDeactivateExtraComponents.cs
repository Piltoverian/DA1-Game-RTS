using UnityEngine;

namespace Unity.Multiplayer.Center.NetcodeForEntitiesSetup
{
    public class AutoDeactivateExtraComponents : MonoBehaviour
    {
        void Awake()
        {
            // Deactivate extra AudioListeners
            var audioListeners = FindAllObjects<AudioListener>();
            for (var i = 1; i < audioListeners.Length; i++)
            {
                audioListeners[i].enabled = false;
            }

            // Deactivate extra Directional Lights
            var directionalLights = FindAllObjects<Light>();
            var directionalLightCount = 0;
            foreach (var light in directionalLights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLightCount++;
                    if (directionalLightCount > 1)
                    {
                        light.enabled = false;
                    }
                }
            }
        }

        static T[] FindAllObjects<T>() where T : Object
        {
#if UNITY_6000_4_OR_NEWER
            return Object.FindObjectsByType<T>();
#else
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#endif
        }
    }
}

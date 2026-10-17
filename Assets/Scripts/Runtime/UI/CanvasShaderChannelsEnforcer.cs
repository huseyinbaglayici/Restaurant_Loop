using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantLoop.Runtime.UI
{
    // Enables TexCoord1/Normal/Tangent on every canvas in every loaded scene; TMP outline/underlay flickers without them.
    public static class CanvasShaderChannelsEnforcer
    {
        private const AdditionalCanvasShaderChannels RequiredChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToAllCanvases();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToAllCanvases();
        }

        public static void ApplyToAllCanvases()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].additionalShaderChannels |= RequiredChannels;
                }
            }
        }
    }
}

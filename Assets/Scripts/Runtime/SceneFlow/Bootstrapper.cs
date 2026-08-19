using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantLoop.SceneFlow
{
    public class Bootstrapper : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenuScene";

        private void Start()
        {
            SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Additive);
        }
    }
}

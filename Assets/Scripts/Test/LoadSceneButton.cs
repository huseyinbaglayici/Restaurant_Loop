using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadSceneButton : MonoBehaviour
{
    [Header("Scene Settings")]
    public string sceneName;

    private void Awake()
    {
        // Eğer bu script doğrudan bir UI Button objesine eklenmişse otomatik dinleyici bağlar
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(LoadScene);
        }
    }

    /// <summary>
    /// Inspector'da girilen sceneName isimli sahneyi yükler.
    /// </summary>
    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("[LoadSceneButton] Sahne adı boş bırakılmış!", this);
        }
    }

    /// <summary>
    /// İsteğe bağlı: Buton OnClick event'inden parametre olarak sahne ismi göndermek istersen.
    /// </summary>
    public void LoadSceneByName(string targetScene)
    {
        if (!string.IsNullOrEmpty(targetScene))
        {
            SceneManager.LoadScene(targetScene);
        }
    }
}

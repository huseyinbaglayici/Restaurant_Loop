using UnityEngine;
using UnityEngine.UI;

public class TestPowerUpPickFromBottom : MonoBehaviour
{
    private Button btn;
    private Color originalColor;

    void Start()
    {
        btn = GetComponent<Button>();
        if (btn != null) 
        {
            originalColor = btn.image.color;
            btn.onClick.AddListener(ActivatePowerUp);
        }
    }

    void Update()
    {
        if (btn != null && TestGameManager.Instance != null)
        {
            // Özellik açıksa yeşil yan, kapalıysa eski rengine dön
            btn.image.color = TestGameManager.Instance.allowPickFromBottom ? Color.green : originalColor;
        }
    }

    public void ActivatePowerUp()
    {
        if (TestGameManager.Instance != null && !TestGameManager.Instance.allowPickFromBottom)
        {
            Debug.Log("PowerUp Activated: Pick From Bottom!");
            TestGameManager.Instance.allowPickFromBottom = true;
            TestGameManager.Instance.RefreshAllQueuesClickability();
        }
    }
}

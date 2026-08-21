using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TestPowerUpExtraTray : MonoBehaviour
{
    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(ActivatePowerUp);
    }

    public void ActivatePowerUp()
    {
        Debug.Log("PowerUp Activated: Extra Tray! (Adding more capacity to the tray)");
        if (TestGameManager.Instance != null)
        {
            bool added = TestGameManager.Instance.AddExtraRackSlot();
            if (added)
            {
                StartCoroutine(FlashGreen());
            }
            else
            {
                Debug.Log("Extra Tray power-up can only be used once!");
            }
        }
    }

    private IEnumerator FlashGreen()
    {
        Button btn = GetComponent<Button>();
        if (btn == null) yield break;
        
        Color orig = btn.image.color;
        btn.image.color = Color.green;
        yield return new WaitForSeconds(0.3f);
        btn.image.color = orig;
    }
}

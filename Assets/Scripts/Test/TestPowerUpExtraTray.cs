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
                
            }
            else
            {
                Debug.Log("Extra Tray power-up can only be used once!");
            }
        }
    }

}

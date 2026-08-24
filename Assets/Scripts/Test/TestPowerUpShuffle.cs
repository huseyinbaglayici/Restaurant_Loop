using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TestPowerUpShuffle : MonoBehaviour
{
    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(ActivatePowerUp);
    }

    public void ActivatePowerUp()
    {
        Debug.Log("PowerUp Activated: Shuffle! (Shuffling colors)");
        if (TestGameManager.Instance != null)
        {
            TestGameManager.Instance.ShuffleQueues();
        
        }
    }

}

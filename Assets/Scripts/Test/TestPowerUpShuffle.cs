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
            StartCoroutine(FlashGreen());
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

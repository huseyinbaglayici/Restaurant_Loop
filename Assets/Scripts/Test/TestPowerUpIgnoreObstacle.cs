using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TestPowerUpIgnoreObstacle : MonoBehaviour
{
    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(ActivatePowerUp);
    }

    public void ActivatePowerUp()
    {
        Debug.Log("PowerUp Activated: Ignore Obstacle! (Currently empty)");
        StartCoroutine(FlashGreen());
        // TODO: Implement ignore obstacle logic
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

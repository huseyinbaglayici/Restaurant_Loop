using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TestCustomer : MonoBehaviour
{
    [Header("Customer Settings")]
    public Color orderColor = Color.red;
    public bool isServeable = true;
    public TestCustomer innerCustomer; // Bu müşteri yok olunca öne gelecek olan müşteri
    
    private GameObject canvasObj;
    private Image bubbleImage;
    
    private void Start()
    {
        CreateUI();
        UpdateVisuals();
        
        Renderer rnd = GetComponent<Renderer>();
        if (rnd != null)
        {
            rnd.material.color = isServeable ? orderColor : Color.gray;
        }
    }
    
    private void CreateUI()
    {
        // Dinamik olarak Canvas UI oluşturuyoruz
        canvasObj = new GameObject("OrderCanvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, 2f, 0);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 2f);
        
        GameObject bgObj = new GameObject("Bubble");
        bgObj.transform.SetParent(canvasObj.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bubbleImage = bgObj.AddComponent<Image>();
        bgObj.GetComponent<RectTransform>().sizeDelta = new Vector2(1.5f, 1.5f);
        bubbleImage.color = orderColor;
    }
    
    public void UpdateVisuals()
    {
        if (bubbleImage != null)
        {
            bubbleImage.enabled = isServeable;
            // Aktif olanlar daha büyük gözükür, genel boyutları ufak tuttuk
            transform.localScale = isServeable ? Vector3.one * 0.7f : Vector3.one * 0.5f;
        }
        
        Renderer rnd = GetComponent<Renderer>();
        if (rnd != null)
        {
            rnd.material.color = isServeable ? orderColor : Color.gray;
        }
    }

    public void Serve()
    {
        TestGameManager.Instance.customers.Remove(this);
        TestGameManager.Instance.CheckWin();
        StartCoroutine(JumpAndDestroy());
    }
    
    private IEnumerator JumpAndDestroy()
    {
        float t = 0;
        Vector3 startPos = transform.position;
        // Zıplama Tween benzeri
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            transform.position = startPos + Vector3.up * Mathf.Sin(t / 0.5f * Mathf.PI) * 2f;
            yield return null;
        }
        
        if (innerCustomer != null)
        {
            innerCustomer.isServeable = true;
            innerCustomer.UpdateVisuals();
            TestGameManager.Instance.customers.Add(innerCustomer);
            innerCustomer.StartCoroutine(innerCustomer.MoveTo(startPos)); // İçe doğru kaysın
        }
        
        Destroy(gameObject);
    }
    
    public IEnumerator MoveTo(Vector3 targetPos)
    {
        float t = 0;
        Vector3 startPos = transform.position;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, t / 0.5f);
            yield return null;
        }
        transform.position = targetPos;
    }
}

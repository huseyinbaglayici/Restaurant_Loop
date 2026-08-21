using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TestCustomer : MonoBehaviour
{
    [Header("Customer Settings")]
    public Color orderColor = Color.red;
    public Color orderColor2 = Color.white; // Type 3 için ikinci renk
    public bool hasSecondOrder = false; // Type 3 ise true olur
    public bool isServeable = true;
    public TestCustomer innerCustomer; // Bu müşteri yok olunca öne gelecek olan müşteri
    
    [Header("Painter Data")]
    public int gridX;
    public int gridY;
    public int behaviorType; // 0: Classic, 1: Hidden, 2: Type3
    
    private GameObject canvasObj;
    private Image bubbleImage;
    private Image bubbleImage2; // İkinci baloncuk
    private Text questionMarkText;
    
    private void Start()
    {
        CreateUI();
        UpdateVisuals();
    }
    
    private void CreateUI()
    {
        // Dinamik olarak Canvas UI oluşturuyoruz
        canvasObj = new GameObject("OrderCanvas");
        canvasObj.transform.SetParent(transform);
        // Sağ üste (top right) ve daha aşağıya (Y=1.3) pozisyonlama:
        canvasObj.transform.localPosition = new Vector3(0.6f, 1.3f, 0.6f);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 2f);
        
        GameObject bgObj = new GameObject("Bubble");
        bgObj.transform.SetParent(canvasObj.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bubbleImage = bgObj.AddComponent<Image>();
        // Boyutu 4'te 1'ine indiriyoruz (1.5f yerine 0.375f)
        bgObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0.375f, 0.375f);
        bubbleImage.color = orderColor; // Bubble rengi siparişe göre değişsin
        
#if UNITY_EDITOR
        // Editörde yuvarlak görünmesi için Unity'nin varsayılan Knob sprite'ını atıyoruz
        bubbleImage.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#endif

        // İkinci baloncuk (Eğer Type3 ise)
        if (hasSecondOrder)
        {
            GameObject bgObj2 = new GameObject("Bubble2");
            bgObj2.transform.SetParent(canvasObj.transform);
            bgObj2.transform.localPosition = new Vector3(0.4f, 0, 0); // Biraz sağına
            bubbleImage2 = bgObj2.AddComponent<Image>();
            bgObj2.GetComponent<RectTransform>().sizeDelta = new Vector2(0.375f, 0.375f);
            bubbleImage2.color = orderColor2;
            
#if UNITY_EDITOR
            bubbleImage2.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#endif
            
            // İki baloncuklu müşteri için sağ tarafa ek obje koymuyoruz, sadece baloncuk yetiyor
        }

        // Soru işareti için Text objesi (Şu anlık kapalı olacak)
        GameObject textObj = new GameObject("QuestionMark");
        textObj.transform.SetParent(canvasObj.transform);
        textObj.transform.localPosition = hasSecondOrder ? new Vector3(0.2f, 0, 0) : Vector3.zero; // Ortaya
        questionMarkText = textObj.AddComponent<Text>();
        questionMarkText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        questionMarkText.text = "?";
        questionMarkText.fontSize = 24;
        questionMarkText.alignment = TextAnchor.MiddleCenter;
        questionMarkText.color = Color.black; 
        textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(hasSecondOrder ? 0.7f : 0.375f, 0.375f);
        textObj.transform.localScale = Vector3.one * 0.05f; 
        
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1, -1);
    }
    
    public void UpdateVisuals()
    {
        if (bubbleImage != null)
        {
            // Eğer müşteri "Hidden" türündeyse ve henüz servis edilebilir değilse
            bool isHiddenAndWaiting = (behaviorType == 1 && !isServeable);
            
            // Hiddenlarda şu anlık direkt bubble olmasın (kapalı olsun).
            // Diğerleri için aktif.
            bubbleImage.enabled = isServeable;
            bubbleImage.color = orderColor; // Bubble rengi siparişe göre
            
            if (bubbleImage2 != null)
            {
                bubbleImage2.enabled = (isServeable && hasSecondOrder);
                bubbleImage2.color = orderColor2;
            }
            
            if (questionMarkText != null)
            {
                questionMarkText.enabled = false; // Şimdilik ? da olmasın madem bubble yok
            }
            
            // İçerideki/Dışarıdaki ayrımı yapmadan hepsinin boyutu 1 olsun
            transform.localScale = Vector3.one;
        }
        
        Renderer rnd = GetComponent<Renderer>();
        if (rnd != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            // Beden her zaman renksiz (beyaz), sadece hidden ise gri
            Color targetColor = isServeable ? Color.white : Color.gray;
            block.SetColor("_Color", targetColor);
            block.SetColor("_BaseColor", targetColor);
            rnd.SetPropertyBlock(block);
        }
    }

    public void Serve()
    {
        if (hasSecondOrder)
        {
            // İlk siparişi yedi, ikinciye geç
            hasSecondOrder = false;
            orderColor = orderColor2;
                
            UpdateVisuals();
            
            // Ufak bir zıplama efekti yapalım yediğini belli etmek için
            StartCoroutine(SmallJump());
            return;
        }

        if (TestPainterGameManager.Instance != null && TestPainterGameManager.Instance is TestPainterGameManager painterGM)
        {
            painterGM.OnCustomerServed(this);
        }
        else if (TestGameManager.Instance != null)
        {
            TestGameManager.Instance.customers.Remove(this);
            TestGameManager.Instance.CheckWin();
        }
        
        StartCoroutine(JumpAndDestroy());
    }
    
    private IEnumerator SmallJump()
    {
        float t = 0;
        Vector3 startPos = transform.position;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            transform.position = startPos + Vector3.up * Mathf.Sin(t / 0.3f * Mathf.PI) * 0.5f;
            yield return null;
        }
        transform.position = startPos;
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
    
    private void OnDrawGizmos()
    {
        // Serve mesafesini (4.5f) görmek için Editör'de sarı bir daire çizer
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 4.5f);
    }
}

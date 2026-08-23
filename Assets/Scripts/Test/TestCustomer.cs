using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TestCustomer : MonoBehaviour
{
    [Header("Customer Settings")]
    public Color orderColor = Color.red;
    public Color orderColor2 = Color.white; // Type 3 için ikinci renk
    public TestCustomerData.CustomerColor orderColorEnum;
    public TestCustomerData.CustomerColor orderColor2Enum;
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
        canvasObj.transform.rotation = Quaternion.identity;
        
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

    [Header("Body Customization")]
    public List<Renderer> bodyRenderers = new List<Renderer>();
    public List<Color> originalBodyColors = new List<Color>();

    private int GetGroupIndexForRenderer(Renderer r, List<CustomerMaterialColorGroup> groups)
    {
        if (groups == null || groups.Count == 0) return -1;

        string go = r.gameObject.name.ToLower().Trim();

        // 1. Eyes (Gözler)
        if (go.Contains("eyes") || go.Contains("eye") || go.Contains("goz") || go.Contains("göz"))
        {
            return 5; // 6. Göz Grubu
        }

        // 2. Body (Ten)
        if (go.Contains("body") || go.Contains("skin") || go.Contains("ten") || go.Contains("head"))
        {
            return 0; // 1. Ten Grubu
        }

        // 3. Hair (Saç)
        if (go.Contains("hair") || go.Contains("sac") || go.Contains("saç"))
        {
            return 1; // 2. Saç Grubu
        }

        // 4. T-Shirt (Üst Kıyafet)
        if (go.Contains("tshirt") || go.Contains("t-shirt") || go.Contains("shirt") || go.Contains("top") || go.Contains("ust") || go.Contains("üst"))
        {
            return 2; // 3. T-Shirt Grubu
        }

        // 5. Pants (Pantolon / Alt Kıyafet)
        if (go.Contains("pants") || go.Contains("pant") || go.Contains("bottom") || go.Contains("trouser") || go.Contains("alt"))
        {
            return 3; // 4. Pantolon Grubu
        }

        // 6. Shoes (Ayakkabı)
        if (go.Contains("shoes") || go.Contains("shoe") || go.Contains("feet") || go.Contains("ayak"))
        {
            return 4; // 5. Ayakkabı Grubu
        }

        // 7. TargetMaterial Eşleşmesi (İsimden bulunamazsa fallback)
        Material mat = r.sharedMaterial;
        if (mat != null)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].targetMaterial != null && mat.name.StartsWith(groups[i].targetMaterial.name))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    public void ApplyMaterialGroupColors(List<CustomerMaterialColorGroup> groups)
    {
        if (groups == null || groups.Count == 0) return;

        bodyRenderers.Clear();
        originalBodyColors.Clear();

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (var r in allRenderers)
        {
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (r.GetComponent<TextMesh>() != null) continue;

            bodyRenderers.Add(r);

            int targetGroupIndex = GetGroupIndexForRenderer(r, groups);
            if (targetGroupIndex < 0 || targetGroupIndex >= groups.Count)
            {
                originalBodyColors.Add(r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
                continue;
            }

            CustomerMaterialColorGroup matchedGroup = groups[targetGroupIndex];
            if (matchedGroup == null || matchedGroup.possibleColors == null || matchedGroup.possibleColors.Count == 0)
            {
                originalBodyColors.Add(r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
                continue;
            }

            Color chosen = matchedGroup.possibleColors[Random.Range(0, matchedGroup.possibleColors.Count)];
            originalBodyColors.Add(chosen);

            Material[] mats = r.materials; // Kopyaları al
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m].HasProperty("_Color")) mats[m].SetColor("_Color", chosen);
                if (mats[m].HasProperty("_BaseColor")) mats[m].SetColor("_BaseColor", chosen);
                mats[m].color = chosen;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block, m);
                block.SetColor("_Color", chosen);
                block.SetColor("_BaseColor", chosen);
                r.SetPropertyBlock(block, m);
            }
            r.materials = mats; // Renderer'a geri yaz!
        }
    }
    
    public void UpdateVisuals()
    {
        if (bubbleImage != null)
        {
            // Servis edilebilir ise sipariş rengi, Hidden/beklemede ise renksiz (gri) baloncuk
            bubbleImage.enabled = true;
            bubbleImage.color = isServeable ? orderColor : Color.gray;
            
            if (bubbleImage2 != null)
            {
                bubbleImage2.enabled = hasSecondOrder;
                bubbleImage2.color = isServeable ? orderColor2 : Color.gray;
            }
            
            if (questionMarkText != null)
            {
                questionMarkText.enabled = false;
            }
            
            transform.localScale = Vector3.one;
        }
    }

    public void Serve()
    {
        if (hasSecondOrder)
        {
            // İlk siparişi yedi, ikinciye geç
            hasSecondOrder = false;
            orderColor = orderColor2;
            orderColorEnum = orderColor2Enum;
            UpdateVisuals();
            
            // Ufak bir zıplama efekti yapalım yediğini belli etmek için
            StartCoroutine(SmallJump());
            return;
        }

        if (TestGameManager.Instance != null)
        {
            TestGameManager.Instance.OnCustomerServed(this);
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

[System.Serializable]
public class CustomerMaterialColorGroup
{
    public string groupName = "Material Slot";
    [Tooltip("Hangi materyale uygulanacağı (İsteğe bağlı, boş bırakılırsa slot sırasına göre eşleşir)")]
    public Material targetMaterial;
    [Tooltip("Bu materyal/parça için rastgele seçilebilecek renkler havuzu")]
    public List<Color> possibleColors = new List<Color>();
}

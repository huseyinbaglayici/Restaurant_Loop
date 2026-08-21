using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TestConveyorBelt : MonoBehaviour
{
    public static TestConveyorBelt Instance;
    
    [Header("Conveyor Settings")]
    public int capacity = 5; // Bant üzerinde max 5 obje
    public float width = 8f; // Dikdörtgenin genişliği
    public float depth = 6f; // Dikdörtgenin yüksekliği (derinliği)
    public float speed = 5f; // Saniyede ilerleme hızı (birim)
    
    private float Perimeter => 2f * width + 2f * depth;
    
    private class BeltItem
    {
        public TestFoodStack food;
        public float distance; // Bant üzerindeki 1 boyutlu konumu
    }
    
    private List<BeltItem> items = new List<BeltItem>();
    private Text capacityText;
    
    private void Awake() 
    { 
        Instance = this; 
        // GenerateVisuals(); // Kendi modellerinizi kullandığınız için kapatıldı
        CreateUI();
    }
    
    private void CreateUI()
    {
        // Ekranda sol üst köşede "Bant Kapasitesi: 0/5" gösterecek UI Canvas
        GameObject canvasObj = new GameObject("BeltCapacityCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        GameObject textObj = new GameObject("CapacityText");
        textObj.transform.SetParent(canvasObj.transform);
        capacityText = textObj.AddComponent<Text>();
        capacityText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        capacityText.fontSize = 36;
        capacityText.color = Color.white;
        capacityText.alignment = TextAnchor.UpperLeft;
        
        // Daha okunaklı olması için gölge (Outline) ekliyoruz
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); // Sol Üst
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(7, -6); // İstenen pozisyon
        rt.sizeDelta = new Vector2(300, 100);
    }
    
    private void GenerateVisuals()
    {
        GameObject visuals = new GameObject("BeltVisuals");
        visuals.transform.SetParent(transform);
        visuals.transform.localPosition = Vector3.zero;
        
        float thickness = 1.2f; // Bandın kalınlığı
        float height = 0.1f; // Bandın yüksekliği
        float yOffset = -0.1f; // Yemeklerin biraz altında dursun
        
        // Alt Kenar
        GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottom.name = "BottomEdge";
        bottom.transform.SetParent(visuals.transform);
        bottom.transform.localPosition = new Vector3(0, yOffset, -depth / 2f);
        bottom.transform.localScale = new Vector3(width + thickness, height, thickness);
        
        // Üst Kenar
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TopEdge";
        top.transform.SetParent(visuals.transform);
        top.transform.localPosition = new Vector3(0, yOffset, depth / 2f);
        top.transform.localScale = new Vector3(width + thickness, height, thickness);
        
        // Sol Kenar
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "LeftEdge";
        left.transform.SetParent(visuals.transform);
        left.transform.localPosition = new Vector3(-width / 2f, yOffset, 0);
        left.transform.localScale = new Vector3(thickness, height, depth - thickness);
        
        // Sağ Kenar
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.name = "RightEdge";
        right.transform.SetParent(visuals.transform);
        right.transform.localPosition = new Vector3(width / 2f, yOffset, 0);
        right.transform.localScale = new Vector3(thickness, height, depth - thickness);
        
        // Hepsini koyu gri yap ve Collider'larını sil ki tıklamaları engellemesinler
        foreach (var r in visuals.GetComponentsInChildren<Renderer>())
        {
            r.material.color = new Color(0.15f, 0.15f, 0.15f); // Koyu asfalt/bant rengi
            
            // Eğer Collider'ları varsa raycast'i (tıklamayı) engellememesi için yok et
            Collider col = r.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }
    }
    
    public bool TryAddItem(TestFoodStack food)
    {
        if (items.Count >= capacity) return false;
        
        // Ekrana "Sol Alt Kısımdan" giriş (başlangıç noktası = 0)
        float startDistance = 0f; 
        
        // Başlangıç noktasında çok yakın bir yemek var mı kontrolü
        foreach (var item in items)
        {
            // Mesafe farkı kapasite başına düşen uzunluktan azsa girmesin
            float minSpacing = Perimeter / (capacity + 1);
            if (Mathf.Abs(startDistance - item.distance) < minSpacing && 
                Mathf.Abs(startDistance - item.distance) > -minSpacing + Perimeter) // Wrap kontrolü basitçe
            {
                if (item.distance < minSpacing || item.distance > Perimeter - minSpacing)
                {
                    return false; 
                }
            }
        }
        
        items.Add(new BeltItem { food = food, distance = startDistance });
        return true;
    }
    
    private void Update()
    {
        if (capacityText != null && TestGameManager.Instance != null)
        {
            int rackOccupied = 0;
            if (TestGameManager.Instance.rackItems != null)
            {
                foreach (var item in TestGameManager.Instance.rackItems)
                {
                    if (item != null) rackOccupied++;
                }
            }
            int rackTotal = TestGameManager.Instance.rackSlots != null ? TestGameManager.Instance.rackSlots.Length : 0;
            capacityText.text = $"Rack: {rackOccupied} / {rackTotal}";
        }
        
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            item.distance += speed * Time.deltaTime;
            
            if (item.distance >= Perimeter) 
            {
                // Bir turunu (lap) tamamladı. Rafa inmeye çalışsın.
                bool moved = TestGameManager.Instance.TryMoveToRack(item.food);
                if (moved)
                {
                    item.food.state = TestFoodStack.FoodState.OnRack;
                    items.RemoveAt(i);
                    continue; // Bu item artık bantta değil, bir sonrakine geç.
                }
                else
                {
                    // Raf doluysa mecburen bantta dönmeye devam eder (ya da FAIL tetiklenir)
                    item.distance -= Perimeter;
                }
            }
            
            // Konum hesaplama
            Vector3 pos = GetPositionOnBelt(item.distance);
            
            // Yumuşak geçiş için Lerp (Raftan banda atlarken anında ışınlanmaması için)
            Vector3 targetPos = transform.position + pos;
            item.food.transform.position = Vector3.Lerp(item.food.transform.position, targetPos, Time.deltaTime * 5f);
            
            // Bant hareket ederken müşterilerle eşleşmeyi kontrol et
            CheckServe(item, i);
        }
    }
    
    private Vector3 GetPositionOnBelt(float d)
    {
        Vector3 bl = new Vector3(-width / 2f, 0, -depth / 2f); // Sol Alt (Başlangıç)
        Vector3 br = new Vector3(width / 2f, 0, -depth / 2f);  // Sağ Alt
        Vector3 tr = new Vector3(width / 2f, 0, depth / 2f);   // Sağ Üst
        Vector3 tl = new Vector3(-width / 2f, 0, depth / 2f);  // Sol Üst
        
        // Saat yönünün tersine (Counter-Clockwise) hareket
        // 1. Sol Alttan -> Sağ Alta
        if (d < width)
        {
            return Vector3.Lerp(bl, br, d / width);
        }
        // 2. Sağ Alttan -> Sağ Üste
        else if (d < width + depth)
        {
            return Vector3.Lerp(br, tr, (d - width) / depth);
        }
        // 3. Sağ Üstten -> Sol Üste
        else if (d < 2f * width + depth)
        {
            return Vector3.Lerp(tr, tl, (d - (width + depth)) / width);
        }
        // 4. Sol Üstten -> Sol Alta
        else
        {
            return Vector3.Lerp(tl, bl, (d - (2f * width + depth)) / depth);
        }
    }
    
    private void CheckServe(BeltItem beltItem, int index)
    {
        var customers = TestGameManager.Instance.customers;
        for (int i = customers.Count - 1; i >= 0; i--)
        {
            var customer = customers[i];
            
            // Müşteri servis edilebilir mi ve sipariş rengi aynı mı?
            if (customer.isServeable && customer.orderColor == beltItem.food.foodColor)
            {
                // Orthogonal (Dik) hizada mı diye kontrol et (sadece sağ/sol veya üst/alt hizasındakileri görebilsin)
                float alignThreshold = 0.5f;
                bool isAlignedX = Mathf.Abs(customer.transform.position.x - beltItem.food.transform.position.x) < alignThreshold;
                bool isAlignedZ = Mathf.Abs(customer.transform.position.z - beltItem.food.transform.position.z) < alignThreshold;
                
                if (isAlignedX || isAlignedZ)
                {
                    float dist = Vector3.Distance(customer.transform.position, beltItem.food.transform.position);
                    
                    // --- ENGEL (OBSTACLE) KONTROLÜ ---
                    Vector3 direction = customer.transform.position - beltItem.food.transform.position;
                    RaycastHit[] hits = Physics.RaycastAll(beltItem.food.transform.position, direction.normalized, dist);
                    
                    bool isBlocked = false;
                    foreach (var hit in hits)
                    {
                        if (hit.collider.gameObject == customer.gameObject) continue;
                        
                        if (hit.collider.GetComponent<TestObstacle>() != null)
                        {
                            isBlocked = true;
                            break;
                        }
                        
                        TestCustomer hitCst = hit.collider.GetComponent<TestCustomer>();
                        if (hitCst != null)
                        {
                            isBlocked = true;
                            break;
                        }
                    }
                    
                    if (!isBlocked)
                    {
                        beltItem.food.amount--;
                        beltItem.food.UpdateUI();
                        
                        customer.Serve();
                        
                        if (beltItem.food.amount <= 0)
                        {
                            items.RemoveAt(index);
                            Destroy(beltItem.food.gameObject);
                        }
                        
                        // Bir frame'de aynı food stack ile sadece 1 müşteriye hizmet et, yoksa anında tüm haritayı temizler
                        return;
                    }
                }
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 c = transform.position;
        Vector3 bl = c + new Vector3(-width / 2f, 0, -depth / 2f);
        Vector3 br = c + new Vector3(width / 2f, 0, -depth / 2f);
        Vector3 tr = c + new Vector3(width / 2f, 0, depth / 2f);
        Vector3 tl = c + new Vector3(-width / 2f, 0, depth / 2f);
        
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}

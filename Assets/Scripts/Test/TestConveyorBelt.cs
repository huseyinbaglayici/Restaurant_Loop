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

    [Header("Conveyor Visual & Path from Data")]
    [Tooltip("Düz bant prefabı (Örn: conveyor_normal)")]
    public GameObject conveyorStraightPrefab;
    [Tooltip("Köşe bant prefabı (Örn: conveyor_corner)")]
    public GameObject conveyorCornerPrefab;
    [Tooltip("Hücre dünya boyutu")]
    public float conveyorCellSize = 1f;
    [Tooltip("Bant prefablarının toplanacağı parent Transform")]
    public Transform conveyorVisualsParent;
    
    private List<Vector3> pathWaypoints = new List<Vector3>();
    private List<float> waypointCumulativeDistances = new List<float>();
    private float customPathLength = 0f;
    private bool hasCustomPath = false;
    
    public float Perimeter => hasCustomPath ? customPathLength : (2f * width + 2f * depth);
    
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
#if UNITY_EDITOR
        if (conveyorStraightPrefab == null)
            conveyorStraightPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/conveyor_normal.prefab");
        if (conveyorCornerPrefab == null)
            conveyorCornerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/conveyor_corner.prefab");
#endif
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
        float minSpacing = Perimeter / (capacity + 1) * 0.75f;
        foreach (var item in items)
        {
            if (item == null || item.food == null) continue;
            if (item.distance < minSpacing || item.distance > Perimeter - minSpacing)
            {
                return false; 
            }
        }
        
        items.Add(new BeltItem { food = food, distance = startDistance });
        return true;
    }
    
    public bool TryAddLinkedItems(TestFoodStack f1, TestFoodStack f2)
    {
        if (items.Count + 1 >= capacity) return false; // 2 boş yer lazım
        
        float startDistance = 0f; 
        float spacing = Perimeter / (capacity + 1);
        
        foreach (var item in items)
        {
            if (Mathf.Abs(startDistance - item.distance) < spacing && 
                Mathf.Abs(startDistance - item.distance) > -spacing + Perimeter) 
            {
                if (item.distance < spacing || item.distance > Perimeter - spacing)
                {
                    return false; 
                }
            }
        }
        
        // Birini startDistance'a, diğerini daha yakınına (spacing / 3) koyalım
        float linkedSpacing = spacing / 3f;
        
        items.Add(new BeltItem { food = f1, distance = startDistance });
        items.Add(new BeltItem { food = f2, distance = startDistance - linkedSpacing });
        
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
            if (item == null || item.food == null) continue; // Null check eklendi
            
            item.distance += speed * Time.deltaTime;
            
            if (item.distance >= Perimeter) 
            {
                // Bir turunu (lap) tamamladı. Rafa inmeye çalışsın.
                bool moved = TestGameManager.Instance.TryMoveToRack(item.food);
                if (moved)
                {
                    item.food.state = TestFoodStack.FoodState.OnRack;
                    items[i] = null; // RemoveAt yerine null atıyoruz ki döngü kaymasın
                    TestGameManager.Instance.PlayFoodDropToRackSound();
                    continue; // Bu item artık bantta değil, bir sonrakine geç.
                }
                else
                {
                    // Raf doluysa mecburen bantta dönmeye devam eder
                    item.distance -= Perimeter;
                }
            }
            
            // Konum hesaplama
            Vector3 pos = GetPositionOnBelt(item.distance);
            
            // Yumuşak geçiş için Lerp (Custom yol zaten dünya koordinatındadır)
            Vector3 targetPos = hasCustomPath ? pos : (transform.position + pos);
            item.food.transform.position = Vector3.Lerp(item.food.transform.position, targetPos, Time.deltaTime * 8f);
            
            // Bant hareket ederken müşterilerle eşleşmeyi kontrol et
            CheckServe(item, i);
        }
        
        // Döngü bittikten sonra null olan (silinmiş/rafa inmiş) elemanları temizle
        items.RemoveAll(x => x == null || x.food == null);

        // Bantta yemek varsa loop çal, yoksa durdur
        if (TestGameManager.Instance != null)
        {
            if (items.Count > 0)
            {
                TestGameManager.Instance.StartConveyorLoop();
            }
            else
            {
                TestGameManager.Instance.StopConveyorLoop();
            }
        }
    }
    
    public void BuildPathFromConveyorData(RestaurantLoop.Runtime.Data.ConveyorLevelDataSO data, Vector3 centerPos)
    {
        if (data == null) return;

        // 1. Görsel Bant Parçalarını Oluştur
        SpawnConveyorTiles(data, centerPos);

        // 2. Yol Noktalarını (Waypoints) Çıkar
        pathWaypoints.Clear();
        waypointCumulativeDistances.Clear();
        customPathLength = 0f;

        List<Vector2Int> orderedCoords = null;
        if (data.BakedPath != null && data.BakedPath.OrderedPath != null && data.BakedPath.OrderedPath.Count > 0)
        {
            orderedCoords = data.BakedPath.OrderedPath;
        }
        else if (data.ConveyorCells != null && data.ConveyorCells.Count > 0)
        {
            orderedCoords = new List<Vector2Int>();
            foreach (var c in data.ConveyorCells)
            {
                if (!c.IsGate) orderedCoords.Add(c.Coord);
            }
        }

        if (orderedCoords != null && orderedCoords.Count > 0)
        {
            float offsetX = (data.GridWidth - 1) * conveyorCellSize / 2f;
            float offsetZ = (data.GridHeight - 1) * conveyorCellSize / 2f;

            foreach (var coord in orderedCoords)
            {
                Vector3 wp = centerPos + new Vector3(coord.x * conveyorCellSize - offsetX, 0.4f, coord.y * conveyorCellSize - offsetZ);
                pathWaypoints.Add(wp);
            }

            waypointCumulativeDistances.Add(0f);
            for (int i = 0; i < pathWaypoints.Count; i++)
            {
                Vector3 p1 = pathWaypoints[i];
                Vector3 p2 = pathWaypoints[(i + 1) % pathWaypoints.Count];
                float dist = Vector3.Distance(p1, p2);
                customPathLength += dist;
                waypointCumulativeDistances.Add(customPathLength);
            }

            hasCustomPath = pathWaypoints.Count > 1 && customPathLength > 0.01f;
        }
    }

    public void SpawnConveyorTiles(RestaurantLoop.Runtime.Data.ConveyorLevelDataSO data, Vector3 centerPos)
    {
        if (data == null || data.ConveyorCells == null || data.ConveyorCells.Count == 0) return;

#if UNITY_EDITOR
        if (conveyorStraightPrefab == null)
            conveyorStraightPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/conveyor_normal.prefab");
        if (conveyorCornerPrefab == null)
            conveyorCornerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/conveyor_corner.prefab");
#endif

        if (conveyorVisualsParent == null)
        {
            GameObject parentObj = new GameObject("ConveyorVisuals");
            parentObj.transform.SetParent(transform);
            parentObj.transform.localPosition = Vector3.zero;
            conveyorVisualsParent = parentObj.transform;
        }
        else
        {
            for (int i = conveyorVisualsParent.childCount - 1; i >= 0; i--)
            {
                var child = conveyorVisualsParent.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        float offsetX = (data.GridWidth - 1) * conveyorCellSize / 2f;
        float offsetZ = (data.GridHeight - 1) * conveyorCellSize / 2f;

        // Yol koordinatlarının sırasını ve yönlerini haritalandır
        var orderedPath = data.BakedPath != null ? data.BakedPath.OrderedPath : null;
        var pathIndexByCoord = new Dictionary<Vector2Int, int>();
        if (orderedPath != null)
        {
            for (int i = 0; i < orderedPath.Count; i++)
            {
                pathIndexByCoord[orderedPath[i]] = i;
            }
        }

        foreach (var cell in data.ConveyorCells)
        {
            if (cell.IsGate) continue; // Kapı hücreleri fiziki bant parçası taşımaz

            Vector3 worldPos = centerPos + new Vector3(cell.Coord.x * conveyorCellSize - offsetX, 0f, cell.Coord.y * conveyorCellSize - offsetZ);
            float rotY = RestaurantLoop.Runtime.Conveyor.ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation);

            GameObject prefabToSpawn = cell.Shape == RestaurantLoop.Runtime.Data.ConveyorTileShape.Straight 
                ? conveyorStraightPrefab 
                : conveyorCornerPrefab;

            if (prefabToSpawn != null)
            {
                // Sadece X eksenindeki prefab rotasyonu korunur, Y eksenine grid açısı (rotY) atanır
                float prefabXRot = prefabToSpawn.transform.eulerAngles.x;
                Quaternion worldRot = Quaternion.Euler(prefabXRot, rotY, 0f);
                GameObject piece = Instantiate(prefabToSpawn, worldPos, worldRot, conveyorVisualsParent);
                piece.transform.localScale = prefabToSpawn.transform.localScale;
                piece.name = $"Conveyor_{cell.Coord.x}_{cell.Coord.y}";
                piece.SetActive(true);

                // 1. Düz Bant: Hareket yönünü Level Data yoluna göre belirle ve TestConveyorVisual'a uygula
                if (cell.Shape == RestaurantLoop.Runtime.Data.ConveyorTileShape.Straight)
                {
                    var visual = piece.GetComponent<TestConveyorVisual>() ?? piece.GetComponentInChildren<TestConveyorVisual>();
                    if (visual != null)
                    {
                        bool isForward = true;
                        if (orderedPath != null && pathIndexByCoord.TryGetValue(cell.Coord, out int idx))
                        {
                            Vector2Int nextCoord = orderedPath[(idx + 1) % orderedPath.Count];
                            Vector2Int pathMoveDir = nextCoord - cell.Coord;
                            Vector2Int arrowDir = RotateCoord(Vector2Int.right, cell.Rotation);
                            isForward = (pathMoveDir == arrowDir);
                        }

                        // Yatay (90/270 derece) ve Dikey (0/180 derece) UV eksen farkını düzeltir
                        float axisSign = Mathf.Cos(2f * rotY * Mathf.Deg2Rad);
                        float baseSpeed = Mathf.Abs(visual.scrollSpeedY) > 0.001f ? Mathf.Abs(visual.scrollSpeedY) : 1.0f;
                        visual.scrollSpeedY = (isForward ? 1f : -1f) * axisSign * baseSpeed;
                    }
                }
                // 2. Köşe Bant: Dönüş yönünü (CW/CCW) Level Data yoluna göre ayarla
                else if (cell.Shape == RestaurantLoop.Runtime.Data.ConveyorTileShape.Corner)
                {
                    var cornerVisual = piece.GetComponent<TestConveyorVisualCorner>() ?? piece.GetComponentInChildren<TestConveyorVisualCorner>();
                    if (cornerVisual != null)
                    {
                        cornerVisual.repeatAngle = 360f; // Tam dairesel texture için 360 derece kesintisiz döner
                        if (orderedPath != null && pathIndexByCoord.TryGetValue(cell.Coord, out int idx))
                        {
                            int count = orderedPath.Count;
                            Vector2Int prevCoord = orderedPath[(idx - 1 + count) % count];
                            Vector2Int nextCoord = orderedPath[(idx + 1) % count];
                            Vector2Int dirIn = cell.Coord - prevCoord;
                            Vector2Int dirOut = nextCoord - cell.Coord;
                            int cross = dirIn.x * dirOut.y - dirIn.y * dirOut.x;
                            cornerVisual.isClockwise = (cross < 0);
                        }
                    }
                }
            }
        }
    }

    private static Vector2Int RotateCoord(Vector2Int dir, float degrees)
    {
        int steps = Mathf.RoundToInt(degrees / 90f) % 4;
        if (steps < 0) steps += 4;
        for (int i = 0; i < steps; i++) dir = new Vector2Int(-dir.y, dir.x);
        return dir;
    }
    
    private Vector3 GetPositionOnBelt(float d)
    {
        if (hasCustomPath && pathWaypoints.Count > 1)
        {
            float total = customPathLength;
            if (total <= 0f) return pathWaypoints[0];

            d = Mathf.Repeat(d, total);

            for (int i = 0; i < pathWaypoints.Count; i++)
            {
                float dStart = waypointCumulativeDistances[i];
                float dEnd = waypointCumulativeDistances[i + 1];

                if (d >= dStart && d <= dEnd)
                {
                    float segLen = dEnd - dStart;
                    float t = segLen > 0.0001f ? (d - dStart) / segLen : 0f;
                    Vector3 p1 = pathWaypoints[i];
                    Vector3 p2 = pathWaypoints[(i + 1) % pathWaypoints.Count];
                    return Vector3.Lerp(p1, p2, t);
                }
            }

            return pathWaypoints[0];
        }

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
        if (beltItem == null || beltItem.food == null || beltItem.food.amount <= 0) return;
        if (TestGameManager.Instance == null || TestGameManager.Instance.customers == null) return;
        
        var customers = TestGameManager.Instance.customers;
        for (int i = customers.Count - 1; i >= 0; i--)
        {
            var customer = customers[i];
            if (customer == null || !customer.isServeable) continue;
            
            // Renk eşleştirme: Enum varsa enum ile, yoksa Unity Color ile karşılaştır
            bool colorMatch;
            if (beltItem.food.foodColorEnum != TestCustomerData.CustomerColor.None 
                && customer.orderColorEnum != TestCustomerData.CustomerColor.None)
            {
                colorMatch = (customer.orderColorEnum == beltItem.food.foodColorEnum);
            }
            else
            {
                colorMatch = (customer.orderColor == beltItem.food.foodColor);
            }
            
            if (!colorMatch) continue;

            // Orthogonal (Dik) hizada mı diye kontrol et (X veya Z ekseninde)
            float alignThreshold = 0.8f;
            float maxServeDist = 6.0f;
            
            float diffX = Mathf.Abs(customer.transform.position.x - beltItem.food.transform.position.x);
            float diffZ = Mathf.Abs(customer.transform.position.z - beltItem.food.transform.position.z);
            
            bool isAlignedX = diffX < alignThreshold;
            bool isAlignedZ = diffZ < alignThreshold;
            
            if (isAlignedX || isAlignedZ)
            {
                // Yatay eksende müşteri gövde yüksekliğinde Raycast (zemin veya bant meshlerine çarpmaması için)
                Vector3 rayOrigin = new Vector3(beltItem.food.transform.position.x, customer.transform.position.y + 0.3f, beltItem.food.transform.position.z);
                Vector3 rayTarget = new Vector3(customer.transform.position.x, customer.transform.position.y + 0.3f, customer.transform.position.z);
                Vector3 direction = rayTarget - rayOrigin;
                float dist = direction.magnitude;
                
                if (dist > maxServeDist) continue;
                
                // --- ENGEL (OBSTACLE) VE DİĞER MÜŞTERİ KONTROLÜ ---
                RaycastHit[] hits = Physics.RaycastAll(rayOrigin, direction.normalized, dist);
                
                bool isBlocked = false;
                foreach (var hit in hits)
                {
                    // Kendisi veya çocuk collider'ı ise es geç
                    TestCustomer hitCst = hit.collider.GetComponentInParent<TestCustomer>();
                    if (hitCst == customer) continue;
                    
                    if (hit.collider.GetComponentInParent<TestObstacle>() != null)
                    {
                        isBlocked = true;
                        break;
                    }
                    
                    if (hitCst != null)
                    {
                        isBlocked = true;
                        break;
                    }
                }
                
                if (!isBlocked)
                {
                    Vector3 startPos = beltItem.food.transform.position + Vector3.up * 0.4f;
                    Color fColor = beltItem.food.foodColor;
                    TestCustomer targetCustomer = customer;

                    // Müşterinin sonraki karelerde (0.016s) aynı yemekten tekrar tekrar çekmesini engellemek için anında kapatıyoruz
                    targetCustomer.isServeable = false;

                    // Yemeğin müşteriye doğru uçması, küçülmesi ve varış anında partikül patlaması
                    StartCoroutine(AnimateFoodToCustomer(startPos, targetCustomer, fColor, () =>
                    {
                        if (targetCustomer != null)
                        {
                            targetCustomer.Serve();
                        }
                    }));

                    beltItem.food.amount--;
                    
                    if (beltItem.food.amount <= 0)
                    {
                        if (beltItem.food.linkedStack != null && beltItem.food.linkedStack.amount > 0)
                        {
                            // Sadece hayalete dönüşür (Kutusu ve 0 yazısı kalır)
                            beltItem.food.isGhost = true;
                            beltItem.food.UpdateUI();
                        }
                        else
                        {
                            // İkisi de sıfırlandı veya zaten partneri yok
                            beltItem.food.isGhost = false;
                            beltItem.food.UpdateUI(); // Son kez güncelle
                            
                            // Partneri bul
                            BeltItem partnerBeltItem = null;
                            if (beltItem.food.linkedStack != null)
                            {
                                foreach (var b in items)
                                {
                                    if (b.food == beltItem.food.linkedStack)
                                    {
                                        partnerBeltItem = b;
                                        break;
                                    }
                                }
                            }
                            
                            // Güvenli silme işlemi (Index kaymasını önlemek için null atıyoruz)
                            items[index] = null;
                            Destroy(beltItem.food.gameObject);
                            
                            if (partnerBeltItem != null)
                            {
                                int pIndex = items.IndexOf(partnerBeltItem);
                                if (pIndex != -1)
                                {
                                    items[pIndex] = null; // Sadece null bırakıyoruz, döngü sonu temizlenecek
                                    Destroy(partnerBeltItem.food.gameObject);
                                }
                            }

                            if (TestGameManager.Instance != null)
                            {
                                TestGameManager.Instance.PlayFoodGroupCompletedSound();
                            }
                        }
                    }
                    else
                    {
                        beltItem.food.UpdateUI();
                    }
                    
                    return;
                }
            }
        }
    }

    private System.Collections.IEnumerator AnimateFoodToCustomer(Vector3 startPos, TestCustomer customer, Color foodColor, System.Action onArrive)
    {
        GameObject foodProj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foodProj.name = "FlyingFood";
        foodProj.transform.position = startPos;
        foodProj.transform.localScale = Vector3.one * 0.35f;
        Destroy(foodProj.GetComponent<Collider>());

        Renderer rend = foodProj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = foodColor;
        }

        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 targetPos = customer != null ? customer.transform.position + Vector3.up * 0.85f : startPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (customer != null)
            {
                targetPos = customer.transform.position + Vector3.up * 0.85f;
            }

            // Müşteriye doğru tatlı bir parabolik yayla süzülme
            Vector3 curPos = Vector3.Lerp(startPos, targetPos, t);
            curPos.y += Mathf.Sin(t * Mathf.PI) * 0.6f;
            foodProj.transform.position = curPos;

            // Yaklaştıkça küçülme
            float scale = Mathf.Lerp(0.35f, 0.05f, t * t);
            foodProj.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        // Ağza/Gövdeye varış anında minik renkli kırıntı/yeme partikülü efekti
        CreateBurstParticles(targetPos, foodColor);
        Destroy(foodProj);

        if (TestGameManager.Instance != null)
        {
            TestGameManager.Instance.PlayCustomerEatSound();
        }

        onArrive?.Invoke();
    }

    private void CreateBurstParticles(Vector3 pos, Color color)
    {
        GameObject pObj = new GameObject("FoodEatBurst");
        pObj.transform.position = pos;

        ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = color;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.gravityModifier = 1.2f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 0.3f), new GradientColorKey(color, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = pObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(pObj, 0.5f);
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

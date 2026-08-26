using UnityEngine;
using System.Collections.Generic;

public class TestGameManager : MonoBehaviour
{
    public static TestGameManager Instance;
    
    [Header("Level References")]
    public List<TestCustomer> customers = new List<TestCustomer>();

    [Header("Audio Settings & SFX")]
    [Tooltip("Ses efektlerini çalmak için AudioSource (Boş bırakılırsa otomatik oluşturulur)")]
    public AudioSource audioSource;
    [Tooltip("Bant loop sesini çalmak için AudioSource (Boş bırakılırsa otomatik oluşturulur)")]
    public AudioSource conveyorAudioSource;
    [Tooltip("Stack'teki veya raftaki objelere basınca çalacak ses")]
    public AudioClip foodClickSound;
    [Tooltip("Yemek objesi banda giderken / hareket ederken çalacak ses")]
    public AudioClip foodSendSound;
    [Tooltip("Müşteri yemeği yediğinde çalacak ses")]
    public AudioClip customerEatSound;
    [Tooltip("Bir yemek grubu (stack) tamamen bitip tükendiğinde çalacak ses")]
    public AudioClip foodGroupCompletedSound;
    [Tooltip("Bant üzerinde en az bir yemek varken sürekli (loop) çalacak ses")]
    public AudioClip conveyorLoopSound;
    [Tooltip("Yemek banttan çıkıp rafa düştüğünde çalacak ses")]
    public AudioClip foodDropToRackSound;
    
    [Header("Level Data (Data-Driven - Optional)")]
    [Tooltip("Unified Level Data (Eski veya Birleşik Level Yapısı)")]
    public TestLevelDataSO levelData;

    [Header("Conveyor Grid Level Data (ConveyorLevel_1 gibi hazır level verisi)")]
    [Tooltip("Doğrudan ConveyorLevelDataSO (ConveyorLevel_1 vb.) bağlamak için")]
    public RestaurantLoop.Runtime.Data.ConveyorLevelDataSO conveyorLevelData;
    [Tooltip("Grid hücre aralığı / dünya boyutu")]
    public float conveyorCellSize = 1f;

    [Header("Level UI Display")]
    [Tooltip("Level numarasını gösterecek UI Text (UnityEngine.UI.Text)")]
    public UnityEngine.UI.Text levelText;
    [Tooltip("İsteğe bağlı: TextMeshPro kullanıyorsanız TextMeshProUGUI")]
    public TMPro.TextMeshProUGUI levelTextTMP;

    public RestaurantLoop.Runtime.Data.ConveyorLevelDataSO GetActiveConveyorData()
    {
        if (conveyorLevelData != null) return conveyorLevelData;
        if (levelData != null && levelData.conveyorData != null) return levelData.conveyorData;
        return null;
    }
    
    [Header("Prefabs & Food Visuals (Global)")]
    public GameObject customerPrefab;
    public float foodStackYOffset = 0.5f;
    public FoodPrefabData redFood;
    public FoodPrefabData yellowFood;
    public FoodPrefabData whiteFood;
    public FoodPrefabData blueFood;
    public FoodPrefabData pinkFood;

    [Header("UI Bubble Sprites (5 Colors)")]
    [Tooltip("Kırmızı sipariş baloncuğu görseli")]
    public Sprite redBubbleSprite;
    [Tooltip("Sarı sipariş baloncuğu görseli")]
    public Sprite yellowBubbleSprite;
    [Tooltip("Beyaz sipariş baloncuğu görseli")]
    public Sprite whiteBubbleSprite;
    [Tooltip("Mavi sipariş baloncuğu görseli")]
    public Sprite blueBubbleSprite;
    [Tooltip("Pembe sipariş baloncuğu görseli")]
    public Sprite pinkBubbleSprite;

    [Header("Customer 5-Material Color Palettes")]
    [Tooltip("Customer prefabindeki 5 farklı materyal için ayrı ayrı rastgele renk listeleri")]
    public List<CustomerMaterialColorGroup> customerMaterialColorGroups = GetDefaultMaterialColorGroups();

    public static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
        return Color.white;
    }

    public static List<CustomerMaterialColorGroup> GetDefaultMaterialColorGroups()
    {
        return new List<CustomerMaterialColorGroup>()
        {
            new CustomerMaterialColorGroup 
            { 
                groupName = "1. Ten Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#874B2CFF"),
                    HexToColor("#DE955EFF"),
                    HexToColor("#FDBF99FF")
                } 
            },
            new CustomerMaterialColorGroup 
            { 
                groupName = "2. Saç Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#1D140EFF"),
                    HexToColor("#563C2BFF"),
                    HexToColor("#DA6B2DFF"),
                    HexToColor("#FDBA50FF")
                } 
            },
            new CustomerMaterialColorGroup 
            { 
                groupName = "3. T-Shirt Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#FB7512FF"),
                    HexToColor("#5A873EFF"),
                    HexToColor("#3869AFFF"),
                    HexToColor("#8F8483FF")
                } 
            },
            new CustomerMaterialColorGroup 
            { 
                groupName = "4. Pantolon Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#6F9ED4FF"),
                    HexToColor("#1D1C19FF"),
                    HexToColor("#1B253FFF")
                } 
            },
            new CustomerMaterialColorGroup 
            { 
                groupName = "5. Ayakkabı Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#111111FF"),
                    HexToColor("#6D3920FF")
                } 
            },
            new CustomerMaterialColorGroup 
            { 
                groupName = "6. Göz Renkleri", 
                possibleColors = new List<Color> 
                { 
                    HexToColor("#111111FF"), // Siyah
                    HexToColor("#3869AFFF"), // Mavi
                    HexToColor("#563C2BFF"), // Kahverengi
                    HexToColor("#5A873EFF")  // Yeşil
                } 
            }
        };
    }

    [ContextMenu("Reset Customer Colors To Default Hex")]
    public void ResetCustomerColorsToDefault()
    {
        customerMaterialColorGroups = GetDefaultMaterialColorGroups();
    }

    [Header("Rack Settings")]
    public Transform[] rackSlots; // Raf boşluklarının (slotlarının) pozisyonları
    public TestFoodStack[] rackItems; // Raftaki objeleri takip eder

    [Header("Power Up States")]
    public bool allowPickFromBottom = false;
    
    [Header("Queue Settings")]
    public Transform[] queueSlots; // 3 kuyruk başı pozisyonu
    public List<TestFoodStack>[] queues = new List<TestFoodStack>[3];
    
    [Header("Customer Generation")]
    public Transform customerCenter; // Müşterilerin etrafında dizileceği merkez nokta
    [Tooltip("Müşteriler arasındaki X ekseni boşluğu (aralığı)")]
    public float customerSpacingX = 1.5f;
    [Tooltip("Müşteriler arasındaki Z ekseni boşluğu (aralığı)")]
    public float customerSpacingZ = 1.5f;
    [HideInInspector] public int outerCustomerCount = 18;
    [HideInInspector] public float custRectWidth = 6.5f; // Dış müşteri dikdörtgeninin genişliği
    [HideInInspector] public float custRectDepth = 9f; // Dış müşteri dikdörtgeninin derinliği
    [HideInInspector] public float innerOffset = 1.2f; // İç müşterilerin ne kadar içeride duracağı
    [HideInInspector] public Color[] possibleColors = new Color[] { Color.red, Color.blue, Color.yellow, Color.green };
    
    public Sprite GetBubbleSprite(TestCustomerData.CustomerColor color)
    {
        switch (color)
        {
            case TestCustomerData.CustomerColor.Red: return redBubbleSprite;
            case TestCustomerData.CustomerColor.Yellow: return yellowBubbleSprite;
            case TestCustomerData.CustomerColor.White: return whiteBubbleSprite;
            case TestCustomerData.CustomerColor.Blue: return blueBubbleSprite;
            case TestCustomerData.CustomerColor.Pink: return pinkBubbleSprite;
            default: return null;
        }
    }

    public Sprite GetBubbleSprite(Color color)
    {
        if (color == Color.red) return redBubbleSprite;
        if (color == Color.yellow) return yellowBubbleSprite;
        if (color == Color.white) return whiteBubbleSprite;
        if (color == Color.blue) return blueBubbleSprite;
        if (Mathf.Abs(color.r - 1f) < 0.15f && Mathf.Abs(color.g - 0.4f) < 0.25f) return pinkBubbleSprite;
        return null;
    }

#if UNITY_EDITOR
    private void AutoAssignDefaultBubbleSprites()
    {
        if (redBubbleSprite == null)
            redBubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI Elements/baloncuklar/foods/hamburger_red_bubble.png");
        if (yellowBubbleSprite == null)
            yellowBubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI Elements/baloncuklar/foods/patates_yellow_bubble.png");
        if (whiteBubbleSprite == null)
            whiteBubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI Elements/baloncuklar/foods/et_white_bubble.png");
        if (blueBubbleSprite == null)
            blueBubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI Elements/baloncuklar/foods/içecek_blue_bubble.png");
        if (pinkBubbleSprite == null)
            pinkBubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI Elements/baloncuklar/foods/donut_pembe bubble.png");
    }

    private void Reset()
    {
        AutoAssignDefaultBubbleSprites();
    }
#endif

    protected virtual void Awake() 
    { 
        Instance = this; 

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (rackSlots != null && rackSlots.Length > 0)
        {
            rackItems = new TestFoodStack[rackSlots.Length];
        }

        // Eğer Inspector'da eski/boş renk listesi kalmışsa varsayılan HEX listesini otomatik doldur
        if (customerMaterialColorGroups == null || customerMaterialColorGroups.Count < 6 || 
            customerMaterialColorGroups[0].possibleColors == null || customerMaterialColorGroups[0].possibleColors.Count == 0)
        {
            customerMaterialColorGroups = GetDefaultMaterialColorGroups();
        }

#if UNITY_EDITOR
        if (redBubbleSprite == null || yellowBubbleSprite == null || whiteBubbleSprite == null || blueBubbleSprite == null || pinkBubbleSprite == null)
        {
            AutoAssignDefaultBubbleSprites();
        }
#endif
    }
    
    public static TestCustomerData.CustomerColor FoodTypeToCustomerColor(RestaurantLoop.Runtime.Data.FoodType type)
    {
        switch (type)
        {
            case RestaurantLoop.Runtime.Data.FoodType.Hamburger:
            case RestaurantLoop.Runtime.Data.FoodType.Pizza:
            case RestaurantLoop.Runtime.Data.FoodType.Kebab:
                return TestCustomerData.CustomerColor.Red;
            case RestaurantLoop.Runtime.Data.FoodType.Fries:
            case RestaurantLoop.Runtime.Data.FoodType.Croissant:
                return TestCustomerData.CustomerColor.Yellow;
            case RestaurantLoop.Runtime.Data.FoodType.Meat:
            case RestaurantLoop.Runtime.Data.FoodType.Sushi:
            case RestaurantLoop.Runtime.Data.FoodType.Steak:
                return TestCustomerData.CustomerColor.White;
            case RestaurantLoop.Runtime.Data.FoodType.Drink:
                return TestCustomerData.CustomerColor.Blue;
            case RestaurantLoop.Runtime.Data.FoodType.Donut:
                return TestCustomerData.CustomerColor.Pink;
            default:
                return TestCustomerData.CustomerColor.Red;
        }
    }

    protected virtual void Start()
    {
        UpdateLevelUI();
        InitConveyorVisualsAndPath();
        GenerateCustomers();
        GenerateQueues();
    }

    public void UpdateLevelUI()
    {
        string levelStr = "LEVEL 1";

        var cData = GetActiveConveyorData();
        if (cData != null)
        {
            int lvl = cData.LevelId > 0 ? cData.LevelId : 1;
            levelStr = $"LEVEL {lvl}";
        }
        else if (levelData != null)
        {
            string cleanName = levelData.name.Replace("Level_", "").Replace("Level", "").Replace("level_", "").Replace("level", "").Trim();
            if (string.IsNullOrEmpty(cleanName)) cleanName = "1";
            levelStr = $"LEVEL {cleanName}";
        }

        if (levelText != null) levelText.text = levelStr;
        if (levelTextTMP != null) levelTextTMP.text = levelStr;
    }

    public void InitConveyorVisualsAndPath()
    {
        var activeCData = GetActiveConveyorData();
        if (activeCData != null && TestConveyorBelt.Instance != null)
        {
            Vector3 centerPos = customerCenter != null ? customerCenter.position : transform.position;
            TestConveyorBelt.Instance.conveyorCellSize = conveyorCellSize;
            TestConveyorBelt.Instance.BuildPathFromConveyorData(activeCData, centerPos);
        }
    }
    
    private Vector3 GetPositionOnRect(float distance, float w, float d)
    {
        Vector3 bl = new Vector3(-w / 2f, 0, -d / 2f); // Sol Alt
        Vector3 br = new Vector3(w / 2f, 0, -d / 2f);  // Sağ Alt
        Vector3 tr = new Vector3(w / 2f, 0, d / 2f);   // Sağ Üst
        Vector3 tl = new Vector3(-w / 2f, 0, d / 2f);  // Sol Üst
        
        if (distance <= w)
            return Vector3.Lerp(bl, br, distance / w);
        else if (distance <= w + d)
            return Vector3.Lerp(br, tr, (distance - w) / d);
        else if (distance <= 2f * w + d)
            return Vector3.Lerp(tr, tl, (distance - (w + d)) / w);
        else
            return Vector3.Lerp(tl, bl, (distance - (2f * w + d)) / d);
    }
    
    public Color GetUnityColor(TestCustomerData.CustomerColor color)
    {
        switch (color)
        {
            case TestCustomerData.CustomerColor.Red: return Color.red;
            case TestCustomerData.CustomerColor.Yellow: return Color.yellow;
            case TestCustomerData.CustomerColor.White: return Color.white;
            case TestCustomerData.CustomerColor.Blue: return Color.blue;
            case TestCustomerData.CustomerColor.Pink: return new Color(1f, 0.4f, 0.7f);
            default: return Color.gray;
        }
    }

    private void GenerateCustomers()
    {
        if (customerCenter == null)
        {
            if (customers != null && customers.Count > 0)
            {
                foreach (var c in customers)
                {
                    if (c != null)
                    {
                        c.gameObject.SetActive(true);
                        c.UpdateVisuals();
                    }
                }
            }
            return;
        }
        
        customers.Clear();

        GameObject prefabToUse = customerPrefab != null ? customerPrefab : (levelData != null ? levelData.customerPrefab : null);
        Quaternion customerRotation = Quaternion.Euler(0f, 180f, 0f);

        // 1. Durum: levelData (Unified Level Data) atanmışsa
        if (levelData != null)
        {
            for (int i = customerCenter.childCount - 1; i >= 0; i--)
            {
                Transform child = customerCenter.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            int gridCols = levelData.gridSizeX;
            int gridRows = levelData.gridSizeY;
            float offsetX = (gridCols - 1) * customerSpacingX / 2f;
            float offsetZ = (gridRows - 1) * customerSpacingZ / 2f;

            foreach (var cData in levelData.customers)
            {
                Vector3 pos = new Vector3(cData.gridX * customerSpacingX - offsetX, 0, cData.gridY * customerSpacingZ - offsetZ);
                pos += customerCenter.position;

                GameObject obj;
                TestCustomer cst;

                if (prefabToUse != null)
                {
                    obj = Instantiate(prefabToUse, pos, customerRotation);
                    obj.name = $"Customer_{cData.gridX}_{cData.gridY}";
                    obj.transform.SetParent(customerCenter);
                    obj.SetActive(true);
                    cst = obj.GetComponent<TestCustomer>();
                    if (cst == null) cst = obj.AddComponent<TestCustomer>();
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    obj.name = $"Customer_{cData.gridX}_{cData.gridY}";
                    obj.transform.position = pos;
                    obj.transform.rotation = customerRotation;
                    obj.transform.localScale = Vector3.one * 0.7f;
                    obj.transform.SetParent(customerCenter);
                    obj.SetActive(true);
                    cst = obj.AddComponent<TestCustomer>();
                }

                cst.orderColor = GetUnityColor(cData.orderColor);
                cst.orderColorEnum = cData.orderColor;

                if (cData.behavior == TestCustomerData.CustomerBehavior.Type3)
                {
                    cst.orderColor2 = GetUnityColor(cData.orderColor2);
                    cst.orderColor2Enum = cData.orderColor2;
                    cst.hasSecondOrder = true;
                }

                cst.gridX = cData.gridX;
                cst.gridY = cData.gridY;
                cst.behaviorType = (int)cData.behavior;
                cst.isServeable = (cData.behavior != TestCustomerData.CustomerBehavior.Hidden);

                // Parçalar için rastgele renkleri uygula
                cst.ApplyMaterialGroupColors(customerMaterialColorGroups);
                cst.UpdateVisuals();

                customers.Add(cst);
            }
            return;
        }

        // 2. Durum: Doğrudan ConveyorLevelDataSO (ConveyorLevel_1 gibi) atanmışsa
        var activeCData = GetActiveConveyorData();
        if (activeCData != null && activeCData.Customers != null && activeCData.Customers.Count > 0)
        {
            for (int i = customerCenter.childCount - 1; i >= 0; i--)
            {
                Transform child = customerCenter.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            int gridCols = activeCData.GridWidth;
            int gridRows = activeCData.GridHeight;
            float offsetX = (gridCols - 1) * conveyorCellSize / 2f;
            float offsetZ = (gridRows - 1) * conveyorCellSize / 2f;

            foreach (var cEntity in activeCData.Customers)
            {
                Vector3 pos = new Vector3(cEntity.Coord.x * conveyorCellSize - offsetX, 0, cEntity.Coord.y * conveyorCellSize - offsetZ);
                pos += customerCenter.position;

                GameObject obj;
                TestCustomer cst;

                if (prefabToUse != null)
                {
                    obj = Instantiate(prefabToUse, pos, customerRotation);
                    obj.name = $"Customer_{cEntity.Coord.x}_{cEntity.Coord.y}";
                    obj.transform.SetParent(customerCenter);
                    obj.SetActive(true);
                    cst = obj.GetComponent<TestCustomer>();
                    if (cst == null) cst = obj.AddComponent<TestCustomer>();
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    obj.name = $"Customer_{cEntity.Coord.x}_{cEntity.Coord.y}";
                    obj.transform.position = pos;
                    obj.transform.rotation = customerRotation;
                    obj.transform.localScale = Vector3.one * 0.7f;
                    obj.transform.SetParent(customerCenter);
                    obj.SetActive(true);
                    cst = obj.AddComponent<TestCustomer>();
                }

                var colorEnum1 = FoodTypeToCustomerColor(cEntity.Order.Type);
                cst.orderColor = GetUnityColor(colorEnum1);
                cst.orderColorEnum = colorEnum1;

                if (cEntity.HasSecondOrder)
                {
                    var colorEnum2 = FoodTypeToCustomerColor(cEntity.SecondOrder.Type);
                    cst.orderColor2 = GetUnityColor(colorEnum2);
                    cst.orderColor2Enum = colorEnum2;
                    cst.hasSecondOrder = true;
                }

                cst.gridX = cEntity.Coord.x;
                cst.gridY = cEntity.Coord.y;
                cst.behaviorType = cEntity.IsHidden ? 1 : (cEntity.HasSecondOrder ? 2 : 0);
                cst.isServeable = !cEntity.IsHidden;

                cst.ApplyMaterialGroupColors(customerMaterialColorGroups);
                cst.UpdateVisuals();

                customers.Add(cst);
            }
            return;
        }

        // Fallback: Klasik Rastgele Müşteri Üretimi
        float outerPerimeter = 2f * custRectWidth + 2f * custRectDepth;
        float innerRectWidth = custRectWidth - innerOffset * 2f;
        float innerRectDepth = custRectDepth - innerOffset * 2f;
        float innerPerimeter = 2f * innerRectWidth + 2f * innerRectDepth;
        
        float outerStep = outerPerimeter / outerCustomerCount;
        float innerStep = innerPerimeter / outerCustomerCount;
        
        for (int i = 0; i < outerCustomerCount; i++)
        {
            Vector3 outerPos = customerCenter.position + GetPositionOnRect(i * outerStep, custRectWidth, custRectDepth);
            
            // Müşterinin TAM İÇİNDE doğmaması için ufak bir OverlapSphere kontrolü (Sadece tam üstündeyse atla)
            bool isInsideObstacle = false;
            Collider[] hits = Physics.OverlapSphere(outerPos, 0.1f);
            foreach (var hit in hits)
            {
                if (hit.GetComponent<TestObstacle>() != null)
                {
                    isInsideObstacle = true;
                    break;
                }
            }
            
            if (isInsideObstacle)
            {
                continue; // Engelin tam içine denk geliyorsa doğurma
            }
            
            // İç Müşteri (Gri)
            Vector3 innerPos = customerCenter.position + GetPositionOnRect(i * innerStep, innerRectWidth, innerRectDepth);
            GameObject innerObj = prefabToUse != null ? Instantiate(prefabToUse, innerPos, customerRotation) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            innerObj.name = "InnerCustomer_" + i;
            innerObj.transform.position = innerPos;
            innerObj.transform.rotation = customerRotation;
            if (prefabToUse == null) innerObj.transform.localScale = Vector3.one * 0.5f;
            innerObj.transform.SetParent(customerCenter);
            innerObj.SetActive(true);
            TestCustomer innerCst = innerObj.GetComponent<TestCustomer>() ?? innerObj.AddComponent<TestCustomer>();
            innerCst.isServeable = false;
            innerCst.orderColor = possibleColors[Random.Range(0, possibleColors.Length)];
            innerCst.ApplyMaterialGroupColors(customerMaterialColorGroups);
            innerCst.UpdateVisuals();
            
            // Dış Müşteri (Renkli)
            GameObject outerObj = prefabToUse != null ? Instantiate(prefabToUse, outerPos, customerRotation) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            outerObj.name = "OuterCustomer_" + i;
            outerObj.transform.position = outerPos;
            outerObj.transform.rotation = customerRotation;
            if (prefabToUse == null) outerObj.transform.localScale = Vector3.one * 0.5f;
            outerObj.transform.SetParent(customerCenter);
            outerObj.SetActive(true);
            TestCustomer outerCst = outerObj.GetComponent<TestCustomer>() ?? outerObj.AddComponent<TestCustomer>();
            outerCst.isServeable = true;
            outerCst.orderColor = possibleColors[Random.Range(0, possibleColors.Length)];
            outerCst.ApplyMaterialGroupColors(customerMaterialColorGroups);
            outerCst.UpdateVisuals();
            
            // Bağlantı
            outerCst.innerCustomer = innerCst;
            
            // Listeye ekle
            customers.Add(outerCst);
        }
    }
    
    private void GenerateQueues()
    {
        if (queueSlots == null || queueSlots.Length < 3) return;
        
        for(int i = 0; i < 3; i++) {
            queues[i] = new List<TestFoodStack>();
        }

        // Eğer levelData'da kuyruklar tanımlıysa ve geçerliyse
        bool hasValidQueueData = false;
        if (levelData != null)
        {
            List<FoodChunk>[] checkQueues = { levelData.queue1, levelData.queue2, levelData.queue3 };
            foreach (var qList in checkQueues)
            {
                if (qList != null)
                {
                    foreach (var c in qList)
                    {
                        if (c != null && c.color != TestCustomerData.CustomerColor.None)
                        {
                            hasValidQueueData = true;
                            break;
                        }
                    }
                }
                if (hasValidQueueData) break;
            }
        }

        var activeCData = GetActiveConveyorData();
        bool hasConveyorQueues = false;
        if (levelData == null && activeCData != null && activeCData.Queues != null && activeCData.Queues.Count > 0)
        {
            foreach (var qLane in activeCData.Queues)
            {
                if (qLane != null && qLane.Entries != null && qLane.Entries.Count > 0)
                {
                    hasConveyorQueues = true;
                    break;
                }
            }
        }

        if (hasValidQueueData)
        {
            Dictionary<int, TestFoodStack> unlinkedStacks = new Dictionary<int, TestFoodStack>();
            List<FoodChunk>[] levelDataQueues = { levelData.queue1, levelData.queue2, levelData.queue3 };

            for (int q = 0; q < 3; q++)
            {
                List<FoodChunk> currentQueueData = levelDataQueues[q];
                if (currentQueueData == null) continue;

                foreach (var chunk in currentQueueData)
                {
                    if (chunk == null || chunk.color == TestCustomerData.CustomerColor.None) continue;

                    GameObject baseObj = new GameObject("FoodStack_" + chunk.color.ToString());
                    baseObj.transform.localScale = Vector3.one;
                    baseObj.AddComponent<BoxCollider>(); 
                    
                    TestFoodStack food = baseObj.AddComponent<TestFoodStack>();
                    food.foodColor = GetUnityColor(chunk.color);
                    food.foodColorEnum = chunk.color;
                    food.amount = chunk.amount;
                    food.state = TestFoodStack.FoodState.InQueue;
                    food.queueIndex = q;
                    food.isClickable = false;
                    
                    if (chunk.linkID > 0)
                    {
                        if (unlinkedStacks.ContainsKey(chunk.linkID))
                        {
                            TestFoodStack partner = unlinkedStacks[chunk.linkID];
                            if (partner != null && partner.queueIndex != q)
                            {
                                food.linkedStack = partner;
                                partner.linkedStack = food;
                            }
                            unlinkedStacks.Remove(chunk.linkID);
                        }
                        else
                        {
                            unlinkedStacks[chunk.linkID] = food;
                        }
                    }
                    
                    queues[q].Add(food);
                }
            }
        }
        else if (hasConveyorQueues)
        {
            Dictionary<int, TestFoodStack> unlinkedStacks = new Dictionary<int, TestFoodStack>();
            for (int q = 0; q < Mathf.Min(3, activeCData.Queues.Count); q++)
            {
                var lane = activeCData.Queues[q];
                if (lane == null || lane.Entries == null) continue;

                foreach (var entry in lane.Entries)
                {
                    var cEnum = FoodTypeToCustomerColor(entry.Type);
                    GameObject baseObj = new GameObject("FoodStack_" + cEnum.ToString());
                    baseObj.transform.localScale = Vector3.one;
                    baseObj.AddComponent<BoxCollider>();
                    
                    TestFoodStack food = baseObj.AddComponent<TestFoodStack>();
                    food.foodColor = GetUnityColor(cEnum);
                    food.foodColorEnum = cEnum;
                    food.amount = entry.Amount;
                    food.state = TestFoodStack.FoodState.InQueue;
                    food.queueIndex = q;
                    food.isClickable = false;

                    if (entry.LinkId > 0)
                    {
                        if (unlinkedStacks.ContainsKey(entry.LinkId))
                        {
                            TestFoodStack partner = unlinkedStacks[entry.LinkId];
                            if (partner != null && partner.queueIndex != q)
                            {
                                food.linkedStack = partner;
                                partner.linkedStack = food;
                            }
                            unlinkedStacks.Remove(entry.LinkId);
                        }
                        else
                        {
                            unlinkedStacks[entry.LinkId] = food;
                        }
                    }

                    queues[q].Add(food);
                }
            }
        }
        else
        {
            // 1. Conservation of Mass: Count all generated customer colors
            Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
            TestCustomer[] allCustomers = customerCenter != null ? customerCenter.GetComponentsInChildren<TestCustomer>() : customers.ToArray();
            foreach(var c in allCustomers)
            {
                if (colorCounts.ContainsKey(c.orderColor)) colorCounts[c.orderColor]++;
                else colorCounts[c.orderColor] = 1;
                
                // Eğer ikinci bir siparişi varsa, onu da yemek sayısına dahil et!
                if (c.hasSecondOrder)
                {
                    if (colorCounts.ContainsKey(c.orderColor2)) colorCounts[c.orderColor2]++;
                    else colorCounts[c.orderColor2] = 1;
                }
            }

            // 2. Chunk into sizes
            List<TestFoodStack> allGeneratedStacks = new List<TestFoodStack>();
            foreach(var kvp in colorCounts)
            {
                int remaining = kvp.Value;
                while(remaining > 0)
                {
                    int chunkSize = Random.Range(3, 6); 
                    if (remaining <= 5) {
                        chunkSize = remaining; 
                    } else if (remaining - chunkSize < 3) {
                        chunkSize = remaining - 3;
                        if (chunkSize < 3) chunkSize = remaining; 
                    }
                    
                    remaining -= chunkSize;

                    GameObject baseObj = new GameObject("FoodStack_" + kvp.Key.ToString());
                    baseObj.transform.localScale = Vector3.one;
                    baseObj.AddComponent<BoxCollider>(); // Collider eklendi, boyutu script içinden ayarlanacak
                    
                    TestFoodStack food = baseObj.AddComponent<TestFoodStack>();
                    food.foodColor = kvp.Key;
                    food.amount = chunkSize;
                    food.state = TestFoodStack.FoodState.InQueue;
                    
                    allGeneratedStacks.Add(food);
                }
            }

            // 3. Shuffle and Distribute
            ShuffleList(allGeneratedStacks);
            
            for (int i = 0; i < allGeneratedStacks.Count; i++)
            {
                int qIndex = i % 3; 
                queues[qIndex].Add(allGeneratedStacks[i]);
                allGeneratedStacks[i].queueIndex = qIndex;
                allGeneratedStacks[i].isClickable = false;
            }
        }

        // 4. Place visually
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < queues[i].Count; j++)
            {
                TestFoodStack food = queues[i][j];
                // Top-down kamera için Y eksenini sabit tutup, Z ekseninde ekrana doğru (aşağıya) dizelim
                // Böylece j=0 (tıklanabilir olan ilk eleman) bantlara en yakın (en üstte) kalır.
                food.transform.position = queueSlots[i].position + new Vector3(0, 0, -j * 1.2f);
                
                if (j == 0 || allowPickFromBottom)
                {
                    food.isClickable = true;
                }
            }
        }
    }

    protected void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    
    public void PopQueue(int queueIndex, TestFoodStack food)
    {
        if (queues == null) return;
        if (queueIndex >= 0 && queueIndex < queues.Length)
        {
            if (queues[queueIndex] != null && queues[queueIndex].Contains(food))
            {
                queues[queueIndex].Remove(food);
                
                // Kalanları öne kaydır
                for (int i = 0; i < queues[queueIndex].Count; i++)
                {
                    TestFoodStack nextFood = queues[queueIndex][i];
                    if (nextFood == null) continue;
                    nextFood.queueIndex = queueIndex;
                    if (queueSlots != null && queueIndex < queueSlots.Length && queueSlots[queueIndex] != null)
                    {
                        Vector3 targetPos = queueSlots[queueIndex].position + new Vector3(0, 0, -i * 1.2f);
                        nextFood.StopAllCoroutines();
                        nextFood.StartCoroutine(nextFood.MoveTo(targetPos));
                    }
                    nextFood.isClickable = (i == 0 || allowPickFromBottom);
                }
                
                // Power-up tek kullanımlıksa gücü kapat ve tüm kuyrukların tıklanabilirliğini güncelle
                if (allowPickFromBottom)
                {
                    allowPickFromBottom = false;
                    RefreshAllQueuesClickability();
                }
            }
        }
    }

    public void RefreshAllQueuesClickability()
    {
        if (queues == null) return;
        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i] == null) continue;
            for (int j = 0; j < queues[i].Count; j++)
            {
                queues[i][j].isClickable = (j == 0 || allowPickFromBottom);
            }
        }
    }
    
    public bool TryMoveToRack(TestFoodStack food)
    {
        for (int i = 0; i < rackSlots.Length; i++)
        {
            if (rackItems[i] == null)
            {
                rackItems[i] = food;
                food.transform.position = rackSlots[i].position;
                CheckFail();
                return true;
            }
        }
        return false;
    }
    
    public void RemoveFromRack(TestFoodStack food)
    {
        for (int i = 0; i < rackItems.Length; i++)
        {
            if (rackItems[i] == food)
            {
                rackItems[i] = null;
                break;
            }
        }
    }
    
    public void OnCustomerServed(TestCustomer servedCustomer)
    {
        customers.Remove(servedCustomer);
        
        // Eğer data-driven modundaysak ve hidden komşu varsa aç
        if (levelData != null)
        {
            foreach (var c in customers)
            {
                if (c != null && c.behaviorType == 1 && !c.isServeable) // 1 = Hidden
                {
                    if (Mathf.Abs(c.gridX - servedCustomer.gridX) + Mathf.Abs(c.gridY - servedCustomer.gridY) == 1)
                    {
                        c.isServeable = true;
                        c.UpdateVisuals();
                    }
                }
            }
        }
        
        CheckWin();
    }

    public void CheckWin()
    {
        if (customers.Count == 0)
        {
            Debug.Log("WIN! - Bütün müşteriler servis edildi.");
        }
    }
    
    public void CheckFail()
    {
        // Fail senaryosu: Rafta hiç yer yoksa
        bool rackFull = true;
        foreach (var item in rackItems)
        {
            if (item == null) 
            {
                rackFull = false;
                break;
            }
        }
        
        if (rackFull)
        {
            // Gerçek bir senaryoda bantta dönenlerin hiçbiriyle uyuşmuyorsa fail verilir,
            // Prototip olduğu için basitçe raf dolunca FAIL yazdırıyoruz.
            Debug.Log("FAIL! - Raf doldu, hamle kalmadı.");
        }
    }

    public bool hasAddedExtraRackSlot = false;

    public bool AddExtraRackSlot()
    {
        if (hasAddedExtraRackSlot) return false;
        if (rackSlots == null || rackSlots.Length == 0) return false;

        hasAddedExtraRackSlot = true;
        Transform lastSlot = rackSlots[rackSlots.Length - 1];
        GameObject newSlotObj = Instantiate(lastSlot.gameObject, lastSlot.parent);
        newSlotObj.name = "RackSlot_" + rackSlots.Length;
        
        Vector3 delta = Vector3.right * 1.2f; // Default aralık
        if (rackSlots.Length > 1)
        {
            delta = rackSlots[rackSlots.Length - 1].position - rackSlots[rackSlots.Length - 2].position;
        }
        
        newSlotObj.transform.position = lastSlot.position + delta;

        Transform[] newSlots = new Transform[rackSlots.Length + 1];
        for (int i = 0; i < rackSlots.Length; i++) newSlots[i] = rackSlots[i];
        newSlots[newSlots.Length - 1] = newSlotObj.transform;
        rackSlots = newSlots;

        TestFoodStack[] newItems = new TestFoodStack[rackItems.Length + 1];
        for (int i = 0; i < rackItems.Length; i++) newItems[i] = rackItems[i];
        rackItems = newItems;
        
        Debug.Log("Added extra tray slot! Total slots: " + rackSlots.Length);
        return true;
    }

    public void ShuffleQueues()
    {
        if (queues == null) return;
        List<TestFoodStack> allInQueue = new List<TestFoodStack>();
        foreach (var q in queues)
        {
            if (q != null)
            {
                allInQueue.AddRange(q);
                q.Clear();
            }
        }

        ShuffleList(allInQueue);

        for (int i = 0; i < allInQueue.Count; i++)
        {
            int qIndex = i % queues.Length;
            if (queues[qIndex] != null)
            {
                queues[qIndex].Add(allInQueue[i]);
            }
            allInQueue[i].queueIndex = qIndex;
            allInQueue[i].isClickable = false;
        }

        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i] == null) continue;
            for (int j = 0; j < queues[i].Count; j++)
            {
                TestFoodStack food = queues[i][j];
                if (queueSlots != null && i < queueSlots.Length && queueSlots[i] != null)
                {
                    food.transform.position = queueSlots[i].position + new Vector3(0, 0, -j * 1.2f);
                }
                
                if (j == 0 || allowPickFromBottom)
                {
                    food.isClickable = true;
                }
            }
        }
    }

    #region Audio Playback Methods
    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }
        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayFoodClickSound() => PlaySound(foodClickSound);
    public void PlayFoodSendSound() => PlaySound(foodSendSound);
    public void PlayCustomerEatSound() => PlaySound(customerEatSound);
    public void PlayFoodGroupCompletedSound() => PlaySound(foodGroupCompletedSound);
    public void PlayFoodDropToRackSound() => PlaySound(foodDropToRackSound);

    public void StartConveyorLoop()
    {
        if (conveyorLoopSound == null) return;
        if (conveyorAudioSource == null)
        {
            conveyorAudioSource = gameObject.AddComponent<AudioSource>();
            conveyorAudioSource.loop = true;
            conveyorAudioSource.playOnAwake = false;
        }

        if (!conveyorAudioSource.isPlaying || conveyorAudioSource.clip != conveyorLoopSound)
        {
            conveyorAudioSource.clip = conveyorLoopSound;
            conveyorAudioSource.loop = true;
            conveyorAudioSource.Play();
        }
    }

    public void StopConveyorLoop()
    {
        if (conveyorAudioSource != null && conveyorAudioSource.isPlaying)
        {
            conveyorAudioSource.Stop();
        }
    }
    #endregion
}

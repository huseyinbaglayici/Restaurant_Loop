using UnityEngine;
using System.Collections.Generic;

public class TestGameManager : MonoBehaviour
{
    public static TestGameManager Instance;
    
    [Header("Level References")]
    public List<TestCustomer> customers = new List<TestCustomer>();
    
    [Header("Rack Settings")]
    public Transform[] rackSlots; // Raf boşluklarının (slotlarının) pozisyonları
    private TestFoodStack[] rackItems; // Raftaki objeleri takip eder
    
    [Header("Queue Settings")]
    public Transform[] queueSlots; // 3 kuyruk başı pozisyonu
    public List<TestFoodStack>[] queues = new List<TestFoodStack>[3];
    
    [Header("Customer Generation")]
    public Transform customerCenter; // Müşterilerin etrafında dizileceği merkez nokta
    public int outerCustomerCount = 18;
    public float custRectWidth = 6.5f; // Dış müşteri dikdörtgeninin genişliği
    public float custRectDepth = 9f; // Dış müşteri dikdörtgeninin derinliği
    public float innerOffset = 1.2f; // İç müşterilerin ne kadar içeride duracağı
    public Color[] possibleColors = new Color[] { Color.red, Color.blue, Color.yellow, Color.green };
    
    private void Awake() 
    { 
        Instance = this; 
        if (rackSlots != null && rackSlots.Length > 0)
        {
            rackItems = new TestFoodStack[rackSlots.Length];
        }
    }
    
    private void Start()
    {
        GenerateCustomers();
        GenerateQueues();
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
    
    private void GenerateCustomers()
    {
        if (customerCenter == null) return;
        
        customers.Clear();
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
            GameObject innerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            innerObj.name = "InnerCustomer_" + i;
            innerObj.transform.position = innerPos;
            innerObj.transform.localScale = Vector3.one * 0.5f; // Boyutu küçültüldü
            innerObj.transform.SetParent(customerCenter);
            TestCustomer innerCst = innerObj.AddComponent<TestCustomer>();
            innerCst.isServeable = false;
            innerCst.orderColor = possibleColors[Random.Range(0, possibleColors.Length)];
            
            // Dış Müşteri (Renkli)
            GameObject outerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            outerObj.name = "OuterCustomer_" + i;
            outerObj.transform.position = outerPos;
            outerObj.transform.localScale = Vector3.one * 0.5f; // Boyutu küçültüldü
            outerObj.transform.SetParent(customerCenter);
            TestCustomer outerCst = outerObj.AddComponent<TestCustomer>();
            outerCst.isServeable = true;
            outerCst.orderColor = possibleColors[Random.Range(0, possibleColors.Length)];
            
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

        // 1. Conservation of Mass: Count all generated customer colors
        Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
        TestCustomer[] allCustomers = customerCenter.GetComponentsInChildren<TestCustomer>();
        foreach(var c in allCustomers)
        {
            if (colorCounts.ContainsKey(c.orderColor)) colorCounts[c.orderColor]++;
            else colorCounts[c.orderColor] = 1;
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
                baseObj.transform.localScale = Vector3.one * 0.8f;
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

        // 4. Place visually
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < queues[i].Count; j++)
            {
                TestFoodStack food = queues[i][j];
                // Top-down kamera için Y eksenini sabit tutup, Z ekseninde ekrana doğru (aşağıya) dizelim
                // Böylece j=0 (tıklanabilir olan ilk eleman) bantlara en yakın (en üstte) kalır.
                food.transform.position = queueSlots[i].position + new Vector3(0, 0, -j * 1.2f);
                
                if (j == 0)
                {
                    food.isClickable = true;
                }
            }
        }
    }

    private void ShuffleList<T>(List<T> list)
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
        if (queueIndex >= 0 && queueIndex < 3)
        {
            if (queues[queueIndex].Count > 0 && queues[queueIndex][0] == food)
            {
                queues[queueIndex].RemoveAt(0);
                
                // Kalanları öne kaydır
                for (int i = 0; i < queues[queueIndex].Count; i++)
                {
                    TestFoodStack nextFood = queues[queueIndex][i];
                    nextFood.transform.position = queueSlots[queueIndex].position + new Vector3(0, 0, -i * 1.2f);
                    
                    if (i == 0)
                    {
                        nextFood.isClickable = true;
                    }
                }
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
}

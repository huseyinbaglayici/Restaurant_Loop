using UnityEngine;
using System.Collections.Generic;

public class TestPainterGameManager : TestGameManager
{
    [Header("Level Data (Unified Data-Driven)")]
    public TestLevelDataSO levelData;

    public TestCustomer[,] customerGrid;
    private int gridCols = 0;
    private int gridRows = 0;
    private float gridSpacing = 1.5f;

    protected override void Start()
    {
        FetchPaintedCustomers();
        GeneratePainterQueues();
    }
    
    private void FetchPaintedCustomers()
    {
        if (levelData == null)
        {
            Debug.LogError("Level Data is missing! Please assign a TestLevelDataSO.");
            return;
        }

        customers.Clear();
        gridCols = levelData.gridSizeX;
        gridRows = levelData.gridSizeY;
        customerGrid = new TestCustomer[gridCols, gridRows];
        
        float offsetX = (gridCols - 1) * gridSpacing / 2f;
        float offsetY = (gridRows - 1) * gridSpacing / 2f;

        foreach (var cData in levelData.customers)
        {
            Vector3 pos = new Vector3(cData.gridX * gridSpacing - offsetX, 0, cData.gridY * gridSpacing - offsetY);
            if (customerCenter != null) pos += customerCenter.position;

            GameObject obj;
            TestCustomer cst;
            
            if (levelData.customerPrefab != null)
            {
                obj = Instantiate(levelData.customerPrefab, pos, Quaternion.identity);
                obj.name = $"Customer_{cData.gridX}_{cData.gridY}";
                if (customerCenter != null) obj.transform.SetParent(customerCenter);
                cst = obj.GetComponent<TestCustomer>();
                if (cst == null) cst = obj.AddComponent<TestCustomer>();
            }
            else
            {
                Debug.LogWarning("Customer Prefab atanmamış! TestLevelDataSO üzerinden prefab atayın. Kapsül oluşturuluyor.");
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = $"Customer_{cData.gridX}_{cData.gridY}";
                obj.transform.position = pos;
                obj.transform.localScale = Vector3.one * 0.7f;
                if (customerCenter != null) obj.transform.SetParent(customerCenter);
                cst = obj.AddComponent<TestCustomer>();
            }

            cst.orderColor = GetUnityColor(cData.orderColor);
            
            if (cData.behavior == TestCustomerData.CustomerBehavior.Type3)
            {
                cst.orderColor2 = GetUnityColor(cData.orderColor2);
                cst.hasSecondOrder = true;
            }
            
            cst.gridX = cData.gridX;
            cst.gridY = cData.gridY;
            cst.behaviorType = (int)cData.behavior;
            cst.isServeable = (cData.behavior != TestCustomerData.CustomerBehavior.Hidden); 
            
            if (levelData.customerPrefab == null)
            {
                Renderer rnd = obj.GetComponent<Renderer>();
                if (rnd != null)
                {
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    block.SetColor("_Color", Color.white);
                    block.SetColor("_BaseColor", Color.white); 
                    rnd.SetPropertyBlock(block);
                }
            }

            customerGrid[cData.gridX, cData.gridY] = cst;
            customers.Add(cst);
        }
        
        Debug.Log($"Spawned {customers.Count} customers from LevelData. Grid size: {gridCols}x{gridRows}");
    }
    
    private Color GetUnityColor(TestCustomerData.CustomerColor color)
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
    
    public void OnCustomerServed(TestCustomer servedCustomer)
    {
        int x = servedCustomer.gridX;
        int y = servedCustomer.gridY;
        
        // Remove from grid
        customerGrid[x, y] = null;
        customers.Remove(servedCustomer);
        
        // Sadece servis edilen müşterinin sağ/sol/üst/alt komşularına haber ver (Eğer Hidden iseler açılsınlar)
        ExposeNeighborIfHidden(x - 1, y);
        ExposeNeighborIfHidden(x + 1, y);
        ExposeNeighborIfHidden(x, y - 1);
        ExposeNeighborIfHidden(x, y + 1);
        
        CheckWin();
    }

    private void ExposeNeighborIfHidden(int x, int y)
    {
        if (x >= 0 && x < gridCols && y >= 0 && y < gridRows)
        {
            TestCustomer neighbor = customerGrid[x, y];
            if (neighbor != null && neighbor.behaviorType == 1 && !neighbor.isServeable) // 1 = Hidden
            {
                neighbor.isServeable = true;
                neighbor.UpdateVisuals();
            }
        }
    }

    public TestCustomer FindCustomerForFood(Vector3 foodPos, Color foodColor)
    {
        TestCustomer bestMatch = null;
        float minDistance = float.MaxValue;
        float alignmentThreshold = 0.2f;

        foreach (var cst in customers)
        {
            if (!cst.isServeable) continue;

            bool alignX = Mathf.Abs(foodPos.x - cst.transform.position.x) < alignmentThreshold;
            bool alignZ = Mathf.Abs(foodPos.z - cst.transform.position.z) < alignmentThreshold;

            if (alignX || alignZ)
            {
                // Has line of sight?
                if (HasLineOfSight(foodPos, cst.transform.position, alignX))
                {
                    if (cst.orderColor == foodColor)
                    {
                        float dist = Vector3.Distance(foodPos, cst.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestMatch = cst;
                        }
                    }
                }
            }
        }

        return bestMatch;
    }

    private bool HasLineOfSight(Vector3 fromPos, Vector3 toPos, bool isAlignX)
    {
        // If alignX is true, they share the same X, meaning we check along Z axis.
        foreach (var cst in customers)
        {
            if (cst.transform.position == toPos) continue; // Skip the target itself

            bool cstAlignX = Mathf.Abs(cst.transform.position.x - fromPos.x) < 0.1f;
            bool cstAlignZ = Mathf.Abs(cst.transform.position.z - fromPos.z) < 0.1f;

            if (isAlignX && cstAlignX)
            {
                // Check if cst is between fromPos.z and toPos.z
                float minZ = Mathf.Min(fromPos.z, toPos.z);
                float maxZ = Mathf.Max(fromPos.z, toPos.z);
                if (cst.transform.position.z > minZ && cst.transform.position.z < maxZ)
                {
                    return false; // Blocked
                }
            }
            else if (!isAlignX && cstAlignZ)
            {
                // Check if cst is between fromPos.x and toPos.x
                float minX = Mathf.Min(fromPos.x, toPos.x);
                float maxX = Mathf.Max(fromPos.x, toPos.x);
                if (cst.transform.position.x > minX && cst.transform.position.x < maxX)
                {
                    return false; // Blocked
                }
            }
        }
        return true; // Clear line of sight
    }

    private void GeneratePainterQueues()
    {
        if (queueSlots == null || queueSlots.Length < 3 || levelData == null) return;
        
        for(int i = 0; i < 3; i++) {
            queues[i] = new List<TestFoodStack>();
        }

        Dictionary<int, TestFoodStack> unlinkedStacks = new Dictionary<int, TestFoodStack>();

        // 3 kuyruk listesi
        List<FoodChunk>[] levelDataQueues = { levelData.queue1, levelData.queue2, levelData.queue3 };

        for (int q = 0; q < 3; q++)
        {
            List<FoodChunk> currentQueueData = levelDataQueues[q];
            if (currentQueueData == null) continue;

            foreach (var chunk in currentQueueData)
            {
                GameObject baseObj = new GameObject("FoodStack_" + chunk.color.ToString());
                baseObj.transform.localScale = Vector3.one * 0.8f;
                baseObj.AddComponent<BoxCollider>(); 
                
                TestFoodStack food = baseObj.AddComponent<TestFoodStack>();
                food.foodColor = chunk.color;
                food.amount = chunk.amount;
                food.state = TestFoodStack.FoodState.InQueue;
                
                // Link Logic
                if (chunk.linkID > 0)
                {
                    if (unlinkedStacks.ContainsKey(chunk.linkID))
                    {
                        TestFoodStack partner = unlinkedStacks[chunk.linkID];
                        food.linkedStack = partner;
                        partner.linkedStack = food;
                        unlinkedStacks.Remove(chunk.linkID);
                    }
                    else
                    {
                        unlinkedStacks[chunk.linkID] = food;
                    }
                }
                
                food.queueIndex = q;
                food.isClickable = false;
                queues[q].Add(food);
            }
        }

        // Objeleri pozisyonlandırma ve isClickable ayarı
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < queues[i].Count; j++)
            {
                TestFoodStack food = queues[i][j];
                food.transform.position = queueSlots[i].position + new Vector3(0, 0, -j * 1.2f);
                
                if (j == 0 || allowPickFromBottom)
                {
                    food.isClickable = true;
                }
            }
        }
    }
}

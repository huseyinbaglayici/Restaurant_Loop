using System.Collections.Generic;
using UnityEngine;
using RestaurantLoop.Runtime.Data; // Arkadaşının scriptlerine referans

[CreateAssetMenu(menuName = "RestaurantLoop/Unified Level Data", fileName = "UnifiedLevel")]
public class TestLevelDataSO : ScriptableObject
{
    [Header("Conveyor Data (Takım Arkadaşının Sistemi)")]
    public ConveyorLevelDataSO conveyorData;

    [Header("Prefabs")]
    public GameObject customerPrefab;
    public float foodStackYOffset = 0.5f;
    public FoodPrefabData redFood;
    public FoodPrefabData yellowFood;
    public FoodPrefabData whiteFood;
    public FoodPrefabData blueFood;
    public FoodPrefabData pinkFood;

    [Header("Customer Grid")]
    public int gridSizeX = 10;
    public int gridSizeY = 10;
    public List<TestCustomerData> customers = new List<TestCustomerData>();

    [Header("Food Queues (Sütunlar)")]
    public List<FoodChunk> queue1 = new List<FoodChunk>();
    public List<FoodChunk> queue2 = new List<FoodChunk>();
    public List<FoodChunk> queue3 = new List<FoodChunk>();
}

[System.Serializable]
public class FoodChunk
{
    public TestCustomerData.CustomerColor color;
    public int amount;
    public int linkID; 
}

[System.Serializable]
public class FoodPrefabData
{
    public GameObject openPrefab;
    public GameObject closedPrefab;
}

[System.Serializable]
public class TestCustomerData
{
    public enum CustomerBehavior { Classic, Hidden, Type3 }
    public enum CustomerColor { None, Red, Yellow, White, Blue, Pink }

    public int gridX;
    public int gridY;
    public CustomerBehavior behavior; 
    public CustomerColor orderColor;
    public CustomerColor orderColor2; // 2nd Order için
}

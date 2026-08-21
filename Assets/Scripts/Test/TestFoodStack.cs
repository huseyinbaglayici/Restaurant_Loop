using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TestFoodStack : MonoBehaviour
{
    public enum FoodState { InQueue, OnRack, OnBelt }
    
    [Header("Food Stack Settings")]
    public FoodState state = FoodState.InQueue;
    public Color foodColor = Color.red;
    public int amount = 3; // Stack içindeki porsiyon sayısı
    
    public int queueIndex = -1; // Hangi kuyrukta olduğu
    public bool isClickable = false; // Tıklanabilir mi?
    
    private List<GameObject> visualCubes = new List<GameObject>();
    private BoxCollider col;
    private TextMesh textMesh;
    private GameObject bubbleBgObj;
    
    private void Start()
    {
        col = GetComponent<BoxCollider>();
        CreateVisualCubes();
        CreateUI();
    }
    
    private void CreateVisualCubes()
    {
        for (int i = 0; i < amount; i++)
        {
            bool isOpen = (i == amount - 1); // En üstteki açık olacak
            GameObject block = SpawnVisualBlock(i, isOpen);
            visualCubes.Add(block);
        }
        
        UpdateCollider();
    }
    
    private GameObject SpawnVisualBlock(int index, bool isOpen)
    {
        GameObject prefabToSpawn = null;
        
        // GameManager'dan prefableri çekmeyi dene
        if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM)
        {
            if (foodColor == Color.red) 
                prefabToSpawn = isOpen ? painterGM.redFood.openPrefab : painterGM.redFood.closedPrefab;
            else if (foodColor == Color.yellow) 
                prefabToSpawn = isOpen ? painterGM.yellowFood.openPrefab : painterGM.yellowFood.closedPrefab;
            else if (foodColor == Color.white) 
                prefabToSpawn = isOpen ? painterGM.whiteFood.openPrefab : painterGM.whiteFood.closedPrefab;
            else if (foodColor == Color.blue) 
                prefabToSpawn = isOpen ? painterGM.blueFood.openPrefab : painterGM.blueFood.closedPrefab;
            else // Pink: new Color(1f, 0.4f, 0.7f)
                prefabToSpawn = isOpen ? painterGM.pinkFood.openPrefab : painterGM.pinkFood.closedPrefab;
        }
        
        GameObject block;
        if (prefabToSpawn != null)
        {
            block = Instantiate(prefabToSpawn, transform);
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM2)
                yOffset = painterGM2.foodStackYOffset;

            block.transform.localPosition = new Vector3(0, index * yOffset, 0);
            // Prefab'in kendi orijinal scale ve rotation değerlerini bozmuyoruz!
        }
        else
        {
            // Prefab yoksa Fallback olarak Küp oluştur
            block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(transform);
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM2)
                yOffset = painterGM2.foodStackYOffset;

            block.transform.localPosition = new Vector3(0, index * yOffset, 0);
            block.transform.localScale = Vector3.one;
            Renderer rnd = block.GetComponent<Renderer>();
            if (rnd != null) rnd.material.color = foodColor;
        }
        
        // Çocuk objenin kendi collider'ını sil (Tıklamayı engellemesin)
        Collider[] colliders = block.GetComponentsInChildren<Collider>();
        foreach(var c in colliders) Destroy(c);
        
        return block;
    }
    
    private void UpdateCollider()
    {
        if (col != null && amount > 0)
        {
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM)
                yOffset = painterGM.foodStackYOffset;
                
            // Tüm blokları kapsayacak kadar büyük bir collider
            col.size = new Vector3(1f, amount * yOffset, 1f);
            col.center = new Vector3(0, (amount * yOffset) / 2f - (yOffset / 2f), 0);
        }
    }
    
    private void CreateUI()
    {
        // TextMesh objesi oluştur
        GameObject textObj = new GameObject("AmountText");
        textObj.transform.SetParent(transform);
        
        // Top-down kamera için X ekseninde 90 derece döndür
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
        
        textMesh = textObj.AddComponent<TextMesh>();
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.characterSize = 0.1f;
        textMesh.fontSize = 60;
        textMesh.color = Color.white;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.text = amount.ToString();
        
        // Siyah baloncuk için basit bir Quad
        bubbleBgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bubbleBgObj.name = "BubbleBG";
        bubbleBgObj.transform.SetParent(textObj.transform);
        // Text'in hemen altında dursun (yerel Z ekseninde)
        bubbleBgObj.transform.localPosition = new Vector3(0, 0, 0.01f); 
        bubbleBgObj.transform.localRotation = Quaternion.identity;
        bubbleBgObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // Baloncuk boyutu
        
        Destroy(bubbleBgObj.GetComponent<Collider>());
        
        Renderer bgRnd = bubbleBgObj.GetComponent<Renderer>();
        if (bgRnd != null)
        {
            Material bgMat = new Material(Shader.Find("Sprites/Default"));
            bgMat.color = new Color(0, 0, 0, 0.7f);
            bgRnd.material = bgMat;
        }
        
        UpdateUI();
    }
    
    public void UpdateUI()
    {
        if (textMesh != null) textMesh.text = amount.ToString();
        
        // Artık kuyrukta arkada olsalar da numaralarını gösteriyoruz
        if (textMesh != null && bubbleBgObj != null)
        {
            bool showUI = amount > 0;
            
            textMesh.GetComponent<Renderer>().enabled = showUI;
            bubbleBgObj.GetComponent<Renderer>().enabled = showUI;
            
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM)
                yOffset = painterGM.foodStackYOffset;
                
            // Yüksekliği en üstteki küpe göre ayarla, X ve Z'yi sağ alt köşe (+X, -Z) yap
            textMesh.transform.localPosition = new Vector3(0.6f, amount * yOffset + 0.1f, -0.6f);
        }
        
        // Yenilmiş (miktarı düşmüş) küpleri görselden sil
        bool countChanged = false;
        while (visualCubes.Count > amount && visualCubes.Count > 0)
        {
            GameObject topCube = visualCubes[visualCubes.Count - 1];
            visualCubes.RemoveAt(visualCubes.Count - 1);
            Destroy(topCube);
            countChanged = true;
        }
        
        // Eğer miktar düştüyse (yemek yendiyse) ve hala blok varsa, yeni en üstteki bloğu "Açık Kutu" ile değiştir!
        if (countChanged && visualCubes.Count > 0)
        {
            int topIndex = visualCubes.Count - 1;
            GameObject oldTop = visualCubes[topIndex];
            
            // Eski kapalı kutuyu yok et
            Destroy(oldTop);
            
            // Yerine açık kutu spawnla
            GameObject newTop = SpawnVisualBlock(topIndex, true);
            visualCubes[topIndex] = newTop;
        }
        
        UpdateCollider();
    }
    
    private void OnMouseDown()
    {
        Debug.Log($"Clicked on {gameObject.name}. State: {state}, isClickable: {isClickable}");
        
        // Kuyruktaysa doğrudan BANDA (Conveyor Belt) gönder
        if (state == FoodState.InQueue && isClickable)
        {
            bool addedToBelt = TestConveyorBelt.Instance.TryAddItem(this);
            Debug.Log($"TryAddItem (to belt from queue) result: {addedToBelt}");
            if (addedToBelt)
            {
                state = FoodState.OnBelt;
                // Kuyruktan çıkar ve arkadakini aktifleştir
                TestGameManager.Instance.PopQueue(queueIndex, this);
            }
        }
        // Raftaysa Banda Sok
        else if (state == FoodState.OnRack)
        {
            bool added = TestConveyorBelt.Instance.TryAddItem(this);
            Debug.Log($"TryAddItem (to belt) result: {added}");
            if (added)
            {
                state = FoodState.OnBelt;
                TestGameManager.Instance.RemoveFromRack(this);
            }
        }
    }
    
    private void Update()
    {
        // UI'ın anlık durumunu güncellemek için (Şu an kaldırıldı)
        UpdateUI();
    }
}

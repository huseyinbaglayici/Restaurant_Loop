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
    
    [Header("Link System")]
    public TestFoodStack linkedStack;
    public bool isGhost = false; // Miktarı 0 olunca bağlı obje bantta kalmaya devam edebilsin diye
    
    private List<GameObject> visualCubes = new List<GameObject>();
    private BoxCollider col;
    private TextMesh textMesh;
    private GameObject bubbleBgObj;
    private LineRenderer lineRend;
    
    private void Start()
    {
        col = GetComponent<BoxCollider>();
        CreateVisualCubes();
        CreateUI();
        
        if (linkedStack != null)
        {
            // İkili bağ görseli (ip) oluştur
            // Sadece ID'si küçük olan objenin LineRenderer'ı çizsin ki üst üste çakışmasın (Z-fighting)
            if (gameObject.GetInstanceID() < linkedStack.gameObject.GetInstanceID())
            {
                lineRend = gameObject.AddComponent<LineRenderer>();
                lineRend.startWidth = 0.25f;
                lineRend.endWidth = 0.25f;
                lineRend.positionCount = 2;
                
                // Unlit materyal
                lineRend.material = new Material(Shader.Find("Sprites/Default"));
                
                // Ortadan ikiye bölünen renk gradienti
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(foodColor, 0.0f), 
                        new GradientColorKey(foodColor, 0.49f), 
                        new GradientColorKey(linkedStack.foodColor, 0.51f), 
                        new GradientColorKey(linkedStack.foodColor, 1.0f) 
                    },
                    new GradientAlphaKey[] { 
                        new GradientAlphaKey(1.0f, 0.0f), 
                        new GradientAlphaKey(1.0f, 1.0f) 
                    }
                );
                lineRend.colorGradient = gradient;
            }
        }
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
        if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM && painterGM.levelData != null)
        {
            if (foodColor == Color.red) 
                prefabToSpawn = isOpen ? painterGM.levelData.redFood.openPrefab : painterGM.levelData.redFood.closedPrefab;
            else if (foodColor == Color.yellow) 
                prefabToSpawn = isOpen ? painterGM.levelData.yellowFood.openPrefab : painterGM.levelData.yellowFood.closedPrefab;
            else if (foodColor == Color.white) 
                prefabToSpawn = isOpen ? painterGM.levelData.whiteFood.openPrefab : painterGM.levelData.whiteFood.closedPrefab;
            else if (foodColor == Color.blue) 
                prefabToSpawn = isOpen ? painterGM.levelData.blueFood.openPrefab : painterGM.levelData.blueFood.closedPrefab;
            else // Pink: new Color(1f, 0.4f, 0.7f)
                prefabToSpawn = isOpen ? painterGM.levelData.pinkFood.openPrefab : painterGM.levelData.pinkFood.closedPrefab;
        }
        
        GameObject block;
        if (prefabToSpawn != null)
        {
            block = Instantiate(prefabToSpawn, transform);
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM2 && painterGM2.levelData != null)
                yOffset = painterGM2.levelData.foodStackYOffset;

            block.transform.localPosition = new Vector3(0, index * yOffset, 0);
            // Prefab'in kendi orijinal scale ve rotation değerlerini bozmuyoruz!
        }
        else
        {
            // Prefab yoksa Fallback olarak Küp oluştur
            block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(transform);
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM2 && painterGM2.levelData != null)
                yOffset = painterGM2.levelData.foodStackYOffset;

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
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM && painterGM.levelData != null)
                yOffset = painterGM.levelData.foodStackYOffset;
                
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
        
        if (textMesh != null && bubbleBgObj != null)
        {
            // Ghost durumundaysa da UI açık kalsın (üzerinde 0 yazacak)
            bool showUI = amount > 0 || isGhost;
            
            textMesh.GetComponent<Renderer>().enabled = showUI;
            bubbleBgObj.GetComponent<Renderer>().enabled = showUI;
            
            float yOffset = 1.05f;
            if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM && painterGM.levelData != null)
                yOffset = painterGM.levelData.foodStackYOffset;
                
            // Yüksekliği en üstteki küpe göre ayarla, 0 olsa bile 1 yüksekliğinde dursun
            float currentAmount = Mathf.Max(1, amount);
            textMesh.transform.localPosition = new Vector3(0.6f, currentAmount * yOffset + 0.1f, -0.6f);
        }
        
        bool countChanged = false;
        // Eğer ghost ise ve miktar 0'a düştüyse, 1 tane kutu kalsın (Asset olarak dursun)
        int targetVisualCount = (isGhost && amount == 0) ? 1 : amount;
        
        // Yenilmiş (miktarı düşmüş) küpleri görselden sil
        while (visualCubes.Count > targetVisualCount && visualCubes.Count > 0)
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
        
        // Eğer bir partneri varsa ve partneri tıklanabilir DEĞİLSE, tıklamayı reddet
        if (linkedStack != null && !linkedStack.isClickable)
        {
            Debug.Log("Cannot click! Linked partner is not at the front of its queue.");
            return;
        }

        // Kuyruktaysa doğrudan BANDA (Conveyor Belt) gönder
        if (state == FoodState.InQueue && isClickable)
        {
            if (linkedStack != null)
            {
                // İkili bant ekleme
                bool addedBoth = TestConveyorBelt.Instance.TryAddLinkedItems(this, linkedStack);
                if (addedBoth)
                {
                    state = FoodState.OnBelt;
                    linkedStack.state = FoodState.OnBelt;
                    TestGameManager.Instance.PopQueue(queueIndex, this);
                    TestGameManager.Instance.PopQueue(linkedStack.queueIndex, linkedStack);
                }
            }
            else
            {
                // Tekli
                bool addedToBelt = TestConveyorBelt.Instance.TryAddItem(this);
                if (addedToBelt)
                {
                    state = FoodState.OnBelt;
                    TestGameManager.Instance.PopQueue(queueIndex, this);
                }
            }
        }
        // Raftaysa Banda Sok (Raftan ikili dönme olayını şimdilik tekli gibi bırakıyoruz veya TODO ekliyoruz)
        else if (state == FoodState.OnRack)
        {
            // Eğer rack sisteminde linked yapılar varsa buraya eklenebilir, şu an basit geçiyorum.
            bool added = TestConveyorBelt.Instance.TryAddItem(this);
            if (added)
            {
                state = FoodState.OnBelt;
                TestGameManager.Instance.RemoveFromRack(this);
            }
        }
    }
    
    private void Update()
    {
        UpdateUI();
        
        if (lineRend != null && linkedStack != null)
        {
            // Eğer birimiz tamamen yok olduysak çizgiyi kapat
            if (amount <= 0 && linkedStack.amount <= 0)
            {
                lineRend.enabled = false;
            }
            else
            {
                float yOffset = 1.05f;
                if (TestGameManager.Instance != null && TestGameManager.Instance is TestPainterGameManager painterGM && painterGM.levelData != null)
                    yOffset = painterGM.levelData.foodStackYOffset;

                Vector3 pos1 = transform.position + new Vector3(0, Mathf.Max(0.5f, (amount * yOffset) / 2f), 0);
                Vector3 pos2 = linkedStack.transform.position + new Vector3(0, Mathf.Max(0.5f, (linkedStack.amount * yOffset) / 2f), 0);
                
                lineRend.SetPosition(0, pos1);
                lineRend.SetPosition(1, pos2);
            }
        }
    }
}

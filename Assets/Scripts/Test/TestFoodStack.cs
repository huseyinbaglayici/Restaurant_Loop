using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TestFoodStack : MonoBehaviour
{
    public enum FoodState { InQueue, OnRack, OnBelt }
    
    [Header("Food Stack Settings")]
    public FoodState state = FoodState.InQueue;
    public Color foodColor = Color.red;
    public TestCustomerData.CustomerColor foodColorEnum;
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
        
        // GameManager'dan veya LevelData'dan prefableri çekmeyi dene
        if (TestGameManager.Instance != null)
        {
            var gm = TestGameManager.Instance;
            var ld = gm.levelData;

            TestCustomerData.CustomerColor cEnum = foodColorEnum;
            if (cEnum == TestCustomerData.CustomerColor.None)
            {
                if (foodColor == Color.red) cEnum = TestCustomerData.CustomerColor.Red;
                else if (foodColor == Color.yellow) cEnum = TestCustomerData.CustomerColor.Yellow;
                else if (foodColor == Color.white) cEnum = TestCustomerData.CustomerColor.White;
                else if (foodColor == Color.blue) cEnum = TestCustomerData.CustomerColor.Blue;
                else if (foodColor == new Color(1f, 0.4f, 0.7f)) cEnum = TestCustomerData.CustomerColor.Pink;
            }

            FoodPrefabData targetFood = null;
            switch (cEnum)
            {
                case TestCustomerData.CustomerColor.Red:
                    targetFood = (gm.redFood != null && (gm.redFood.openPrefab != null || gm.redFood.closedPrefab != null)) ? gm.redFood : (ld != null ? ld.redFood : null);
                    break;
                case TestCustomerData.CustomerColor.Yellow:
                    targetFood = (gm.yellowFood != null && (gm.yellowFood.openPrefab != null || gm.yellowFood.closedPrefab != null)) ? gm.yellowFood : (ld != null ? ld.yellowFood : null);
                    break;
                case TestCustomerData.CustomerColor.White:
                    targetFood = (gm.whiteFood != null && (gm.whiteFood.openPrefab != null || gm.whiteFood.closedPrefab != null)) ? gm.whiteFood : (ld != null ? ld.whiteFood : null);
                    break;
                case TestCustomerData.CustomerColor.Blue:
                    targetFood = (gm.blueFood != null && (gm.blueFood.openPrefab != null || gm.blueFood.closedPrefab != null)) ? gm.blueFood : (ld != null ? ld.blueFood : null);
                    break;
                case TestCustomerData.CustomerColor.Pink:
                    targetFood = (gm.pinkFood != null && (gm.pinkFood.openPrefab != null || gm.pinkFood.closedPrefab != null)) ? gm.pinkFood : (ld != null ? ld.pinkFood : null);
                    break;
            }

            if (targetFood != null)
            {
                prefabToSpawn = isOpen ? targetFood.openPrefab : targetFood.closedPrefab;
            }
        }
        
        GameObject block;
        float yOffset = 0.5f;
        if (TestGameManager.Instance != null)
        {
            yOffset = TestGameManager.Instance.foodStackYOffset;
            if (yOffset <= 0 && TestGameManager.Instance.levelData != null)
                yOffset = TestGameManager.Instance.levelData.foodStackYOffset;
        }
        if (yOffset <= 0) yOffset = 0.5f;

        if (prefabToSpawn != null)
        {
            block = Instantiate(prefabToSpawn, transform);
            block.transform.localPosition = new Vector3(0, index * yOffset, 0);
            // Prefab'in kendi orijinal scale ve rotation değerlerini bozmuyoruz!
        }
        else
        {
            // Prefab yoksa Fallback olarak Küp oluştur
            block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(transform);
            block.transform.localPosition = new Vector3(0, index * yOffset, 0);
            block.transform.localScale = Vector3.one * 0.4f;
            Renderer rnd = block.GetComponent<Renderer>();
            if (rnd != null) rnd.material.color = foodColor;
        }
        
        // Çocuk objenin kendi collider'ını sil (Tıklamayı engellemesin)
        Collider[] colliders = block.GetComponentsInChildren<Collider>();
        foreach(var c in colliders) Destroy(c);
        
        return block;
    }
    
    private float GetFoodStackYOffset()
    {
        if (TestGameManager.Instance != null)
        {
            float y = TestGameManager.Instance.foodStackYOffset;
            if (y <= 0 && TestGameManager.Instance.levelData != null)
                y = TestGameManager.Instance.levelData.foodStackYOffset;
            if (y > 0) return y;
        }
        return 0.5f;
    }

    private void UpdateCollider()
    {
        if (col != null && amount > 0)
        {
            float yOffset = GetFoodStackYOffset();
                
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

        // TitanOne-Regular fontunu yükle
        Font titanFont = Resources.Load<Font>("Fonts/TitanOne-Regular");
        if (titanFont == null)
        {
#if UNITY_EDITOR
            titanFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/TitanOne-Regular.ttf");
#endif
        }
        if (titanFont != null)
        {
            textMesh.font = titanFont;
            MeshRenderer mr = textMesh.GetComponent<MeshRenderer>();
            if (mr != null && titanFont.material != null)
            {
                mr.material = titanFont.material;
            }
        }

        textMesh.text = amount.ToString();
        
        // Yuvarlak (Circle) Arka Plan Oluştur
        bubbleBgObj = new GameObject("BubbleCircleBG");
        bubbleBgObj.transform.SetParent(textObj.transform);
        bubbleBgObj.transform.localPosition = new Vector3(0, 0, 0.01f);
        bubbleBgObj.transform.localRotation = Quaternion.identity;

        MeshFilter mf = bubbleBgObj.AddComponent<MeshFilter>();
        MeshRenderer bgRnd = bubbleBgObj.AddComponent<MeshRenderer>();
        mf.mesh = CreateCircleMesh(0.28f, 32);

        Material bgMat = new Material(Shader.Find("Sprites/Default"));
        bgMat.color = new Color(0, 0, 0, 0.75f);
        bgRnd.material = bgMat;
        
        UpdateUI();
    }

    private Mesh CreateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CircleMesh";

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float x = Mathf.Cos(rad) * radius;
            float y = Mathf.Sin(rad) * radius;

            vertices[i + 1] = new Vector3(x, y, 0);
            uvs[i + 1] = new Vector2((x / radius + 1f) * 0.5f, (y / radius + 1f) * 0.5f);

            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = (i + 1) % segments + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
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
            
            float yOffset = GetFoodStackYOffset();
                
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
        
        // Sadece KUYRUKTAYSA (InQueue) partnerinin de kuyruk başında (tıklanabilir) olması şartı aranır
        if (state == FoodState.InQueue && linkedStack != null && !linkedStack.isClickable)
        {
            Debug.Log("Cannot click! Linked partner is not at the front of its queue.");
            return;
        }

        // 1. Kuyruktaysa doğrudan BANDA (Conveyor Belt) gönder
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
        // 2. Raftaysa (OnRack) Banda Tekrar Sok
        else if (state == FoodState.OnRack)
        {
            bool added = TestConveyorBelt.Instance.TryAddItem(this);
            if (added)
            {
                state = FoodState.OnBelt;
                TestGameManager.Instance.RemoveFromRack(this);
                Debug.Log($"Food {gameObject.name} moved from Rack to Belt successfully.");
            }
            else
            {
                Debug.Log("Cannot add to belt right now! Belt is full or entry point is blocked.");
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
                float yOffset = GetFoodStackYOffset();

                Vector3 pos1 = transform.position + new Vector3(0, Mathf.Max(0.5f, (amount * yOffset) / 2f), 0);
                Vector3 pos2 = linkedStack.transform.position + new Vector3(0, Mathf.Max(0.5f, (linkedStack.amount * yOffset) / 2f), 0);
                
                lineRend.SetPosition(0, pos1);
                lineRend.SetPosition(1, pos2);
            }
        }
    }
    
    public IEnumerator MoveTo(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        float duration = 0.2f;
        while (elapsed < duration)
        {
            if (state != FoodState.InQueue) yield break;
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
        if (state == FoodState.InQueue)
        {
            transform.position = targetPos;
        }
    }
}

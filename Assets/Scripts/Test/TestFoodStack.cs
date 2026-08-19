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
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localPosition = new Vector3(0, i * 1.05f, 0); // Üst üste diz (hafif boşluklu)
            cube.transform.localScale = Vector3.one; 
            
            // Çocuk küpün kendi collider'ını sil (Tıklamayı engellemesin, ana obje yönetecek)
            Destroy(cube.GetComponent<Collider>());
            
            Renderer rnd = cube.GetComponent<Renderer>();
            if (rnd != null) rnd.material.color = foodColor;
            
            visualCubes.Add(cube);
        }
        
        UpdateCollider();
    }
    
    private void UpdateCollider()
    {
        if (col != null && amount > 0)
        {
            // Tüm küpleri kapsayacak kadar büyük bir collider
            col.size = new Vector3(1f, amount * 1.05f, 1f);
            col.center = new Vector3(0, (amount * 1.05f) / 2f - 0.5f, 0);
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
        
        // Sadece aktif (tıklanabilir) ise veya banttaysa UI göster
        if (textMesh != null && bubbleBgObj != null)
        {
            bool showUI = (state != FoodState.InQueue) || isClickable;
            
            textMesh.GetComponent<Renderer>().enabled = showUI;
            bubbleBgObj.GetComponent<Renderer>().enabled = showUI;
            
            // Yüksekliği en üstteki küpe göre ayarla, X ve Z'yi sağ alt köşe (+X, -Z) yap
            textMesh.transform.localPosition = new Vector3(0.6f, amount * 1.05f + 0.1f, -0.6f);
        }
        
        // Yenilmiş (miktarı düşmüş) küpleri görselden sil
        while (visualCubes.Count > amount && visualCubes.Count > 0)
        {
            GameObject topCube = visualCubes[visualCubes.Count - 1];
            visualCubes.RemoveAt(visualCubes.Count - 1);
            Destroy(topCube);
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

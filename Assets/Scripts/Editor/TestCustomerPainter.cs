using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class TestCustomerPainter : EditorWindow
{
    public enum CustomerColor { None, Red, Yellow, White, Blue, Pink }
    public enum CustomerBehavior { Classic, Hidden, Type3 }

    [System.Serializable]
    public struct CellData
    {
        public bool isActive;
        public CustomerBehavior behavior;
        public CustomerColor color;
        public CustomerColor color2; // Type 3 (Double Order) için ikinci renk
    }

    private int gridSizeX = 10;
    private int gridSizeY = 10;
    private float gridSpacing = 1.5f;
    private CellData[,] grid;

    private CustomerColor selectedColor = CustomerColor.Red;
    private CustomerColor selectedColor2 = CustomerColor.Yellow;
    private CustomerBehavior selectedBehavior = CustomerBehavior.Classic;

    [MenuItem("Tools/Test Customer Painter")]
    public static void ShowWindow()
    {
        GetWindow<TestCustomerPainter>("Customer Painter");
    }

    private void OnEnable()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        grid = new CellData[gridSizeX, gridSizeY];
    }

    private Vector2 scrollPos;

    private void OnGUI()
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Customer Painter Settings", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        gridSizeX = EditorGUILayout.IntSlider("Grid Size X", gridSizeX, 1, 20);
        gridSizeY = EditorGUILayout.IntSlider("Grid Size Y", gridSizeY, 1, 20);
        if (EditorGUI.EndChangeCheck())
        {
            InitializeGrid();
        }

        gridSpacing = EditorGUILayout.FloatField("Grid Spacing", gridSpacing);

        GUILayout.Space(10);
        
        GUILayout.Label("1. Select Customer Type:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(selectedBehavior == CustomerBehavior.Classic, "Classic", "Button")) selectedBehavior = CustomerBehavior.Classic;
        if (GUILayout.Toggle(selectedBehavior == CustomerBehavior.Hidden, "Hidden", "Button")) selectedBehavior = CustomerBehavior.Hidden;
        if (GUILayout.Toggle(selectedBehavior == CustomerBehavior.Type3, "2nd Order", "Button")) selectedBehavior = CustomerBehavior.Type3;
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("2. Select Brush Color:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(selectedColor == CustomerColor.None, "Eraser", "Button")) selectedColor = CustomerColor.None;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Toggle(selectedColor == CustomerColor.Red, "Red", "Button")) selectedColor = CustomerColor.Red;
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Toggle(selectedColor == CustomerColor.Yellow, "Yellow", "Button")) selectedColor = CustomerColor.Yellow;
        GUI.backgroundColor = Color.white;
        if (GUILayout.Toggle(selectedColor == CustomerColor.White, "White", "Button")) selectedColor = CustomerColor.White;
        GUI.backgroundColor = Color.blue;
        if (GUILayout.Toggle(selectedColor == CustomerColor.Blue, "Blue", "Button")) selectedColor = CustomerColor.Blue;
        GUI.backgroundColor = new Color(1f, 0.4f, 0.7f); // Pink
        if (GUILayout.Toggle(selectedColor == CustomerColor.Pink, "Pink", "Button")) selectedColor = CustomerColor.Pink;
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        if (selectedBehavior == CustomerBehavior.Type3)
        {
            GUILayout.Space(5);
            GUILayout.Label("3. Select Secondary Color:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.red;
            if (GUILayout.Toggle(selectedColor2 == CustomerColor.Red, "Red", "Button")) selectedColor2 = CustomerColor.Red;
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Toggle(selectedColor2 == CustomerColor.Yellow, "Yellow", "Button")) selectedColor2 = CustomerColor.Yellow;
            GUI.backgroundColor = Color.white;
            if (GUILayout.Toggle(selectedColor2 == CustomerColor.White, "White", "Button")) selectedColor2 = CustomerColor.White;
            GUI.backgroundColor = Color.blue;
            if (GUILayout.Toggle(selectedColor2 == CustomerColor.Blue, "Blue", "Button")) selectedColor2 = CustomerColor.Blue;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.7f); // Pink
            if (GUILayout.Toggle(selectedColor2 == CustomerColor.Pink, "Pink", "Button")) selectedColor2 = CustomerColor.Pink;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        GUILayout.Label("Canvas (Click to paint):", EditorStyles.boldLabel);
        
        if (grid == null || grid.GetLength(0) != gridSizeX || grid.GetLength(1) != gridSizeY) 
            InitializeGrid();

        // Draw Grid
        GUILayout.BeginVertical("box");
        for (int y = gridSizeY - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int x = 0; x < gridSizeX; x++)
            {
                CellData cell = grid[x, y];
                GUI.backgroundColor = GetColorValue(cell.color);

                // Davranış tipini buton üzerinde küçük harfle göster
                string btnText = "";
                if (cell.color != CustomerColor.None)
                {
                    if (cell.behavior == CustomerBehavior.Classic) btnText = "C";
                    else if (cell.behavior == CustomerBehavior.Hidden) btnText = "H";
                    else if (cell.behavior == CustomerBehavior.Type3) btnText = "2nd";
                }

                // Butonu çizmek için rect alıyoruz ki üzerine çift renk çizebilelim
                Rect btnRect = GUILayoutUtility.GetRect(35, 35);
                
                if (cell.behavior == CustomerBehavior.Type3 && cell.color != CustomerColor.None)
                {
                    // Çift siparişse kareyi ortadan ikiye bölerek çiz (iki üçgen veya iki dikdörtgen)
                    // Sol yarı:
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, btnRect.width / 2f, btnRect.height), GetColorValue(cell.color));
                    // Sağ yarı:
                    EditorGUI.DrawRect(new Rect(btnRect.x + btnRect.width / 2f, btnRect.y, btnRect.width / 2f, btnRect.height), GetColorValue(cell.color2));
                    
                    // Üzerine butonun tıklanabilirliğini ve yazısını ekle (Görünmez bir buton)
                    GUI.backgroundColor = Color.clear;
                    if (GUI.Button(btnRect, btnText))
                    {
                        grid[x, y] = new CellData { color = selectedColor, color2 = selectedColor2, behavior = selectedBehavior, isActive = true };
                    }
                    GUI.backgroundColor = Color.white; // Geri yükle
                }
                else
                {
                    // Normal tek renkli buton
                    GUI.backgroundColor = GetColorValue(cell.color);
                    if (GUI.Button(btnRect, btnText))
                    {
                        grid[x, y] = new CellData { color = selectedColor, color2 = selectedColor2, behavior = selectedBehavior, isActive = true };
                    }
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);

        if (GUILayout.Button("Create in Scene", GUILayout.Height(40)))
        {
            if (SceneManager.GetActiveScene().name != "CustomerTestScene")
            {
                if (!EditorUtility.DisplayDialog("Wrong Scene", "You are not in 'CustomerTestScene'. Do you still want to spawn customers here?", "Yes", "No"))
                {
                    return;
                }
            }
            SpawnCustomers();
        }
        
        if (GUILayout.Button("Clear Canvas"))
        {
            InitializeGrid();
        }

        GUILayout.EndScrollView();
    }

    private Color GetColorValue(CustomerColor color)
    {
        switch (color)
        {
            case CustomerColor.Red: return Color.red;
            case CustomerColor.Yellow: return Color.yellow;
            case CustomerColor.White: return Color.white;
            case CustomerColor.Blue: return Color.blue;
            case CustomerColor.Pink: return new Color(1f, 0.4f, 0.7f);
            case CustomerColor.None: default: return Color.gray;
        }
    }

    private void SpawnCustomers()
    {
        // Eski "Painted Customers" objesini bul ve sil ki üst üste binmesin
        GameObject oldParent = GameObject.Find("Painted Customers");
        if (oldParent != null) DestroyImmediate(oldParent);

        GameObject parentObj = new GameObject("Painted Customers");
        float offsetX = (gridSizeX - 1) * gridSpacing / 2f;
        float offsetY = (gridSizeY - 1) * gridSpacing / 2f;

        System.Collections.Generic.Dictionary<Color, int> colorCounts = new System.Collections.Generic.Dictionary<Color, int>();

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                CellData cell = grid[x, y];
                if (cell.color != CustomerColor.None)
                {
                    Vector3 pos = new Vector3(x * gridSpacing - offsetX, 0, y * gridSpacing - offsetY);
                    SpawnCustomer(cell, pos, parentObj.transform, $"Customer_{x}_{y}", x, y);

                    // Sayım yap
                    Color c1 = GetColorValue(cell.color);
                    if (colorCounts.ContainsKey(c1)) colorCounts[c1]++;
                    else colorCounts[c1] = 1;

                    if (cell.behavior == CustomerBehavior.Type3)
                    {
                        Color c2 = GetColorValue(cell.color2);
                        if (colorCounts.ContainsKey(c2)) colorCounts[c2]++;
                        else colorCounts[c2] = 1;
                    }
                }
            }
        }

        // TestPainterGameManager'ı bul ve dataset'i yaz
        TestPainterGameManager gm = FindObjectOfType<TestPainterGameManager>();
        if (gm != null)
        {
            gm.customerCenter = parentObj.transform; // Yeni parent'ı otomatik bağla
            gm.predefinedFoodChunks.Clear();
            
            // Yemek chunk'larını hesapla
            foreach (var kvp in colorCounts)
            {
                int remaining = kvp.Value;
                while (remaining > 0)
                {
                    int chunkSize = Random.Range(3, 6); 
                    if (remaining <= 5) {
                        chunkSize = remaining; 
                    } else if (remaining - chunkSize < 3) {
                        chunkSize = remaining - 3;
                        if (chunkSize < 3) chunkSize = remaining; 
                    }
                    
                    remaining -= chunkSize;
                    
                    TestPainterGameManager.FoodChunk chunk = new TestPainterGameManager.FoodChunk();
                    chunk.color = kvp.Key;
                    chunk.amount = chunkSize;
                    gm.predefinedFoodChunks.Add(chunk);
                }
            }
            
            gm.usePredefinedChunks = true;
            EditorUtility.SetDirty(gm);
            Debug.Log($"Dataset created! Total unique colors: {colorCounts.Count}");
        }
        else
        {
            Debug.LogWarning("TestPainterGameManager not found in scene! Dataset could not be assigned.");
        }
        
        Selection.activeGameObject = parentObj;
        Debug.Log("Painted Customers spawned successfully!");
    }

    private void SpawnCustomer(CellData cell, Vector3 position, Transform parent, string name, int gridX, int gridY)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * 0.7f;
        obj.transform.SetParent(parent);

        TestCustomer cst = obj.AddComponent<TestCustomer>();
        
        cst.orderColor = GetColorValue(cell.color);
        if (cell.behavior == CustomerBehavior.Type3)
        {
            cst.orderColor2 = GetColorValue(cell.color2);
            cst.hasSecondOrder = true;
        }
        
        cst.gridX = gridX;
        cst.gridY = gridY;
        cst.behaviorType = (int)cell.behavior;
        
        // HACK: isServeable'ı Classic veya Type3 ise true yapıyoruz (Sadece Hidden'lar baştan kapalı olur)
        cst.isServeable = (cell.behavior != CustomerBehavior.Hidden); 
        
        Renderer rnd = obj.GetComponent<Renderer>();
        if (rnd != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            // Tüm customerlar renksiz (beyaz)
            block.SetColor("_Color", Color.white);
            block.SetColor("_BaseColor", Color.white); // URP Support
            rnd.SetPropertyBlock(block);
        }

        obj.name = $"Customer_{gridX}_{gridY}";
    }
}

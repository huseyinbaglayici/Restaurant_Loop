using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using RestaurantLoop.Runtime.Data;
using Editor.LevelEditor; // Arkadaşının tool namespace'i
using System.Collections.Generic;

public class TestUnifiedLevelEditor : EditorWindow
{
    private TestLevelDataSO activeData;
    private SerializedObject serializedData;

    private ConveyorGridCanvas conveyorCanvas;
    private VisualElement customerLayer;

    // Araç Seçenekleri
    private enum UnifiedMode { Conveyor, Customer }
    private UnifiedMode currentMode = UnifiedMode.Customer;

    private TestCustomerData.CustomerBehavior selectedBehavior = TestCustomerData.CustomerBehavior.Classic;
    private TestCustomerData.CustomerColor selectedColor = TestCustomerData.CustomerColor.Red;
    private TestCustomerData.CustomerColor selectedColor2 = TestCustomerData.CustomerColor.Yellow;

    [MenuItem("Tools/Unified Level Editor")]
    public static void ShowWindow()
    {
        var w = GetWindow<TestUnifiedLevelEditor>("Unified Level Editor");
        w.minSize = new Vector2(800, 600);
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection = FlexDirection.Row; // Sol panel, Sağ grid

        // SOL PANEL
        var leftPanel = new ScrollView();
        leftPanel.style.width = 350;
        leftPanel.style.borderRightWidth = 2;
        leftPanel.style.borderRightColor = Color.gray;
        leftPanel.style.paddingTop = 10;
        leftPanel.style.paddingBottom = 10;
        leftPanel.style.paddingLeft = 10;
        leftPanel.style.paddingRight = 10;
        root.Add(leftPanel);

        // SAĞ PANEL (Canvas Host)
        var rightPanel = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        rightPanel.style.flexGrow = 1;
        rightPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f); // Koyu arka plan
        root.Add(rightPanel);

        // DATA SEÇİMİ
        var objectField = new ObjectField("Level Data");
        objectField.objectType = typeof(TestLevelDataSO);
        objectField.RegisterValueChangedCallback(evt => {
            activeData = evt.newValue as TestLevelDataSO;
            LoadData();
        });
        leftPanel.Add(objectField);

        var btnCreateNew = new Button(() => {
            activeData = ScriptableObject.CreateInstance<TestLevelDataSO>();
            activeData.conveyorData = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();
            
            string path = EditorUtility.SaveFilePanelInProject("Save Unified Level", "UnifiedLevel", "asset", "");
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(activeData.conveyorData, path.Replace(".asset", "_Conveyor.asset"));
            AssetDatabase.CreateAsset(activeData, path);
            AssetDatabase.SaveAssets();
            
            objectField.value = activeData;
        }) { text = "Create New Level Data" };
        leftPanel.Add(btnCreateNew);

        leftPanel.Add(new VisualElement() { style = { height = 15 } });

        // IMGUI KAPSAYICISI (Sol Paneldeki Araçlar ve Liste İçin)
        var imguiContainer = new IMGUIContainer(DrawLeftPanelIMGUI);
        leftPanel.Add(imguiContainer);

        // CONVEYOR CANVAS
        conveyorCanvas = new ConveyorGridCanvas();
        conveyorCanvas.style.flexGrow = 1; // Alanı kaplaması için
        rightPanel.Add(conveyorCanvas);

        // CUSTOMER LAYER (Conveyor üzerine binen şeffaf katman)
        customerLayer = new VisualElement();
        customerLayer.style.position = Position.Absolute;
        customerLayer.style.left = 0; customerLayer.style.top = 0;
        customerLayer.style.right = 0; customerLayer.style.bottom = 0;
        customerLayer.pickingMode = PickingMode.Ignore; // Tıklamaları engellememesi için
        conveyorCanvas.Add(customerLayer);

        // Tıklama Olayları
        conveyorCanvas.RegisterCallback<PointerDownEvent>(OnGridClicked, TrickleDown.TrickleDown);
        
        LoadData();
    }

    private void DrawLeftPanelIMGUI()
    {
        if (activeData == null)
        {
            EditorGUILayout.HelpBox("Lütfen bir Level Data seçin veya oluşturun.", MessageType.Info);
            return;
        }

        serializedData?.Update();

        GUILayout.Label("--- LEVEL SETTINGS ---", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntField("Grid Genişliği", activeData.gridSizeX);
        int newHeight = EditorGUILayout.IntField("Grid Yüksekliği", activeData.gridSizeY);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(activeData, "Change Grid Size");
            activeData.gridSizeX = Mathf.Clamp(newWidth, 2, 30);
            activeData.gridSizeY = Mathf.Clamp(newHeight, 2, 30);
            if (activeData.conveyorData != null)
            {
                Undo.RecordObject(activeData.conveyorData, "Change Grid Size");
                activeData.conveyorData.GridWidth = activeData.gridSizeX;
                activeData.conveyorData.GridHeight = activeData.gridSizeY;
                EditorUtility.SetDirty(activeData.conveyorData);
            }
            EditorUtility.SetDirty(activeData);
            LoadData(); 
        }

        GUILayout.Space(5);
        GUILayout.Label("Prefabs", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        SerializedProperty custPrefab = serializedData.FindProperty("customerPrefab");
        SerializedProperty yOff = serializedData.FindProperty("foodStackYOffset");
        SerializedProperty rFood = serializedData.FindProperty("redFood");
        SerializedProperty yFood = serializedData.FindProperty("yellowFood");
        SerializedProperty wFood = serializedData.FindProperty("whiteFood");
        SerializedProperty bFood = serializedData.FindProperty("blueFood");
        SerializedProperty pFood = serializedData.FindProperty("pinkFood");
        
        EditorGUILayout.PropertyField(custPrefab, new GUIContent("Customer Prefab"));
        EditorGUILayout.PropertyField(yOff, new GUIContent("Food Stack Y Offset"));
        EditorGUILayout.PropertyField(rFood, new GUIContent("Red Food"));
        EditorGUILayout.PropertyField(yFood, new GUIContent("Yellow Food"));
        EditorGUILayout.PropertyField(wFood, new GUIContent("White Food"));
        EditorGUILayout.PropertyField(bFood, new GUIContent("Blue Food"));
        EditorGUILayout.PropertyField(pFood, new GUIContent("Pink Food"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedData.ApplyModifiedProperties();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("--- TOOLS ---", EditorStyles.boldLabel);
        currentMode = (UnifiedMode)GUILayout.Toolbar((int)currentMode, new string[] { "Conveyor Tool", "Customer Tool" });

        GUILayout.Space(10);

        if (currentMode == UnifiedMode.Conveyor)
        {
            EditorGUILayout.HelpBox("Bant çizmek için sağdaki Grid'i kullanın.\nSol tık: Çiz\nSağ tık: Sil\nShift+Tık: Döndür", MessageType.Info);
            conveyorCanvas.Mode = CanvasMode.Path;
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(conveyorCanvas.BrushShape == ConveyorTileShape.Straight, "Düz Bant", "Button"))
                conveyorCanvas.BrushShape = ConveyorTileShape.Straight;
            if (GUILayout.Toggle(conveyorCanvas.BrushShape == ConveyorTileShape.Corner, "Viraj", "Button"))
                conveyorCanvas.BrushShape = ConveyorTileShape.Corner;
            GUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Müşteri yerleştirmek için sağdaki Grid'i kullanın.", MessageType.Info);
            
            GUILayout.Label("Müşteri Tipi:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedBehavior == TestCustomerData.CustomerBehavior.Classic, "Classic", "Button")) selectedBehavior = TestCustomerData.CustomerBehavior.Classic;
            if (GUILayout.Toggle(selectedBehavior == TestCustomerData.CustomerBehavior.Hidden, "Hidden", "Button")) selectedBehavior = TestCustomerData.CustomerBehavior.Hidden;
            if (GUILayout.Toggle(selectedBehavior == TestCustomerData.CustomerBehavior.Type3, "2nd Order", "Button")) selectedBehavior = TestCustomerData.CustomerBehavior.Type3;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label("Müşteri Rengi:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.None, "Silici", "Button")) selectedColor = TestCustomerData.CustomerColor.None;
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.Red, "Kırmızı", "Button")) selectedColor = TestCustomerData.CustomerColor.Red;
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.Yellow, "Sarı", "Button")) selectedColor = TestCustomerData.CustomerColor.Yellow;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.White, "Beyaz", "Button")) selectedColor = TestCustomerData.CustomerColor.White;
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.Blue, "Mavi", "Button")) selectedColor = TestCustomerData.CustomerColor.Blue;
            if (GUILayout.Toggle(selectedColor == TestCustomerData.CustomerColor.Pink, "Pembe", "Button")) selectedColor = TestCustomerData.CustomerColor.Pink;
            GUILayout.EndHorizontal();

            if (selectedBehavior == TestCustomerData.CustomerBehavior.Type3)
            {
                GUILayout.Space(5);
                GUILayout.Label("2. Sipariş Rengi:", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selectedColor2 == TestCustomerData.CustomerColor.Red, "Kırmızı", "Button")) selectedColor2 = TestCustomerData.CustomerColor.Red;
                if (GUILayout.Toggle(selectedColor2 == TestCustomerData.CustomerColor.Yellow, "Sarı", "Button")) selectedColor2 = TestCustomerData.CustomerColor.Yellow;
                if (GUILayout.Toggle(selectedColor2 == TestCustomerData.CustomerColor.White, "Beyaz", "Button")) selectedColor2 = TestCustomerData.CustomerColor.White;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selectedColor2 == TestCustomerData.CustomerColor.Blue, "Mavi", "Button")) selectedColor2 = TestCustomerData.CustomerColor.Blue;
                if (GUILayout.Toggle(selectedColor2 == TestCustomerData.CustomerColor.Pink, "Pembe", "Button")) selectedColor2 = TestCustomerData.CustomerColor.Pink;
                GUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("Generate Food Dataset from Customers", GUILayout.Height(30)))
            {
                GenerateFoodChunks();
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("--- FOOD CHUNKS (DATASET) ---", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Yemeklerin çıkış sırasını buradan ayarlayabilir, Link ID vererek birbirlerine zincirleyebilirsiniz.", MessageType.None);
        
        SerializedProperty q1 = serializedData.FindProperty("queue1");
        SerializedProperty q2 = serializedData.FindProperty("queue2");
        SerializedProperty q3 = serializedData.FindProperty("queue3");
        
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(q1, new GUIContent("Kuyruk 1 (Sol)"), true);
        EditorGUILayout.PropertyField(q2, new GUIContent("Kuyruk 2 (Orta)"), true);
        EditorGUILayout.PropertyField(q3, new GUIContent("Kuyruk 3 (Sağ)"), true);
        if (EditorGUI.EndChangeCheck())
        {
            serializedData.ApplyModifiedProperties();
        }
    }

    private void OnGridClicked(PointerDownEvent evt)
    {
        if (activeData == null || currentMode != UnifiedMode.Customer) return;

        // Customer modundaysak Conveyor'a tıklanmasını ENGELLE
        evt.StopPropagation(); 

        Vector2 localPos = evt.localPosition;
        int x = Mathf.FloorToInt(localPos.x / conveyorCanvas.CellSize);
        int y = activeData.gridSizeY - 1 - Mathf.FloorToInt(localPos.y / conveyorCanvas.CellSize);

        if (x < 0 || x >= activeData.gridSizeX || y < 0 || y >= activeData.gridSizeY) return;

        if (evt.button == 1 || selectedColor == TestCustomerData.CustomerColor.None) // Sağ tık veya Eraser
        {
            Undo.RecordObject(activeData, "Erase Customer");
            activeData.customers.RemoveAll(c => c.gridX == x && c.gridY == y);
        }
        else if (evt.button == 0) // Sol tık
        {
            Undo.RecordObject(activeData, "Paint Customer");
            activeData.customers.RemoveAll(c => c.gridX == x && c.gridY == y);
            activeData.customers.Add(new TestCustomerData {
                gridX = x,
                gridY = y,
                behavior = selectedBehavior,
                orderColor = selectedColor,
                orderColor2 = selectedColor2
            });
        }

        EditorUtility.SetDirty(activeData);
        RefreshCustomerLayer();
    }

    private void LoadData()
    {
        if (activeData == null)
        {
            conveyorCanvas.style.display = DisplayStyle.None;
            return;
        }
        conveyorCanvas.style.display = DisplayStyle.Flex;

        serializedData = new SerializedObject(activeData);

        if (activeData.conveyorData == null)
        {
            Debug.LogWarning("Bu Level Data'da Conveyor referansı yok!");
        }
        else
        {
            // Canvas'ı arkadaşının Datası ile besle
            conveyorCanvas.Data = activeData.conveyorData;
            conveyorCanvas.GridWidth = activeData.gridSizeX;
            conveyorCanvas.GridHeight = activeData.gridSizeY;
            conveyorCanvas.Resize();
            conveyorCanvas.RebuildCacheAndRepaint();
        }

        RefreshCustomerLayer();
    }

    private void RefreshCustomerLayer()
    {
        customerLayer.Clear();
        if (activeData == null) return;

        foreach (var cst in activeData.customers)
        {
            // Y ekseni UI Toolkit'te terstir
            float px = cst.gridX * conveyorCanvas.CellSize;
            float py = (activeData.gridSizeY - 1 - cst.gridY) * conveyorCanvas.CellSize;

            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.left = px + 5;
            box.style.top = py + 5;
            box.style.width = conveyorCanvas.CellSize - 10;
            box.style.height = conveyorCanvas.CellSize - 10;
            box.style.borderTopLeftRadius = 20;
            box.style.borderTopRightRadius = 20;
            box.style.borderBottomLeftRadius = 20;
            box.style.borderBottomRightRadius = 20;
            box.style.borderTopWidth = 2; box.style.borderBottomWidth = 2;
            box.style.borderLeftWidth = 2; box.style.borderRightWidth = 2;
            box.style.borderTopColor = Color.black; box.style.borderBottomColor = Color.black;
            box.style.borderLeftColor = Color.black; box.style.borderRightColor = Color.black;
            box.style.overflow = Overflow.Hidden;
            box.style.flexDirection = FlexDirection.Row;

            if (cst.behavior == TestCustomerData.CustomerBehavior.Type3)
            {
                var leftHalf = new VisualElement { style = { flexGrow = 1, backgroundColor = GetUnityColor(cst.orderColor) } };
                var rightHalf = new VisualElement { style = { flexGrow = 1, backgroundColor = GetUnityColor(cst.orderColor2) } };
                box.Add(leftHalf);
                box.Add(rightHalf);
            }
            else
            {
                box.style.backgroundColor = GetUnityColor(cst.orderColor);
            }

            var lbl = new Label(cst.behavior == TestCustomerData.CustomerBehavior.Classic ? "C" : (cst.behavior == TestCustomerData.CustomerBehavior.Hidden ? "H" : "2nd"));
            lbl.style.color = Color.black;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.position = Position.Absolute;
            lbl.style.left = 0; lbl.style.top = 0; lbl.style.right = 0; lbl.style.bottom = 0;

            box.Add(lbl);
            customerLayer.Add(box);
        }
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

    private void GenerateFoodChunks()
    {
        Undo.RecordObject(activeData, "Generate Food Dataset");
        
        Dictionary<TestCustomerData.CustomerColor, int> colorCounts = new Dictionary<TestCustomerData.CustomerColor, int>();
        foreach (var cst in activeData.customers)
        {
            if (cst.orderColor != TestCustomerData.CustomerColor.None)
            {
                if (colorCounts.ContainsKey(cst.orderColor)) colorCounts[cst.orderColor]++; else colorCounts[cst.orderColor] = 1;
            }

            if (cst.behavior == TestCustomerData.CustomerBehavior.Type3 && cst.orderColor2 != TestCustomerData.CustomerColor.None)
            {
                if (colorCounts.ContainsKey(cst.orderColor2)) colorCounts[cst.orderColor2]++; else colorCounts[cst.orderColor2] = 1;
            }
        }

        activeData.queue1.Clear();
        activeData.queue2.Clear();
        activeData.queue3.Clear();
        
        int queueAssignCounter = 0;
        foreach (var kvp in colorCounts)
        {
            int remaining = kvp.Value;
            while (remaining > 0)
            {
                int chunkSize = Random.Range(3, 6); 
                if (remaining <= 5) chunkSize = remaining; 
                else if (remaining - chunkSize < 3) chunkSize = remaining - 3;
                if (chunkSize < 3) chunkSize = remaining; 
                
                remaining -= chunkSize;
                
                var chunk = new FoodChunk {
                    color = kvp.Key,
                    amount = chunkSize,
                    linkID = 0
                };
                
                int qIndex = queueAssignCounter % 3;
                if (qIndex == 0) activeData.queue1.Add(chunk);
                else if (qIndex == 1) activeData.queue2.Add(chunk);
                else activeData.queue3.Add(chunk);
                
                queueAssignCounter++;
            }
        }
        
        EditorUtility.SetDirty(activeData);
        serializedData.Update();
        Debug.Log("Dataset oluşturuldu!");
    }
}

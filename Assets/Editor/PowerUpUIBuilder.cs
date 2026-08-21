using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class PowerUpUIBuilder : EditorWindow
{
    [MenuItem("Tools/Build PowerUp UI")]
    public static void BuildPowerUpUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found in the scene. Please create a Canvas first.");
            return;
        }

        // Create Panel
        GameObject panelObj = new GameObject("PowerUpPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panelObj.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = new Vector2(0, 50); // 50 units above bottom
        panelRect.sizeDelta = new Vector2(0, 150); // 150 height

        HorizontalLayoutGroup layout = panelObj.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.spacing = 20;
        layout.padding = new RectOffset(20, 20, 20, 20);

        Undo.RegisterCreatedObjectUndo(panelObj, "Create PowerUp UI");

        // Create Buttons
        CreateButton(panelObj.transform, "Extra Tray", typeof(TestPowerUpExtraTray));
        CreateButton(panelObj.transform, "Pick From Bottom", typeof(TestPowerUpPickFromBottom));
        CreateButton(panelObj.transform, "Shuffle", typeof(TestPowerUpShuffle));
        CreateButton(panelObj.transform, "Ignore Obstacle", typeof(TestPowerUpIgnoreObstacle));

        Debug.Log("PowerUp UI Buttons created successfully at the bottom of the screen!");
    }

    private static void CreateButton(Transform parent, string name, System.Type scriptType)
    {
        GameObject buttonObj = new GameObject("Button_" + name.Replace(" ", ""), typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        // Add the powerup script
        Component powerUpScript = buttonObj.AddComponent(scriptType);

        // Setup the button onClick (Editor-time persistent binding requires UnityEventTools, 
        // for simplicity we just instruct the user or let them add it, but since they have the script on the button,
        // it's easy for them to hook it up, or they can hook it via script at runtime).
        
        // Add Text child
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text text = textObj.GetComponent<Text>();
        text.text = name;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.fontSize = 45;
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 20;
        text.resizeTextMaxSize = 60;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }
}

using UnityEngine;

public class TestMaterialScroller : MonoBehaviour
{
    public enum ScrollAxis
    {
        X_Only,
        Y_Only,
        Both
    }

    [Header("Scroll Settings")]
    public ScrollAxis scrollAxis = ScrollAxis.X_Only;
    public float scrollSpeed = 0.5f;
    public float circularRadius = 0.5f; // 'Both' modunda dairesel kaydırmanın çapı
    public string texturePropertyName = "_BaseMap"; // URP Surface Inputs için standart BaseMap

    private Renderer rend;
    private Material mat;
    private Vector2 currentOffset;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = rend.material;
            if (mat.HasProperty(texturePropertyName))
            {
                currentOffset = mat.GetTextureOffset(texturePropertyName);
            }
            else if (mat.HasProperty("_MainTex"))
            {
                // Eğer _BaseMap yoksa ve _MainTex varsa onu kullan
                texturePropertyName = "_MainTex";
                currentOffset = mat.GetTextureOffset(texturePropertyName);
            }
        }
        else
        {
            Debug.LogWarning("TestMaterialScroller için obje üzerinde Renderer bulunamadı!");
        }
    }

    void Update()
    {
        if (mat == null) return;

        switch (scrollAxis)
        {
            case ScrollAxis.X_Only:
                currentOffset.x += scrollSpeed * Time.deltaTime;
                break;
            case ScrollAxis.Y_Only:
                currentOffset.y += scrollSpeed * Time.deltaTime;
                break;
            case ScrollAxis.Both:
                // Sol alt köşeyi (0,0) baz alarak dairesel yörüngede kaydırır
                currentOffset.x = Mathf.Cos(Time.time * scrollSpeed) * circularRadius;
                currentOffset.y = Mathf.Sin(Time.time * scrollSpeed) * circularRadius;
                break;
        }

        // Seçili (URP _BaseMap vb.) property offset'ini güncelle
        if (mat.HasProperty(texturePropertyName))
        {
            mat.SetTextureOffset(texturePropertyName, currentOffset);
        }
    }
}

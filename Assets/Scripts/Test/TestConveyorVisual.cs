using UnityEngine;

/// <summary>
/// Bant objelerinin veya zeminlerin materyal texture Y offsetini sürekli kaydırarak
/// bandın hareket ettiği (ilerlediği) hissini veren görsel animasyon scripti.
/// </summary>
public class TestConveyorVisual : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Texture kaydırmanın uygulanacağı Renderer. Boş bırakılırsa bu objedeki veya alt objelerdeki ilk Renderer otomatik alınır.")]
    public Renderer targetRenderer;
    
    [Tooltip("Birden fazla materyal varsa hangi materyal slotuna uygulanacağı (0 = İlk Materyal)")]
    public int materialIndex = 0;

    [Header("Scroll Settings")]
    [Tooltip("Texture'ın Y ekseninde kayma hızı")]
    public float scrollSpeedY = 1.0f;

    [Tooltip("İsteğe bağlı: Texture'ın X ekseninde kayma hızı")]
    public float scrollSpeedX = 0.0f;

    [Tooltip("Texture Property adı. Standart/Unlit için '_MainTex', URP/Lit shader'lar için '_BaseMap'")]
    public string texturePropertyName = "_BaseMap";

    [Tooltip("Animasyonun aktif olup olmadığı")]
    public bool isScrolling = true;

    private Material targetMaterial;
    private Vector2 currentOffset;
    private int propertyID;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null && targetRenderer.materials.Length > materialIndex)
        {
            targetMaterial = targetRenderer.materials[materialIndex];
        }

        propertyID = Shader.PropertyToID(texturePropertyName);

        // Eğer belirtilen property yoksa standart _MainTex fallback'ine bak
        if (targetMaterial != null && !targetMaterial.HasProperty(propertyID))
        {
            if (targetMaterial.HasProperty("_MainTex"))
            {
                texturePropertyName = "_MainTex";
                propertyID = Shader.PropertyToID(texturePropertyName);
            }
        }

        if (targetMaterial != null)
        {
            currentOffset = targetMaterial.GetTextureOffset(propertyID);
        }
    }

    private void Update()
    {
        if (!isScrolling || targetMaterial == null) return;

        // Zamana bağlı offset hesaplama (1'e gelince sıfırlanarak sonsuz döngü)
        currentOffset.x = Mathf.Repeat(currentOffset.x + scrollSpeedX * Time.deltaTime, 1f);
        currentOffset.y = Mathf.Repeat(currentOffset.y + scrollSpeedY * Time.deltaTime, 1f);

        targetMaterial.SetTextureOffset(propertyID, currentOffset);
    }

    /// <summary>
    /// Harici olarak hızı değiştirmek isterseniz çağırabilirsiniz.
    /// </summary>
    public void SetSpeed(float speedY, float speedX = 0f)
    {
        scrollSpeedY = speedY;
        scrollSpeedX = speedX;
    }
}

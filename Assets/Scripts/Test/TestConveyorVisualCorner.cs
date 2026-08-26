using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Çeyrek daire konveyör köşe modelleri için tam dairesel (full circle) dokuyu
/// modelin çeyrek daire geometrisi üzerine oturtup merkezinden 360 derece döndüren script.
/// </summary>
public class TestConveyorVisualCorner : MonoBehaviour, RestaurantLoop.Runtime.Conveyor.IConveyorPieceVisual
{
    public enum PlaneAxis
    {
        AutoDetect,   // Modelin en geniş 2 eksenini otomatik bulur (örn: Blender -90X modelleri için X-Y)
        XZ_TopDown,   // Düz zemin modeli (X ve Z eksenleri)
        XY_Local      // -90 derece rotasyonlu modeller (X ve Y eksenleri)
    }

    public enum CornerPivot
    {
        BottomLeft,   // Çeyrek daire iç köşesi Sol-Alt
        BottomRight,  // Çeyrek daire iç köşesi Sağ-Alt
        TopLeft,      // Çeyrek daire iç köşesi Sol-Üst
        TopRight,     // Çeyrek daire iç köşesi Sağ-Üst
        Center,       // Modelin tam geometrik merkezi
        Custom        // Elle girilen özel pivot
    }

    [Header("1. Texture / Materyal Ayarları")]
    [Tooltip("Köşe için Materyal (MeshRenderer üzerinde materyal yoksa otomatik atanır)")]
    public Material cornerMaterial;
    [Tooltip("Köşe için tam yuvarlak texture (örn: conveyorCircularMaterial.png).")]
    public Texture2D cornerTexture;
    [Tooltip("Texture Property adı. URP için '_BaseMap', Standart için '_MainTex'")]
    public string texturePropertyName = "_BaseMap";

    [Header("2. Geometri & Hizalama")]
    [Tooltip("Modelin eğri düzlemi")]
    public PlaneAxis planeAxis = PlaneAxis.AutoDetect;
    [Tooltip("Çeyrek dairenin dönüş merkezi / iç köşesi")]
    public CornerPivot cornerPivot = CornerPivot.BottomLeft;
    [Tooltip("Doku ölçek çarpanı (1 = Tam oturt)")]
    public float textureScale = 1.0f;
    [Tooltip("Doku UV ofseti (Gerekirse ince ayar için)")]
    public Vector2 uvOffset = Vector2.zero;
    [Tooltip("Custom modunda kullanılacak UV pivot noktası (0-1 aralığında)")]
    public Vector2 customPivot = new Vector2(0.5f, 0.5f);

    [Header("3. Dönüş / Animasyon Ayarları")]
    [Tooltip("Animasyon aktif mi?")]
    public bool isRotating = true;
    [Tooltip("Saniyede dönme hızı (Derece cinsinden, örn: 90f)")]
    public float rotationSpeed = 90.0f;
    [Tooltip("Saat yönünde mi dönsün?")]
    public bool isClockwise = true;
    [Tooltip("Dönüş açısının periyodu (Örn: 90f veya 360f)")]
    public float repeatAngle = 360.0f;

    [Header("4. Hedef & Gizmo")]
    [Tooltip("Boş bırakılırsa bu objedeki veya altındaki ilk MeshFilter otomatik bulunur.")]
    public MeshFilter targetMeshFilter;
    [Tooltip("Orijinal model meshi (Otomatik kaydedilir ve kaybolmayı önler)")]
    [SerializeField] private Mesh originalMesh;
    public bool showGizmo = true;
    public Color gizmoColor = Color.cyan;

    private Mesh meshInstance;
    private Vector2[] baseUVs;
    private Vector2[] currentUVs;
    private float currentAngle = 0f;
    private float externalAxisSign = 1f;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            Initialize();
        }
    }

    public void Initialize()
    {
        if (!Application.isPlaying) return;

        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
        }

        if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null) return;
        if (!targetMeshFilter.sharedMesh.isReadable) return;

        Mesh sourceMesh = targetMeshFilter.sharedMesh;
        if (meshInstance == null)
        {
            meshInstance = Instantiate(sourceMesh);
            meshInstance.name = sourceMesh.name + "_RuntimeInstance";
            targetMeshFilter.mesh = meshInstance;
        }

        ApplyTexture();
        GenerateBaseUVs(meshInstance.vertices);
        isInitialized = true;
    }

    private void ApplyTexture()
    {
        Renderer rend = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (rend == null) return;

        if (rend.sharedMaterial == null && cornerMaterial != null)
        {
            rend.material = cornerMaterial;
        }

        if (cornerTexture != null && rend.material != null)
        {
            if (rend.material.HasProperty(texturePropertyName))
                rend.material.SetTexture(texturePropertyName, cornerTexture);
            else if (rend.material.HasProperty("_BaseMap"))
                rend.material.SetTexture("_BaseMap", cornerTexture);
            else if (rend.material.HasProperty("_MainTex"))
                rend.material.SetTexture("_MainTex", cornerTexture);
        }
    }

    private void GenerateBaseUVs(Vector3[] verts)
    {
        if (verts == null || verts.Length == 0) return;

        // Eksen seçimi
        int axisU = 0; // X
        int axisV = 2; // Z

        if (planeAxis == PlaneAxis.XY_Local)
        {
            axisU = 0; // X
            axisV = 1; // Y
        }
        else if (planeAxis == PlaneAxis.AutoDetect)
        {
            // En geniş aralığa sahip 2 ekseni bul
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                minX = Mathf.Min(minX, verts[i].x); maxX = Mathf.Max(maxX, verts[i].x);
                minY = Mathf.Min(minY, verts[i].y); maxY = Mathf.Max(maxY, verts[i].y);
                minZ = Mathf.Min(minZ, verts[i].z); maxZ = Mathf.Max(maxZ, verts[i].z);
            }

            float spanX = maxX - minX;
            float spanY = maxY - minY;
            float spanZ = maxZ - minZ;

            // En küçük span yükseklik (thickness) eksenidir
            if (spanY <= spanX && spanY <= spanZ)
            {
                axisU = 0; axisV = 2; // XZ düzlemi
            }
            else if (spanZ <= spanX && spanZ <= spanY)
            {
                axisU = 0; axisV = 1; // XY düzlemi (-90X modelleri)
            }
            else
            {
                axisU = 1; axisV = 2; // YZ düzlemi
            }
        }

        // Bounding box hesapla
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        for (int i = 0; i < verts.Length; i++)
        {
            float uVal = GetAxisValue(verts[i], axisU);
            float vVal = GetAxisValue(verts[i], axisV);
            minU = Mathf.Min(minU, uVal); maxU = Mathf.Max(maxU, uVal);
            minV = Mathf.Min(minV, vVal); maxV = Mathf.Max(maxV, vVal);
        }

        float spanU = maxU - minU > 0.0001f ? maxU - minU : 1f;
        float spanV = maxV - minV > 0.0001f ? maxV - minV : 1f;
        float radius = Mathf.Max(spanU, spanV);

        Vector2 origin = Vector2.zero;
        Vector2 dirSign = Vector2.one;

        switch (cornerPivot)
        {
            case CornerPivot.BottomLeft:
                origin = new Vector2(minU, minV);
                dirSign = new Vector2(1f, 1f);
                break;
            case CornerPivot.BottomRight:
                origin = new Vector2(maxU, minV);
                dirSign = new Vector2(-1f, 1f);
                break;
            case CornerPivot.TopLeft:
                origin = new Vector2(minU, maxV);
                dirSign = new Vector2(1f, -1f);
                break;
            case CornerPivot.TopRight:
                origin = new Vector2(maxU, maxV);
                dirSign = new Vector2(-1f, -1f);
                break;
            case CornerPivot.Center:
                origin = new Vector2((minU + maxU) * 0.5f, (minV + maxV) * 0.5f);
                dirSign = new Vector2(1f, 1f);
                break;
            case CornerPivot.Custom:
                origin = customPivot;
                dirSign = Vector2.one;
                break;
        }

        baseUVs = new Vector2[verts.Length];
        currentUVs = new Vector2[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            float uVal = GetAxisValue(verts[i], axisU);
            float vVal = GetAxisValue(verts[i], axisV);

            // Dairesel dokunun merkezi (0.5, 0.5) olacak şekilde normalize et
            float normU = ((uVal - origin.x) / radius) * dirSign.x * textureScale;
            float normV = ((vVal - origin.y) / radius) * dirSign.y * textureScale;

            float u = 0.5f + normU * 0.5f + uvOffset.x;
            float v = 0.5f + normV * 0.5f + uvOffset.y;

            baseUVs[i] = new Vector2(u, v);
            currentUVs[i] = baseUVs[i];
        }

        if (meshInstance != null)
        {
            meshInstance.SetUVs(0, baseUVs);
        }
    }

    private static float GetAxisValue(Vector3 v, int axis)
    {
        switch (axis)
        {
            case 0: return v.x;
            case 1: return v.y;
            default: return v.z;
        }
    }

    private void Update()
    {
        if (!isRotating) return;
        StepRotation(Time.deltaTime);
    }

    private void StepRotation(float deltaTime)
    {
        if (this == null) return;

        if (!isInitialized || meshInstance == null || baseUVs == null)
        {
            Initialize();
            if (!isInitialized || meshInstance == null) return;
        }

        float axis = (Mathf.Abs(externalAxisSign) > 0.001f) ? Mathf.Sign(externalAxisSign) : 1f;
        float dir = (isClockwise ? -1f : 1f) * axis;
        currentAngle += dir * rotationSpeed * deltaTime;
        currentAngle = Mathf.Repeat(currentAngle, repeatAngle > 0f ? repeatAngle : 360f);

        float rad = currentAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < baseUVs.Length; i++)
        {
            Vector2 p = baseUVs[i] - center;
            currentUVs[i] = center + new Vector2(
                p.x * cos - p.y * sin,
                p.x * sin + p.y * cos
            );
        }

        meshInstance.uv = currentUVs;
    }

    /// <summary>
    /// ConveyorPieceController entegrasyonu: Bant yönüne göre ters/düz dönüşü otomatik eşler.
    /// </summary>
    public void OnPieceSpawned(float axisSign)
    {
        if (Mathf.Abs(axisSign) > 0.001f)
            externalAxisSign = Mathf.Sign(axisSign);
        else
            externalAxisSign = 1f;
    }

    public void SetRotationSpeed(float speed, bool clockwise = true)
    {
        rotationSpeed = speed;
        isClockwise = clockwise;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && meshInstance != null)
        {
            Destroy(meshInstance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.5f);
    }
}

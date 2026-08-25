using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Çeyrek daire köşe bant modellerinde, tam yuvarlak (Full Circle) bir texture'ı
/// modelin çeyrek daire geometrisiyle (X ve Z eksenlerinde, yukarıdan Y bakışında)
/// otomatik eşleyip merkezden 360 derece döndüren script.
/// </summary>
[ExecuteAlways]
public class TestConveyorVisualCorner : MonoBehaviour
{
    public enum CornerAnchor
    {
        BottomLeft,     // Çeyrek dairenin dönüş merkezi Sol-Alt köşede
        BottomRight,    // Dönüş merkezi Sağ-Alt köşede
        TopLeft,        // Dönüş merkezi Sol-Üst köşede
        TopRight,       // Dönüş merkezi Sağ-Üst köşede
        ManualCustom    // Elle UV pivotu belirle
    }

    public enum ProjectionPlane
    {
        XZ_TopDown,     // Yukarıdan (+Y) bakıldığında zemin düzlemi (X ve Z)
        XY_Vertical     // Dikey düzlem (X ve Y)
    }

    [Header("1. Texture Ayarları")]
    [Tooltip("Köşe için tam yuvarlak texture. Buraya atarsanız materyale otomatik uygulanır.")]
    public Texture2D cornerTexture;

    [Tooltip("Texture Property adı. URP için '_BaseMap', Standart için '_MainTex'")]
    public string texturePropertyName = "_BaseMap";

    [Header("2. Yuvarlak Doku Otomatik Hizalama (X ve Z Ekseninde)")]
    [Tooltip("Tam yuvarlak dokuyu çeyrek daire modelinin köşesine otomatik oturtur")]
    public bool autoAlignFullCircle = true;

    [Tooltip("Düzlem seçimi (Yukarıdan Y ekseninden bakıldığında XZ_TopDown kullanılır)")]
    public ProjectionPlane projectionPlane = ProjectionPlane.XZ_TopDown;

    [Tooltip("Çeyrek dairenin kıvrıldığı iç köşe merkezi")]
    public CornerAnchor cornerAnchor = CornerAnchor.BottomLeft;

    [Header("3. Dönme Ayarları")]
    [Tooltip("Saniyede dönme hızı (Derece cinsinden, örn: 90f)")]
    public float rotationSpeed = 90.0f;

    [Tooltip("Saat yönünde mi dönsün?")]
    public bool isClockwise = true;

    [Tooltip("Dönüş açısının tekrarlanma periyodu. Yuvarlak texture'lar için 360 derece.")]
    public float repeatAngle = 360.0f;

    [Tooltip("Animasyonun aktif olup olmadığı")]
    public bool isRotating = true;

    [Header("4. Manuel UV / Pivot Ayarları (İsteğe Bağlı)")]
    [Tooltip("ManualCustom modunda kullanılacak UV pivot noktası (0-1 aralığında)")]
    public Vector2 customPivot = new Vector2(0.5f, 0.5f);

    [Tooltip("Dokuyu büyütme/küçültme çarpanı")]
    public float textureScale = 1.0f;

    [Tooltip("UV Ofseti")]
    public Vector2 uvOffset = Vector2.zero;

    [Header("5. Hedef & Gizmo")]
    [Tooltip("Boş bırakılırsa bu objedeki veya altındaki ilk MeshFilter otomatik alınır.")]
    public MeshFilter targetMeshFilter;
    public bool showGizmo = true;
    public Color gizmoColor = Color.cyan;

    private Mesh meshInstance;
    private Vector2[] baseCircleUVs;
    private Vector2[] workingUVs;
    private float currentAngle = 0f;
    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnValidate()
    {
        isInitialized = false;
        Initialize();
    }

    public void Initialize()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
        }

        if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null) return;

#if UNITY_EDITOR
        if (!targetMeshFilter.sharedMesh.isReadable)
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(targetMeshFilter.sharedMesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }
#endif

        if (!targetMeshFilter.sharedMesh.isReadable) return;

        if (meshInstance == null)
        {
            meshInstance = Instantiate(targetMeshFilter.sharedMesh);
            meshInstance.name = targetMeshFilter.sharedMesh.name + "_CircleCornerInstance";
            targetMeshFilter.mesh = meshInstance;
        }

        ApplyTexture();
        GenerateBaseUVs();
        isInitialized = true;
    }

    private void ApplyTexture()
    {
        if (cornerTexture == null) return;
        Renderer rend = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (rend != null && rend.sharedMaterial != null)
        {
            if (rend.material.HasProperty(texturePropertyName))
                rend.material.SetTexture(texturePropertyName, cornerTexture);
            else if (rend.material.HasProperty("_MainTex"))
                rend.material.SetTexture("_MainTex", cornerTexture);
        }
    }

    private void GenerateBaseUVs()
    {
        if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null) return;
        Vector3[] verts = targetMeshFilter.sharedMesh.vertices;
        if (verts == null || verts.Length == 0) return;

        if (!autoAlignFullCircle || cornerAnchor == CornerAnchor.ManualCustom)
        {
            baseCircleUVs = targetMeshFilter.sharedMesh.uv;
            if (baseCircleUVs == null || baseCircleUVs.Length == 0)
                baseCircleUVs = new Vector2[verts.Length];
        }
        else
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                float vx = verts[i].x;
                float vz = projectionPlane == ProjectionPlane.XZ_TopDown ? verts[i].z : verts[i].y;

                minX = Mathf.Min(minX, vx);
                maxX = Mathf.Max(maxX, vx);
                minZ = Mathf.Min(minZ, vz);
                maxZ = Mathf.Max(maxZ, vz);
            }

            float spanX = maxX - minX > 0.001f ? maxX - minX : 1f;
            float spanZ = maxZ - minZ > 0.001f ? maxZ - minZ : 1f;
            float radius = Mathf.Max(spanX, spanZ);

            Vector2 origin = Vector2.zero;
            Vector2 dirSign = Vector2.one;

            switch (cornerAnchor)
            {
                case CornerAnchor.BottomLeft:
                    origin = new Vector2(minX, minZ);
                    dirSign = new Vector2(1f, 1f);
                    break;
                case CornerAnchor.BottomRight:
                    origin = new Vector2(maxX, minZ);
                    dirSign = new Vector2(-1f, 1f);
                    break;
                case CornerAnchor.TopLeft:
                    origin = new Vector2(minX, maxZ);
                    dirSign = new Vector2(1f, -1f);
                    break;
                case CornerAnchor.TopRight:
                    origin = new Vector2(maxX, maxZ);
                    dirSign = new Vector2(-1f, -1f);
                    break;
            }

            baseCircleUVs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float vx = verts[i].x;
                float vz = projectionPlane == ProjectionPlane.XZ_TopDown ? verts[i].z : verts[i].y;

                // origin noktasını tam yuvarlak texture'ın (0.5, 0.5) merkezine oturtuyoruz
                float normX = ((vx - origin.x) / radius) * dirSign.x * textureScale;
                float normZ = ((vz - origin.y) / radius) * dirSign.y * textureScale;

                float u = 0.5f + normX * 0.5f + uvOffset.x;
                float v = 0.5f + normZ * 0.5f + uvOffset.y;

                baseCircleUVs[i] = new Vector2(u, v);
            }
        }

        workingUVs = new Vector2[baseCircleUVs.Length];
        System.Array.Copy(baseCircleUVs, workingUVs, baseCircleUVs.Length);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!isRotating) return;

        if (!isInitialized || meshInstance == null || baseCircleUVs == null)
        {
            Initialize();
            if (!isInitialized || meshInstance == null) return;
        }

        float dir = isClockwise ? -1f : 1f;
        currentAngle += dir * rotationSpeed * Time.deltaTime;
        currentAngle = Mathf.Repeat(currentAngle, repeatAngle > 0f ? repeatAngle : 360f);

        Vector2 pivot = (autoAlignFullCircle && cornerAnchor != CornerAnchor.ManualCustom)
            ? new Vector2(0.5f, 0.5f)
            : customPivot;

        float rad = currentAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < baseCircleUVs.Length; i++)
        {
            Vector2 p = baseCircleUVs[i] - pivot;
            Vector2 rotated = new Vector2(
                p.x * cos - p.y * sin,
                p.x * sin + p.y * cos
            );
            workingUVs[i] = pivot + rotated;
        }

        meshInstance.uv = workingUVs;
    }

    private void OnDestroy()
    {
        if (meshInstance != null && Application.isPlaying)
        {
            Destroy(meshInstance);
        }
    }

    public void SetRotationSpeed(float speed, bool clockwise = true)
    {
        rotationSpeed = speed;
        isClockwise = clockwise;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;
        DrawGizmo();
    }

    private void DrawGizmo()
    {
        Vector3 centerPos = transform.position;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(centerPos + Vector3.up * 0.05f, 0.08f);

        // Çember yayı (Zemin X ve Z ekseninde)
        int segments = 32;
        float radius = 0.5f;
        Vector3 prev = centerPos + transform.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = centerPos + (transform.right * Mathf.Cos(a) + transform.forward * Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}

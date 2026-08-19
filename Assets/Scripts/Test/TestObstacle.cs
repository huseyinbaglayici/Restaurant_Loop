using UnityEngine;

/// <summary>
/// Bu script, sahnedeki engelleri (Obstacle) belirlemek için kullanılır.
/// GameManager müşteri yerleştirirken Physics.OverlapSphere ile kontrol ettiğinde,
/// çarptığı objede bu script varsa o noktaya müşteri yerleştirmez.
/// </summary>
public class TestObstacle : MonoBehaviour
{
    // Şu an için sadece bir etiket (Marker) görevi görüyor.
    // İleride engelin boyutu, türü vb. ayarlar buraya eklenebilir.
}

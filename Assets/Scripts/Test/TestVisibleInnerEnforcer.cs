using UnityEngine;

/// <summary>
/// eyllscene2 için özel olarak yazılmış kod.
/// Sahnedeki tüm müşterileri bulur ve eğer içerideki bir müşteri ise (isServeable == false),
/// gri olmak yerine kendi sipariş rengini göstermesini sağlar.
/// Baloncuk (bubble) zaten ana kod tarafından gizlendiği için baloncuk çıkmaz.
/// Sadece bu scripti eyllscene2 sahnesinde boş bir objeye (veya GameManager'a) atmanız yeterlidir.
/// </summary>
public class TestVisibleInnerEnforcer : MonoBehaviour
{
    void LateUpdate()
    {
        // Sahnedeki tüm TestCustomer objelerini bul
        TestCustomer[] allCustomers = FindObjectsOfType<TestCustomer>();
        
        foreach (var customer in allCustomers)
        {
            // Eğer içeride bekleyen (sırası gelmemiş) bir müşteriyse
            if (!customer.isServeable)
            {
                Renderer rnd = customer.GetComponent<Renderer>();
                if (rnd != null)
                {
                    // Ana koddaki Color.gray ezilir ve kendi rengi gösterilir
                    rnd.material.color = customer.orderColor;
                }
            }
        }
    }
}

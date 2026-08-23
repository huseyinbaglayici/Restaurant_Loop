using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hem UI Butonlarına hem de 3D sahne objelerine tatlı, elastik (cozy) bir basılma ve zıplama hissi katar.
/// </summary>
public class CozyButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Press Scale Settings")]
    [Tooltip("Basılı tutulduğundaki ölçek")]
    public float pressedScaleMultiplier = 0.88f;

    [Tooltip("Tıklandığında ekstra zıplama/büyüme oranı")]
    public float punchScaleMultiplier = 1.12f;

    [Tooltip("Animasyon hızı")]
    public float animationSpeed = 16f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Coroutine activeScaleRoutine;
    private bool isPointerDown = false;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        // Eğer UI Button varsa Transition'ı 'None' yaparak Unity'nin sönük geçişi yerine bu animasyonu öne çıkarırız
        Button btn = GetComponent<Button>();
        if (btn != null && btn.transition == Selectable.Transition.ColorTint)
        {
            btn.transition = Selectable.Transition.None;
        }
    }

    private void OnEnable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
    }

    // --- UI Butonları İçin Pointer Olayları ---
    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        SetTargetScale(originalScale * pressedScaleMultiplier);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        SetTargetScale(originalScale);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        StartPunchEffect();
    }

    // --- 3D Sahne Objeleri İçin Mouse Olayları (Collider varsa çalışır) ---
    private void OnMouseDown()
    {
        isPointerDown = true;
        SetTargetScale(originalScale * pressedScaleMultiplier);
    }

    private void OnMouseUp()
    {
        isPointerDown = false;
        StartPunchEffect();
    }

    private void SetTargetScale(Vector3 scale)
    {
        targetScale = scale;
        if (activeScaleRoutine == null && gameObject.activeInHierarchy)
        {
            activeScaleRoutine = StartCoroutine(AnimateScale());
        }
    }

    private void StartPunchEffect()
    {
        if (gameObject.activeInHierarchy)
        {
            if (activeScaleRoutine != null) StopCoroutine(activeScaleRoutine);
            activeScaleRoutine = StartCoroutine(PunchRoutine());
        }
    }

    private IEnumerator AnimateScale()
    {
        while (Vector3.Distance(transform.localScale, targetScale) > 0.005f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
            yield return null;
        }
        transform.localScale = targetScale;
        activeScaleRoutine = null;
    }

    private IEnumerator PunchRoutine()
    {
        // 1. Zıplama / Büyüme (Punch Out)
        Vector3 punchScale = originalScale * punchScaleMultiplier;
        float t = 0;
        while (t < 0.1f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, punchScale, t / 0.1f);
            yield return null;
        }

        // 2. Elastik Orijinal Boyuta Dönüş (Settle Back)
        t = 0;
        Vector3 startScale = transform.localScale;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(startScale, originalScale, t / 0.15f);
            yield return null;
        }

        transform.localScale = originalScale;
        activeScaleRoutine = null;
    }
}

using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Shows "available/total" for tepsi_dagitici's own tray pool (TrayDispenserAnchor). Plain Update
    // poll instead of a signal/event — TrayDispenserAnchor has no change notifications, and this is just
    // two int comparisons a frame, cheap enough not to need one. Update() only starts running after
    // Zenject's SceneContext finishes injection/Initialize (it holds scene objects inactive until then),
    // so the very first frame already reads the post-reset pool state (5/5).
    public class TrayDispenserCounterUI : MonoBehaviour
    {
        [Inject] private TrayDispenserAnchor _trayAnchor;

        [SerializeField] private TMP_Text _text;

        private int _lastAvailable = -1;
        private int _lastTotal = -1;

        private void Awake()
        {
            if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
        }

        private void Update()
        {
            if (_trayAnchor == null || _text == null) return;

            int available = _trayAnchor.AvailableCount;
            int total = _trayAnchor.TotalCount;
            if (available == _lastAvailable && total == _lastTotal) return;

            _lastAvailable = available;
            _lastTotal = total;
            _text.SetText("{0}/{1}", available, total);
        }
    }
}

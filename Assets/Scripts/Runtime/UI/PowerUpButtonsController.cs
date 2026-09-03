using DG.Tweening;
using RestaurantLoop.Audio;
using RestaurantLoop.Economy;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Tutorial;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class PowerUpButtonsController : MonoBehaviour
    {
        [Header("Shuffle Button")]
        [SerializeField] private Button _buttonShuffle;
        [SerializeField] private GameObject _lockShuffle;
        [SerializeField] private TMP_Text _countShuffleText;
        [SerializeField] private GameObject _plusShuffle;

        [Header("Pick From Bottom Button")]
        [SerializeField] private Button _buttonPickFromBottom;
        [SerializeField] private GameObject _lockPickFromBottom;
        [SerializeField] private TMP_Text _countPickFromBottomText;
        [SerializeField] private GameObject _plusPickFromBottom;

        [Header("Extra Tray Button")]
        [SerializeField] private Button _buttonExtraTray;
        [SerializeField] private GameObject _lockExtraTray;
        [SerializeField] private TMP_Text _countExtraTrayText;
        [SerializeField] private GameObject _plusExtraTray;

        private PowerUpService _powerUpService;
        private TutorialInputGate _gate;
        private SignalBus _signalBus;
        private CustomerConsumeService _consumeService;
        private TutorialTargetRegistry _registry;
        private IPowerUpInventoryService _inventory;
        private CurrentLevelProvider _currentLevelProvider;
        private PowerUpSettings _settings;
        private RestaurantLoop.Economy.IEconomyService _economyService;
        private IAudioService _audioService;
        private AudioLibrary _audioLibrary;

        [Inject]
        public void Construct(
            PowerUpService powerUpService,
            TutorialInputGate gate,
            SignalBus signalBus,
            [Inject(Optional = true)] TutorialTargetRegistry registry = null,
            [Inject(Optional = true)] IPowerUpInventoryService inventory = null,
            [Inject(Optional = true)] CurrentLevelProvider currentLevelProvider = null,
            [Inject(Optional = true)] CustomerConsumeService consumeService = null,
            [Inject(Optional = true)] PowerUpSettings settings = null,
            [Inject(Optional = true)] RestaurantLoop.Economy.IEconomyService economyService = null,
            [Inject(Optional = true)] IAudioService audioService = null,
            [Inject(Optional = true)] AudioLibrary audioLibrary = null)
        {
            _powerUpService = powerUpService;
            _gate = gate;
            _signalBus = signalBus;
            _registry = registry;
            _inventory = inventory;
            _currentLevelProvider = currentLevelProvider;
            _consumeService = consumeService;
            _settings = settings ?? Resources.Load<PowerUpSettings>("PowerUpSettings");
            _economyService = economyService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;

            RegisterTutorialTargets();
        }

        private void OnEnable()
        {
            RegisterTutorialTargets();
        }

        private void Start()
        {
            RegisterTutorialTargets();

            // Auto-resolve child visual references if unassigned in Inspector
            AutoResolveReferences();

            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded += HandleLevelEnded;
            }

            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += RefreshAllSlots;
            }

            if (_economyService != null)
            {
                _economyService.OnCoinsChanged += HandleCoinsChanged;
            }

            _signalBus?.Subscribe<LevelResumedSignal>(HandleLevelResumed);

            // Wire button click handlers
            if (_buttonShuffle != null)
            {
                _buttonShuffle.onClick.AddListener(OnShuffleClicked);
            }

            if (_buttonPickFromBottom != null)
            {
                _buttonPickFromBottom.onClick.AddListener(OnPickFromBottomClicked);
            }

            if (_buttonExtraTray != null)
            {
                _buttonExtraTray.onClick.AddListener(OnExtraTrayClicked);
            }

            RefreshAllSlots();
        }

        private void OnDestroy()
        {
            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded -= HandleLevelEnded;
            }

            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= RefreshAllSlots;
            }

            if (_economyService != null)
            {
                _economyService.OnCoinsChanged -= HandleCoinsChanged;
            }

            _signalBus?.Unsubscribe<LevelResumedSignal>(HandleLevelResumed);
        }

        private void RegisterTutorialTargets()
        {
            if (_registry == null) return;

            if (_buttonShuffle == null) _buttonShuffle = FindChildByKeywords(transform, "shuffle", "karistir")?.GetComponent<Button>();
            if (_buttonPickFromBottom == null) _buttonPickFromBottom = FindChildByKeywords(transform, "pickfrombottom", "bottom", "dip", "alttan")?.GetComponent<Button>();
            if (_buttonExtraTray == null) _buttonExtraTray = FindChildByKeywords(transform, "extratray", "tray", "tepsi", "ekstra")?.GetComponent<Button>();

            if (_buttonShuffle != null) _registry.Register(PowerUpButtonId.Shuffle, (RectTransform)_buttonShuffle.transform);
            if (_buttonPickFromBottom != null) _registry.Register(PowerUpButtonId.PickFromBottom, (RectTransform)_buttonPickFromBottom.transform);
            if (_buttonExtraTray != null) _registry.Register(PowerUpButtonId.ExtraTray, (RectTransform)_buttonExtraTray.transform);
        }

        private void HandleCoinsChanged(int coins) => RefreshAllSlots();

        private void OnShuffleClicked()
        {
            if (!IsPowerUpUsable(PowerUpButtonId.Shuffle)) return;

            int count = _inventory != null ? _inventory.GetCount(PowerUpButtonId.Shuffle) : 0;
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();

            if (count <= 0 && !infinite)
            {
                int price = _settings != null ? _settings.GetGoldPrice(PowerUpButtonId.Shuffle) : 100;
                if (_economyService != null && !_economyService.TrySpendCoins(price))
                {
                    PlayNotEnoughCoinsFeedback(_countShuffleText);
                    return;
                }
                _inventory?.AddCount(PowerUpButtonId.Shuffle, 1);
                PlayBuySound();
            }

            if (_inventory == null || _inventory.TryUse(PowerUpButtonId.Shuffle))
            {
                _powerUpService.ActivateShuffle();
                FireActionPerformed(PowerUpButtonId.Shuffle);
                RefreshSlot(PowerUpButtonId.Shuffle, _buttonShuffle, _lockShuffle, _countShuffleText, _plusShuffle);
            }
        }

        private void OnPickFromBottomClicked()
        {
            if (!IsPowerUpUsable(PowerUpButtonId.PickFromBottom)) return;

            int count = _inventory != null ? _inventory.GetCount(PowerUpButtonId.PickFromBottom) : 0;
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();

            if (count <= 0 && !infinite)
            {
                int price = _settings != null ? _settings.GetGoldPrice(PowerUpButtonId.PickFromBottom) : 200;
                if (_economyService != null && !_economyService.TrySpendCoins(price))
                {
                    PlayNotEnoughCoinsFeedback(_countPickFromBottomText);
                    return;
                }
                _inventory?.AddCount(PowerUpButtonId.PickFromBottom, 1);
                PlayBuySound();
            }

            if (_inventory == null || _inventory.TryUse(PowerUpButtonId.PickFromBottom))
            {
                _powerUpService.ActivatePickFromBottom();
                FireActionPerformed(PowerUpButtonId.PickFromBottom);
                RefreshSlot(PowerUpButtonId.PickFromBottom, _buttonPickFromBottom, _lockPickFromBottom, _countPickFromBottomText, _plusPickFromBottom);
            }
        }

        private void OnExtraTrayClicked()
        {
            if (!IsPowerUpUsable(PowerUpButtonId.ExtraTray)) return;
            if (_powerUpService != null && !_powerUpService.CanActivateExtraTray()) return;

            int count = _inventory != null ? _inventory.GetCount(PowerUpButtonId.ExtraTray) : 0;
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();

            if (count <= 0 && !infinite)
            {
                int price = _settings != null ? _settings.GetGoldPrice(PowerUpButtonId.ExtraTray) : 300;
                if (_economyService != null && !_economyService.TrySpendCoins(price))
                {
                    PlayNotEnoughCoinsFeedback(_countExtraTrayText);
                    return;
                }
                _inventory?.AddCount(PowerUpButtonId.ExtraTray, 1);
                PlayBuySound();
            }

            if (_powerUpService != null && _powerUpService.ActivateExtraTray())
            {
                _inventory?.TryUse(PowerUpButtonId.ExtraTray);
                FireActionPerformed(PowerUpButtonId.ExtraTray);
                RefreshSlot(PowerUpButtonId.ExtraTray, _buttonExtraTray, _lockExtraTray, _countExtraTrayText, _plusExtraTray);
            }
        }

        private void PlayBuySound()
        {
            if (_audioService != null && _audioLibrary != null)
            {
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            }
        }

        private void PlayNotEnoughCoinsFeedback(TMP_Text text)
        {
            if (_audioService != null && _audioLibrary != null)
            {
                var sfx = _audioLibrary.NotEnoughCoinsSfx != null ? _audioLibrary.NotEnoughCoinsSfx : _audioLibrary.FailSfx;
                if (sfx != null) _audioService.PlaySfx(sfx);
            }

            if (text != null)
            {
                DG.Tweening.DOTween.Kill(text);
                DG.Tweening.DOTween.Kill(text.transform);

                Color originalColor = _settings != null ? _settings.BadgeTextColor : Color.white;
                text.color = new Color(1f, 0.25f, 0.25f, 1f); // Red warning
                text.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.45f, 20, 1f, false)
                    .SetUpdate(true);
                text.DOColor(originalColor, 0.45f)
                    .SetUpdate(true);
            }
        }

        private bool IsPowerUpUsable(PowerUpButtonId id)
        {
            int currentLevel = GetCurrentLevelId();
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();
            if (!infinite && _inventory != null && !_inventory.IsUnlocked(id, currentLevel)) return false;
            if (_gate != null && !_gate.CanClickPowerUpButton(id)) return false;

            if (id == PowerUpButtonId.ExtraTray && _powerUpService != null && !_powerUpService.CanActivateExtraTray())
            {
                return false;
            }

            return true;
        }

        public void RefreshAllSlots()
        {
            RefreshSlot(PowerUpButtonId.Shuffle, _buttonShuffle, _lockShuffle, _countShuffleText, _plusShuffle);
            RefreshSlot(PowerUpButtonId.PickFromBottom, _buttonPickFromBottom, _lockPickFromBottom, _countPickFromBottomText, _plusPickFromBottom);
            RefreshSlot(PowerUpButtonId.ExtraTray, _buttonExtraTray, _lockExtraTray, _countExtraTrayText, _plusExtraTray);
        }

        private void RefreshSlot(
            PowerUpButtonId id,
            Button button,
            GameObject lockObj,
            TMP_Text countText,
            GameObject plusObj)
        {
            if (button == null) return;

            int currentLevel = GetCurrentLevelId();
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();
            bool isUnlocked = infinite || (_inventory == null || _inventory.IsUnlocked(id, currentLevel));
            var def = _settings?.GetDefinition(id);

            var coinIcon = GetOrCreateCoinIcon(button);

            if (!isUnlocked)
            {
                // 1) LOCKED: Set locked sprite if available, show lock icon, hide count/plus, disable interaction.
                if (def != null && def.LockedSprite != null && button.image != null)
                {
                    button.image.sprite = def.LockedSprite;
                }
                if (lockObj != null) lockObj.SetActive(true);
                if (countText != null) countText.gameObject.SetActive(false);
                if (plusObj != null) plusObj.SetActive(false);
                if (coinIcon != null) coinIcon.gameObject.SetActive(false);
                button.interactable = false;
            }
            else
            {
                // 2) UNLOCKED: Set unlocked sprite if available, hide lock icon, button is interactable.
                if (def != null && def.UnlockedSprite != null && button.image != null)
                {
                    button.image.sprite = def.UnlockedSprite;
                }
                if (lockObj != null) lockObj.SetActive(false);

                bool canUseEffect = id != PowerUpButtonId.ExtraTray || (_powerUpService == null || _powerUpService.CanActivateExtraTray());
                button.interactable = canUseEffect;

                int count = _inventory != null ? _inventory.GetCount(id) : 0;
                if (plusObj != null) plusObj.SetActive(false);

                Vector2 baseOffset = _settings != null ? _settings.BadgeTextOffset : new Vector2(40f, -40f);
                if (def != null && def.CustomTextOffset != Vector2.zero)
                {
                    baseOffset = def.CustomTextOffset;
                }

                Vector2 textSize = _settings != null ? _settings.BadgeTextSize : new Vector2(160f, 60f);

                if (count > 0)
                {
                    if (coinIcon != null) coinIcon.gameObject.SetActive(false);

                    if (countText != null)
                    {
                        countText.gameObject.SetActive(true);
                        countText.enableWordWrapping = false;
                        countText.overflowMode = TextOverflowModes.Overflow;
                        countText.rectTransform.sizeDelta = textSize;
                        countText.text = count.ToString();
                        countText.rectTransform.anchoredPosition = baseOffset;

                        if (_settings != null)
                        {
                            if (_settings.BadgeFontSize > 0) countText.fontSize = _settings.BadgeFontSize;
                            countText.color = _settings.BadgeTextColor;
                            if (_settings.BadgeFontAsset != null) countText.font = _settings.BadgeFontAsset;
                        }
                    }
                }
                else
                {
                    // Count is 0: Show gold price and coin icon instead of "+"
                    int price = _settings != null ? _settings.GetGoldPrice(id) : 100;

                    if (coinIcon != null)
                    {
                        coinIcon.gameObject.SetActive(true);
                        if (_settings != null && _settings.GoldIcon != null)
                        {
                            coinIcon.sprite = _settings.GoldIcon;
                        }

                        Vector2 coinOffset = _settings != null ? _settings.GoldIconOffset : new Vector2(-28f, 0f);
                        Vector2 coinSize = _settings != null ? _settings.GoldIconSize : new Vector2(34f, 34f);

                        coinIcon.rectTransform.anchoredPosition = baseOffset + coinOffset;
                        coinIcon.rectTransform.sizeDelta = coinSize;
                    }

                    if (countText != null)
                    {
                        countText.gameObject.SetActive(true);
                        countText.enableWordWrapping = false;
                        countText.overflowMode = TextOverflowModes.Overflow;
                        countText.rectTransform.sizeDelta = textSize;
                        countText.text = price.ToString();
                        countText.rectTransform.anchoredPosition = baseOffset + new Vector2(16f, 0f);

                        if (_settings != null)
                        {
                            if (_settings.BadgeFontSize > 0) countText.fontSize = _settings.BadgeFontSize;
                            countText.color = _settings.BadgeTextColor;
                            if (_settings.BadgeFontAsset != null) countText.font = _settings.BadgeFontAsset;
                        }
                    }
                }
            }
        }

        private UnityEngine.UI.Image GetOrCreateCoinIcon(Button button)
        {
            if (button == null) return null;
            var found = button.transform.Find("BadgeCoinIcon");
            if (found != null) return found.GetComponent<UnityEngine.UI.Image>();

            var iconObj = new GameObject("BadgeCoinIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconObj.transform.SetParent(button.transform, false);

            var rt = iconObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(34f, 34f);

            var img = iconObj.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            if (_settings != null && _settings.GoldIcon != null)
            {
                img.sprite = _settings.GoldIcon;
            }

            return img;
        }

        private void AutoResolveReferences()
        {
            TryResolveSlotVisuals(_buttonShuffle, ref _lockShuffle, ref _countShuffleText, ref _plusShuffle);
            TryResolveSlotVisuals(_buttonPickFromBottom, ref _lockPickFromBottom, ref _countPickFromBottomText, ref _plusPickFromBottom);
            TryResolveSlotVisuals(_buttonExtraTray, ref _lockExtraTray, ref _countExtraTrayText, ref _plusExtraTray);
        }

        private void TryResolveSlotVisuals(
            Button button,
            ref GameObject lockObj,
            ref TMP_Text countText,
            ref GameObject plusObj)
        {
            if (button == null) return;

            Transform t = button.transform;
            if (lockObj == null)
            {
                var found = FindChildByKeywords(t, "lock", "kilit");
                if (found != null) lockObj = found.gameObject;
            }

            if (countText == null)
            {
                countText = button.GetComponentInChildren<TMP_Text>(true);
            }

            // If no text component exists on the button, create one directly via code
            if (countText == null)
            {
                var textObj = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(button.transform, false);

                var rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = _settings != null ? _settings.BadgeTextSize : new Vector2(160f, 60f);

                Vector2 offset = _settings != null ? _settings.BadgeTextOffset : new Vector2(40f, -40f);
                rt.anchoredPosition = offset;

                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
                if (_settings != null)
                {
                    if (_settings.BadgeFontSize > 0) tmp.fontSize = _settings.BadgeFontSize;
                    tmp.color = _settings.BadgeTextColor;
                    if (_settings.BadgeFontAsset != null) tmp.font = _settings.BadgeFontAsset;
                }

                countText = tmp;
            }
            else
            {
                countText.enableWordWrapping = false;
                countText.overflowMode = TextOverflowModes.Overflow;
                countText.rectTransform.sizeDelta = _settings != null ? _settings.BadgeTextSize : new Vector2(160f, 60f);
            }

            if (plusObj == null)
            {
                var found = FindChildByKeywords(t, "plus", "arti", "add");
                if (found != null) plusObj = found.gameObject;
            }
        }

        private Transform FindChildByKeywords(Transform parent, params string[] keywords)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                string name = child.name.ToLowerInvariant();
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (name.Contains(keywords[k])) return child;
                }
            }
            return null;
        }

        private int GetCurrentLevelId()
        {
            return _currentLevelProvider?.Current != null ? _currentLevelProvider.Current.LevelId : 1;
        }

        private void HandleLevelEnded(bool won)
        {
            gameObject.SetActive(false);
        }

        private void HandleLevelResumed()
        {
            gameObject.SetActive(true);
            RefreshAllSlots();
        }

        private void FireActionPerformed(PowerUpButtonId id) =>
            _signalBus?.Fire(new TutorialActionPerformedSignal { Kind = TutorialHoverTargetKind.PowerUpButton, PowerUpButton = id });
    }
}


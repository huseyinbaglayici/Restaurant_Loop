using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.Runtime.UI
{
    // Visuals of one power-up button: sprite, interactable state and the count / price badge.
    // Built by PowerUpButtonsController; the badge text and coin icon are created under the button at runtime.
    public class PowerUpSlotView
    {
        private const string CoinIconName = "BadgeCoinIcon";
        private static readonly Color NotEnoughCoinsColor = new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly Vector2 PriceTextShift = new Vector2(16f, 0f);

        private readonly PowerUpSettings _settings;
        private readonly TMP_Text _countText;
        private readonly Image _coinIcon;

        public PowerUpButtonId Id { get; }
        public Button Button { get; }

        public PowerUpSlotView(PowerUpButtonId id, Button button, PowerUpSettings settings)
        {
            Id = id;
            Button = button;
            _settings = settings;

            _countText = button.GetComponentInChildren<TMP_Text>(true);
            if (_countText == null) _countText = CreateBadgeText();
            _coinIcon = GetOrCreateCoinIcon();
        }

        public void Refresh(bool isUnlocked, bool canUseEffect, int count, int price)
        {
            var def = _settings.GetDefinition(Id);

            if (!isUnlocked)
            {
                if (def != null && def.LockedSprite != null && Button.image != null) Button.image.sprite = def.LockedSprite;
                _countText.gameObject.SetActive(false);
                _coinIcon.gameObject.SetActive(false);
                Button.interactable = false;
                return;
            }

            if (def != null && def.UnlockedSprite != null && Button.image != null) Button.image.sprite = def.UnlockedSprite;
            Button.interactable = canUseEffect;

            Vector2 baseOffset = def != null && def.CustomTextOffset != Vector2.zero ? def.CustomTextOffset : _settings.BadgeTextOffset;

            if (count > 0)
            {
                _coinIcon.gameObject.SetActive(false);
                ShowBadgeText(count.ToString(), baseOffset);
                return;
            }

            // Out of charges: show the gold price with a coin icon next to it
            _coinIcon.gameObject.SetActive(true);
            if (_settings.GoldIcon != null) _coinIcon.sprite = _settings.GoldIcon;
            _coinIcon.rectTransform.anchoredPosition = baseOffset + _settings.GoldIconOffset;
            _coinIcon.rectTransform.sizeDelta = _settings.GoldIconSize;

            ShowBadgeText(price.ToString(), baseOffset + PriceTextShift);
        }

        // Red flash + shake on the price text.
        public void PlayNotEnoughCoins()
        {
            DOTween.Kill(_countText);
            DOTween.Kill(_countText.transform);

            _countText.color = NotEnoughCoinsColor;
            _countText.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.45f, 20, 1f, false).SetUpdate(true);
            _countText.DOColor(_settings.BadgeTextColor, 0.45f).SetUpdate(true);
        }

        private void ShowBadgeText(string text, Vector2 position)
        {
            _countText.gameObject.SetActive(true);
            _countText.textWrappingMode = TextWrappingModes.NoWrap;
            _countText.overflowMode = TextOverflowModes.Overflow;
            _countText.rectTransform.sizeDelta = _settings.BadgeTextSize;
            _countText.text = text;
            _countText.rectTransform.anchoredPosition = position;
            ApplyBadgeStyle(_countText);
        }

        private void ApplyBadgeStyle(TMP_Text text)
        {
            if (_settings.BadgeFontSize > 0) text.fontSize = _settings.BadgeFontSize;
            text.color = _settings.BadgeTextColor;
            if (_settings.BadgeFontAsset != null) text.font = _settings.BadgeFontAsset;
        }

        private TMP_Text CreateBadgeText()
        {
            var textObj = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(Button.transform, false);

            var rt = CenterAnchored(textObj, _settings.BadgeTextSize);
            rt.anchoredPosition = _settings.BadgeTextOffset;

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            ApplyBadgeStyle(tmp);
            return tmp;
        }

        private Image GetOrCreateCoinIcon()
        {
            var found = Button.transform.Find(CoinIconName);
            if (found != null) return found.GetComponent<Image>();

            var iconObj = new GameObject(CoinIconName, typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(Button.transform, false);
            CenterAnchored(iconObj, new Vector2(34f, 34f));

            var img = iconObj.GetComponent<Image>();
            img.raycastTarget = false;
            if (_settings.GoldIcon != null) img.sprite = _settings.GoldIcon;
            return img;
        }

        private static RectTransform CenterAnchored(GameObject go, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return rt;
        }
    }
}

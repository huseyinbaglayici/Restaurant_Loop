using System;
using RestaurantLoop.Economy;
using RestaurantLoop.Lives;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class MainMenuHeaderPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _heartAmountText;
        [SerializeField] private TextMeshProUGUI _remainingTimeText;

        private IEconomyService _economyService;
        private ILivesService _livesService;

        [Inject]
        public void Construct(
            [Inject(Optional = true)] IEconomyService economyService = null,
            [Inject(Optional = true)] ILivesService livesService = null)
        {
            _economyService = economyService;
            _livesService = livesService;
        }

        private void Start()
        {
            if (_economyService != null)
            {
                _economyService.OnCoinsChanged += OnCoinsChanged;
                UpdateCoins(_economyService.Coins);
            }

            if (_livesService != null)
            {
                _livesService.OnLivesChanged += OnLivesChanged;
                _livesService.OnTimerTick += OnTimerTick;
                UpdateLives(_livesService.CurrentLives);
                UpdateTimer(_livesService.TimeUntilNextLife);
            }
        }

        private void OnDestroy()
        {
            if (_economyService != null)
            {
                _economyService.OnCoinsChanged -= OnCoinsChanged;
            }

            if (_livesService != null)
            {
                _livesService.OnLivesChanged -= OnLivesChanged;
                _livesService.OnTimerTick -= OnTimerTick;
            }
        }

        private void OnCoinsChanged(int coins) => UpdateCoins(coins);

        private void OnLivesChanged(int lives)
        {
            UpdateLives(lives);
            if (_livesService != null)
            {
                UpdateTimer(_livesService.TimeUntilNextLife);
            }
        }

        private void OnTimerTick(TimeSpan remaining) => UpdateTimer(remaining);

        private void UpdateCoins(int coins)
        {
            if (_moneyText != null)
            {
                _moneyText.text = coins.ToString();
            }
        }

        private void UpdateLives(int lives)
        {
            if (_heartAmountText != null)
            {
                _heartAmountText.text = lives.ToString();
            }
        }

        private void UpdateTimer(TimeSpan remaining)
        {
            if (_remainingTimeText == null) return;

            if (_livesService != null && _livesService.IsFull)
            {
                _remainingTimeText.text = "Full";
            }
            else
            {
                _remainingTimeText.text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            }
        }
    }
}

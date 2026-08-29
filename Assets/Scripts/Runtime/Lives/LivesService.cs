using System;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Lives
{
    public class LivesService : ILivesService, IInitializable, ITickable
    {
        public const string LivesSaveKey = "Player_Lives";
        public const string LastRegenTimeSaveKey = "Player_LastRegenTime";

        public const int DefaultMaxLives = 5;
        public const int RegenIntervalSeconds = 20 * 60; // 20 minutes (1200 seconds)

        private int _currentLives = DefaultMaxLives;
        private DateTime _lastRegenTimeUtc = DateTime.UtcNow;
        private float _tickTimer = 0f;

        public int CurrentLives => _currentLives;
        public int MaxLives => DefaultMaxLives;
        public bool IsFull => _currentLives >= DefaultMaxLives;
        public bool HasLives => _currentLives > 0;

        public TimeSpan TimeUntilNextLife
        {
            get
            {
                if (IsFull) return TimeSpan.Zero;

                var elapsed = DateTime.UtcNow - _lastRegenTimeUtc;
                var remainingSeconds = RegenIntervalSeconds - elapsed.TotalSeconds;
                if (remainingSeconds < 0) remainingSeconds = 0;
                return TimeSpan.FromSeconds(remainingSeconds);
            }
        }

        public event Action<int> OnLivesChanged;
        public event Action<TimeSpan> OnTimerTick;

        public void Initialize()
        {
            _currentLives = ES3.KeyExists(LivesSaveKey) ? ES3.Load<int>(LivesSaveKey) : DefaultMaxLives;

            if (ES3.KeyExists(LastRegenTimeSaveKey))
            {
                long ticks = ES3.Load<long>(LastRegenTimeSaveKey);
                _lastRegenTimeUtc = new DateTime(ticks, DateTimeKind.Utc);
            }
            else
            {
                _lastRegenTimeUtc = DateTime.UtcNow;
            }

            RecalculateRegeneration();
        }

        public void Tick()
        {
            _tickTimer += Time.unscaledDeltaTime;
            if (_tickTimer >= 1f)
            {
                _tickTimer = 0f;
                if (!IsFull)
                {
                    RecalculateRegeneration();
                    OnTimerTick?.Invoke(TimeUntilNextLife);
                }
            }
        }

        public bool ConsumeLife()
        {
            if (_currentLives <= 0) return false;

            if (IsFull)
            {
                _lastRegenTimeUtc = DateTime.UtcNow;
            }

            _currentLives--;
            Save();
            OnLivesChanged?.Invoke(_currentLives);
            return true;
        }

        public void AddLives(int amount)
        {
            if (amount <= 0) return;

            _currentLives = Mathf.Min(DefaultMaxLives, _currentLives + amount);
            if (IsFull)
            {
                _lastRegenTimeUtc = DateTime.UtcNow;
            }

            Save();
            OnLivesChanged?.Invoke(_currentLives);
        }

        private void RecalculateRegeneration()
        {
            if (IsFull)
            {
                _lastRegenTimeUtc = DateTime.UtcNow;
                return;
            }

            var elapsed = DateTime.UtcNow - _lastRegenTimeUtc;
            if (elapsed.TotalSeconds >= RegenIntervalSeconds)
            {
                int gainedLives = (int)(elapsed.TotalSeconds / RegenIntervalSeconds);
                _currentLives = Mathf.Min(DefaultMaxLives, _currentLives + gainedLives);

                if (IsFull)
                {
                    _lastRegenTimeUtc = DateTime.UtcNow;
                }
                else
                {
                    _lastRegenTimeUtc = _lastRegenTimeUtc.AddSeconds(gainedLives * RegenIntervalSeconds);
                }

                Save();
                OnLivesChanged?.Invoke(_currentLives);
            }
        }

        private void Save()
        {
            ES3.Save(LivesSaveKey, _currentLives);
            ES3.Save(LastRegenTimeSaveKey, _lastRegenTimeUtc.Ticks);
        }
    }
}

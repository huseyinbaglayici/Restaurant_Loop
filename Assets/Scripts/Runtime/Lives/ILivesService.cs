using System;

namespace RestaurantLoop.Runtime.Lives
{
    public interface ILivesService
    {
        int CurrentLives { get; }
        int MaxLives { get; }
        TimeSpan TimeUntilNextLife { get; }
        bool IsFull { get; }
        bool HasLives { get; }

        bool ConsumeLife();
        void AddLives(int amount);

        event Action<int> OnLivesChanged;
        event Action<TimeSpan> OnTimerTick;
    }
}

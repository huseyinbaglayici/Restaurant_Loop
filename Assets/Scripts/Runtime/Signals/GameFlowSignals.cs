using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.Signals
{
    public class LevelEndedSignal
    {
        public int LevelIndex;
    }

    public class PlayRequestedSignal
    {
        public ConveyorLevelDataSO Level;
    }

    public class LoadingStartedSignal
    {
    }

    public class GameplaySceneReadySignal
    {
    }
}

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

    // unload + reload the same level, sequenced (no menu flash, no overlap with the old instance)
    public class RestartLevelSignal
    {
    }

    public class LoadingStartedSignal
    {
    }

    public class GameplaySceneReadySignal
    {
    }
}

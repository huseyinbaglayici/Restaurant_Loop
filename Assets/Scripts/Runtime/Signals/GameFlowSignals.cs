using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.Runtime.Signals
{
    public class LevelEndedSignal
    {
        public int LevelIndex;
    }

    public class PlayRequestedSignal
    {
        public ConveyorLevelDataSO Level;
    }

    // Unload and reload the same level, in sequence (no menu flash, no overlap with the old instance).
    public class RestartLevelSignal
    {
    }

    // The player chose Keep Playing on the fail popup.
    public class LevelResumedSignal
    {
    }

    public class LoadingStartedSignal
    {
    }

    public class GameplaySceneReadySignal
    {
    }

    // Cold boot only: MainMenuScene finished loading. SceneLoader continues straight into GameplayScene,
    // so this is not a "hide the loading screen" signal.
    public class MainMenuReadySignal
    {
    }

    // The menu is the visible destination (GameplayScene finished unloading, or cold boot with no lives).
    // Hides the loading screen and reveals the menu content.
    public class MenuReadySignal
    {
    }

    // Loading overlay started fading out: the scene under it is now visible.
    public class LoadingHiddenSignal
    {
    }
}

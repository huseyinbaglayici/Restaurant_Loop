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

    // Fired once by Bootstrapper when the very first additive MainMenuScene load completes. This is
    // an internal boot-continuation trigger, NOT a "loading screen can hide now" signal — SceneLoader
    // reacts to it by immediately continuing on into GameplayScene, so on cold boot the player goes
    // straight from the loading screen into gameplay and never sees the menu underneath it.
    public class MainMenuReadySignal
    {
    }

    // Fired once the menu is actually the visible destination — after GameplayScene finishes
    // unloading following a LevelEndedSignal (Claim). Hides the shared loading screen and reveals
    // the menu content. Never fires during boot (MainMenuScene only loads once, at boot, and that
    // load is covered by MainMenuReadySignal's continue-into-gameplay chain instead).
    public class MenuReadySignal
    {
    }
}

using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.SceneFlow
{
    // ProjectContext-scoped — set once (MainMenu Play button, via PlayRequestedSignal) before
    // GameplayScene loads, read once by ConveyorInstaller when GameplayScene's container builds.
    public class CurrentLevelProvider
    {
        public ConveyorLevelDataSO Current { get; set; }
    }
}

using RestaurantLoop.SceneFlow;
using TMPro;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelOverlayPresenter : IInitializable
    {
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly TextMeshProUGUI _levelText;

        public LevelOverlayPresenter(CurrentLevelProvider currentLevelProvider, TextMeshProUGUI levelText)
        {
            _currentLevelProvider = currentLevelProvider;
            _levelText = levelText;
        }

        public void Initialize()
        {
            _levelText.text = $"Level {_currentLevelProvider.Current.LevelId}";
        }
    }
}

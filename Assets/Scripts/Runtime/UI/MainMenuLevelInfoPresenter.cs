using RestaurantLoop.Runtime.Data;
using RestaurantLoop.SceneFlow;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class MainMenuLevelInfoPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _levelInfoText;
        [SerializeField] private TextMeshProUGUI _difficultyText;

        private CurrentLevelProvider _currentLevelProvider;

        [Inject]
        public void Construct(CurrentLevelProvider currentLevelProvider)
        {
            _currentLevelProvider = currentLevelProvider;
        }

        private void Start()
        {
            var level = _currentLevelProvider.Current;
            if (level == null)
                return;

            _levelInfoText.text = $"Level {level.LevelId}";

            if (_difficultyText == null) return;

            bool isHard = level.Difficulty is LevelDifficulty.Hard or LevelDifficulty.SuperHard;
            _difficultyText.gameObject.SetActive(isHard || level.Difficulty == LevelDifficulty.Bonus);
            _difficultyText.text = level.Difficulty switch
            {
                LevelDifficulty.Bonus => "Bonus",
                LevelDifficulty.Hard => "Hard",
                LevelDifficulty.SuperHard => "Super Hard",
                _ => string.Empty
            };
        }
    }
}

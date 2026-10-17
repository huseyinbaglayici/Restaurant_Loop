using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "RestaurantLoop/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("Music")]
        public AudioClip MenuMusic;
        public AudioClip GameplayMusic;

        [Header("Ambience")]
        public AudioClip GameplayAmbience;

        [Header("SFX")]
        public AudioClip DefaultClickSfx;
        [Tooltip("Tapping a food in the queue or the exit stack.")]
        public AudioClip FoodClickSfx;
        [Tooltip("A food is sent to the belt.")]
        public AudioClip FoodSendSfx;
        [Tooltip("A customer eats a food.")]
        public AudioClip CustomerEatSfx;
        [Tooltip("A food stack is fully used up.")]
        public AudioClip FoodGroupCompletedSfx;
        [Tooltip("Belt loop, plays while at least one food is on the belt.")]
        public AudioClip ConveyorLoopSfx;
        [Tooltip("A hidden customer reveals its order.")]
        public AudioClip CustomerRevealSfx;
        [Tooltip("A food leaves the belt and lands on the exit rack.")]
        public AudioClip FoodDropToRackSfx;
        [Tooltip("Win celebration (logo and confetti).")]
        public AudioClip CelebrationSfx;
        [Tooltip("Level Complete panel opens.")]
        public AudioClip WinSfx;
        [Tooltip("Level fail panel opens.")]
        public AudioClip FailSfx;
        [Tooltip("Not enough coins (power-up purchase, Keep Playing).")]
        public AudioClip NotEnoughCoinsSfx;
        [Tooltip("Shuffle power-up whoosh")]
        public AudioClip ShuffleSfx;
        [Tooltip("Pick From Bottom / Extra Tray activation pop")]
        public AudioClip PowerUpSfx;
    }
}

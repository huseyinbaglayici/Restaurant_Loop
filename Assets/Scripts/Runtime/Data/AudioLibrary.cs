using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "RestaurantLoop/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("Music")]
        public AudioClip MenuMusic;
        public AudioClip GameplayMusic;
        [Range(0f, 1f)] public float MusicVolume = 0.4f;

        [Header("Ambience")]
        public AudioClip GameplayAmbience;
        [Range(0f, 1f)] public float AmbienceVolume = 0.15f;

        [Header("SFX")]
        public AudioClip DefaultClickSfx;
        [Tooltip("Stack'teki veya raftaki objelere basınca çalacak ses")]
        public AudioClip FoodClickSfx;
        [Tooltip("Yemek objesi banda giderken çalacak ses")]
        public AudioClip FoodSendSfx;
        [Tooltip("Customer yemeği yediğinde çalacak ses")]
        public AudioClip CustomerEatSfx;
        [Tooltip("Bir yemek grubu (stack) tamamen bitip tükendiğinde çalacak ses")]
        public AudioClip FoodGroupCompletedSfx;
        [Tooltip("Bant üzerinde en az bir yemek varken sürekli (loop) çalacak bant çalışma sesi")]
        public AudioClip ConveyorLoopSfx;
        [Tooltip("Yemek banttan çıkıp rafa düştüğünde çalacak ses")]
        public AudioClip FoodDropToRackSfx;
        [Range(0f, 1f)] public float SfxVolume = 0.2f;
    }
}

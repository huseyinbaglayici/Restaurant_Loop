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
        [Range(0f, 1f)] public float SfxVolume = 0.2f;
    }
}

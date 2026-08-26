using UnityEngine;

namespace RestaurantLoop.Audio
{
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        void PlayMusic(AudioClip clip);
        void StopMusic();
        void PlayAmbience(AudioClip clip);
        void StopAmbience();
        void PlayConveyorLoop(AudioClip clip = null);
        void StopConveyorLoop();
    }
}

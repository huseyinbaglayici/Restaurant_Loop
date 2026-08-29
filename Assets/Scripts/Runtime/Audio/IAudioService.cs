using UnityEngine;

namespace RestaurantLoop.Audio
{
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        void PlayMusic(AudioClip clip);
        void StopMusic();
        void PauseMusic();
        void ResumeMusic();
        void PlayAmbience(AudioClip clip);
        void StopAmbience();
        void PauseAmbience();
        void ResumeAmbience();
        void PlayConveyorLoop(AudioClip clip = null);
        void StopConveyorLoop();
    }
}

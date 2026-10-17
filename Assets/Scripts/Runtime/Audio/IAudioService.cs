using System;
using UnityEngine;

namespace RestaurantLoop.Runtime.Audio
{
    public interface IAudioService
    {
        float MusicVolume { get; }
        float SfxVolume { get; }
        event Action<float, float> OnAudioSettingsChanged;

        void SetMusicVolume(float volume);
        void SetSfxVolume(float volume);

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

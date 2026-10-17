using System;
using UnityEngine;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Belt loop sound while any food is on the belt. Re-asserted every frame so a stop from elsewhere
    // (e.g. level end) is picked back up. Bound right after ConveyorVisualService to read this frame's state.
    public class ConveyorAudio : ITickable, IDisposable
    {
        private readonly ConveyorVisualService _visualService;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        public ConveyorAudio(ConveyorVisualService visualService, AudioService audioService, AudioLibrary audioLibrary)
        {
            _visualService = visualService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Tick()
        {
            // Paused under a fail/complete panel: the belt is frozen, so stay silent.
            if (_visualService.HasBeltVisuals && Time.timeScale > 0f)
            {
                _audioService.PlayConveyorLoop(_audioLibrary.ConveyorLoopSfx);
            }
            else
            {
                _audioService.StopConveyorLoop();
            }
        }

        public void Dispose()
        {
            _audioService.StopConveyorLoop();
        }
    }
}

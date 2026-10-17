using System;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Belt loop sound: plays while any food is drawn on the belt, silent otherwise. Re-asserted every
    // frame (both calls are no-ops when already in that state), so a stop from elsewhere — e.g. level
    // end — is picked back up exactly like before. Bind right after ConveyorVisualService so it reads
    // the same frame's belt state.
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
            if (_visualService.HasBeltVisuals)
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

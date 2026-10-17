using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
{
    public class AudioInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            var audioSettings = Resources.Load<AudioLibrary>("AudioLibrary");
            if (audioSettings == null)
            {
                Debug.LogError("[AudioInstaller] AudioLibrary.asset not found under Resources — audio not bound.");
                return;
            }

            Container.BindInstance(audioSettings).AsSingle();
            Container.BindInterfacesTo<AudioService>().AsSingle();
        }
    }
}

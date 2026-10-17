using RestaurantLoop.Runtime.Audio;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Keeps the music / sfx sliders of a settings panel (menu or gameplay) in sync with AudioService.
    public class AudioSettingsUIController : MonoBehaviour
    {
        [Header("Sliders (found among children by name if left empty)")]
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;

        private AudioService _audioService;
        private bool _isUpdatingInternally;

        [Inject]
        public void Construct(AudioService audioService)
        {
            _audioService = audioService;
            SyncSlidersWithService();
        }

        // Safety net only: the subscription normally lives between OnEnable and OnDisable.
        private void OnDestroy()
        {
            if (_audioService != null) _audioService.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
        }

        private void Awake()
        {
            if (_musicSlider == null || _sfxSlider == null)
            {
                var sliders = GetComponentsInChildren<Slider>(true);
                foreach (var s in sliders)
                {
                    string sName = s.name.ToLower();
                    if (_musicSlider == null && sName.Contains("music")) _musicSlider = s;
                    else if (_sfxSlider == null && (sName.Contains("sound") || sName.Contains("sfx"))) _sfxSlider = s;
                }
            }

            if (_musicSlider != null)
            {
                _musicSlider.minValue = 0f;
                _musicSlider.maxValue = 1f;
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.minValue = 0f;
                _sfxSlider.maxValue = 1f;
            }
        }

        private void OnEnable()
        {
            if (_audioService != null)
            {
                _audioService.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
                _audioService.OnAudioSettingsChanged += HandleAudioSettingsChanged;
                SyncSlidersWithService();
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
                _musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
                _sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }

            SyncSlidersWithService();
        }

        private void OnDisable()
        {
            if (_audioService != null)
            {
                _audioService.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            }
        }

        private void Start()
        {
            SyncSlidersWithService();
        }

        private void SyncSlidersWithService()
        {
            if (_audioService == null) return;

            _isUpdatingInternally = true;
            if (_musicSlider != null)
            {
                _musicSlider.value = _audioService.MusicVolume;
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.value = _audioService.SfxVolume;
            }
            _isUpdatingInternally = false;
        }

        private void HandleAudioSettingsChanged(float musicVol, float sfxVol)
        {
            _isUpdatingInternally = true;
            if (_musicSlider != null && !Mathf.Approximately(_musicSlider.value, musicVol))
            {
                _musicSlider.value = musicVol;
            }

            if (_sfxSlider != null && !Mathf.Approximately(_sfxSlider.value, sfxVol))
            {
                _sfxSlider.value = sfxVol;
            }
            _isUpdatingInternally = false;
        }

        private void OnMusicSliderChanged(float value)
        {
            if (_isUpdatingInternally || _audioService == null) return;
            _audioService.SetMusicVolume(value);
        }

        private void OnSfxSliderChanged(float value)
        {
            if (_isUpdatingInternally || _audioService == null) return;
            _audioService.SetSfxVolume(value);
        }
    }
}

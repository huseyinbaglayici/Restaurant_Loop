using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Win flow view: celebration (logo tween + confetti, also previewed in Edit Mode) then the Level Complete panel.
    // The celebration stays in this MonoBehaviour: it needs [ExecuteAlways] lifecycle, coroutines and its serialized
    // scene refs. Rewards/saving live in LevelProgression, board cleanup in LevelBoardCleanup.
    [ExecuteAlways]
    public class LevelCompleteUIController : MonoBehaviour
    {
        [Header("Editor Preview & Loop")]
        [Tooltip("İşaretlendiğinde sahne görünümünde (Edit Mode) bile sürekli logo ve konfeti animasyonunu döngü halinde oynatır.")]
        [SerializeField] private bool _previewLoopInEditMode = true;

        [Header("Celebration / Win Sequence")]
        [Tooltip("Oyun bittiğinde ilk açılacak kutlama paneli.")]
        [SerializeField] private GameObject _celebrationPanel;
        [Tooltip("Aşağıdan yukarıya süzülecek logo veya tebrik görseli.")]
        [SerializeField] private RectTransform _logoTransform;
        [Tooltip("Logonun hedeflenen büyüklüğü (Scale). Varsayılan olarak 1.3x.")]
        [SerializeField] private Vector3 _logoTargetScale = new Vector3(1.35f, 1.35f, 1.35f);
        [Tooltip("Logonun ilk başlayacağı ekran dışı / alt pozisyon ofseti.")]
        [SerializeField] private Vector2 _logoStartOffset = new Vector2(0, -600f);
        [Tooltip("Logonun animasyon süresi.")]
        [SerializeField] private float _logoMoveDuration = 1.2f;
        [SerializeField] private Ease _logoEase = Ease.OutBack;
        [Tooltip("Logo belirdikten sonra kaç saniye sonra asıl Level Success paneline geçilsin?")]
        [SerializeField] private float _celebrationDuration = 2.5f;

        [Header("Confetti VFX")]
        [Tooltip("Tetiklenecek Konfeti VFX Prefab'leri veya Sahnedeki Particle System'ler (Confetti_blast_multicolor veya Confetti_directional_multicolor).")]
        [SerializeField] private List<ParticleSystem> _confettiParticles = new List<ParticleSystem>();
        [Tooltip("Eğer prefab olarak spawn etmek istersen buraya atayabilirsin.")]
        [SerializeField] private GameObject[] _confettiPrefabs;
        [Tooltip("Konfetilerin doğacağı pozisyonlar (Canvas üzeri veya 3D dünya noktaları).")]
        [SerializeField] private Transform[] _confettiSpawnPoints;
        [Tooltip("Konfeti parçacıklarının / efekt objesinin boyut çarpanı (Scale). Varsayılan 1.5x.")]
        [SerializeField] private float _confettiScaleMultiplier = 1.5f;
        [Tooltip("Toplam kaç dalga/kez konfeti patlasın? (Örn: 3-4 kez ardı ardına).")]
        [SerializeField] private int _burstCount = 3;
        [Tooltip("Dalgalar arasındaki bekleme süresi (saniye).")]
        [SerializeField] private float _burstInterval = 0.35f;
        [Tooltip("Konfetilerin doğacağı rastgele pozisyon yayılma yarıçapı.")]
        [SerializeField] private float _randomSpreadRadius = 150f;
        [Tooltip("UI üzerinde veya arkasında görünmesi için Sorting Order (Logonun arkasında görünmesi için düşük bir değer, örn: 10 veya logonun canvas order'ından küçük bir değer).")]
        [SerializeField] private int _confettiSortingOrder = 10;
        [Tooltip("Kullanılacak Sorting Layer adı (Genellikle 'UI' veya 'Default').")]
        [SerializeField] private string _confettiSortingLayerName = "UI";

        [Header("Level Complete / Success Panel")]
        [SerializeField] private GameObject _panelCompleted;
        [SerializeField] private Button _buttonNext;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _coinRewardText;

        // Injected only in Play Mode; stay null when the celebration runs in Edit Mode, hence the null checks.
        private CustomerConsumeService _consumeService;
        private SignalBus _signalBus;
        private CurrentLevelProvider _currentLevelProvider;
        private AudioService _audioService;
        private AudioLibrary _audioLibrary;
        private LevelProgression _levelProgression;
        private LevelBoardCleanup _boardCleanup;

        private Vector2 _logoTargetPosition;
        private Sequence _celebrationSequence;
        private readonly List<GameObject> _spawnedConfettiInstances = new List<GameObject>();
#if UNITY_EDITOR
        private readonly List<GameObject> _editorSpawnedFx = new List<GameObject>();
#endif

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            SignalBus signalBus,
            CurrentLevelProvider currentLevelProvider,
            AudioService audioService,
            AudioLibrary audioLibrary,
            LevelProgression levelProgression,
            LevelBoardCleanup boardCleanup)
        {
            _consumeService = consumeService;
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _levelProgression = levelProgression;
            _boardCleanup = boardCleanup;
        }

        private void Awake()
        {
            if (_logoTransform != null)
            {
                _logoTargetPosition = _logoTransform.anchoredPosition;
                SetupLogoCanvas();
            }

            if (Application.isPlaying)
            {
                // Stop and hide any confetti already placed in the scene
                if (_confettiParticles != null)
                {
                    foreach (var ps in _confettiParticles)
                    {
                        if (ps != null)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            ps.gameObject.SetActive(false);
                        }
                    }
                }

                if (_celebrationPanel != null) _celebrationPanel.SetActive(false);
                if (_panelCompleted != null) _panelCompleted.SetActive(false);
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && _previewLoopInEditMode)
            {
                StartEditorPreviewLoop();
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                StopEditorPreview();
            }
#endif
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (_consumeService != null) _consumeService.OnLevelEnded += HandleLevelEnded;
                if (_buttonNext != null) _buttonNext.onClick.AddListener(OnNextClicked);
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && _consumeService != null)
            {
                _consumeService.OnLevelEnded -= HandleLevelEnded;
            }
            _celebrationSequence?.Kill();
        }

        // Own Canvas with overrideSorting so the logo always draws in front of the confetti
        private void SetupLogoCanvas()
        {
            Canvas logoCanvas = _logoTransform.GetComponent<Canvas>();
            if (logoCanvas == null)
            {
                logoCanvas = _logoTransform.gameObject.AddComponent<Canvas>();
            }
            logoCanvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(_confettiSortingLayerName))
            {
                logoCanvas.sortingLayerName = _confettiSortingLayerName;
            }
            logoCanvas.sortingOrder = _confettiSortingOrder + 20;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (_previewLoopInEditMode)
                {
                    StartEditorPreviewLoop();
                }
                else
                {
                    StopEditorPreview();
                }
            }
        }

        [ContextMenu("Test Celebration Once")]
        public void TestCelebrationOnce()
        {
            StartCelebrationSequence();
        }

        public void StartEditorPreviewLoop()
        {
            StopEditorPreview();

            if (_celebrationPanel != null) _celebrationPanel.SetActive(true);
            if (_panelCompleted != null) _panelCompleted.SetActive(false);

            if (_logoTransform != null)
            {
                _logoTargetPosition = _logoTransform.anchoredPosition;
                SetupLogoCanvas();
                PlayLogoLoopAnimation();
            }

            PlayConfettiEffects();
        }

        public void StopEditorPreview()
        {
            _celebrationSequence?.Kill();
            if (_logoTransform != null)
            {
                _logoTransform.DOKill();
                _logoTransform.anchoredPosition = _logoTargetPosition;
                _logoTransform.localScale = _logoTargetScale;
            }

            for (int i = _editorSpawnedFx.Count - 1; i >= 0; i--)
            {
                if (_editorSpawnedFx[i] != null)
                {
                    DestroyImmediate(_editorSpawnedFx[i]);
                }
            }
            _editorSpawnedFx.Clear();
        }

        private void PlayLogoLoopAnimation()
        {
            if (_logoTransform == null) return;

            _celebrationSequence?.Kill();
            _celebrationSequence = DOTween.Sequence().SetUpdate(true);

            _logoTransform.anchoredPosition = _logoTargetPosition + _logoStartOffset;
            _logoTransform.localScale = Vector3.zero;

            _celebrationSequence.Append(_logoTransform.DOAnchorPos(_logoTargetPosition, _logoMoveDuration).SetEase(_logoEase));
            _celebrationSequence.Join(_logoTransform.DOScale(_logoTargetScale, _logoMoveDuration).SetEase(Ease.OutBack));
            _celebrationSequence.AppendInterval(1.5f);
            _celebrationSequence.Append(_logoTransform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack));
            _celebrationSequence.AppendCallback(() =>
            {
                PlayConfettiEffects();
            });
            _celebrationSequence.SetLoops(-1);
        }
#endif

        private void HandleLevelEnded(bool won)
        {
            if (won)
            {
                StartCelebrationSequence();
            }
        }

        private void StartCelebrationSequence()
        {
            // Stop background audio and play the celebration sfx
            if (_audioService != null)
            {
                _audioService.StopMusic();
                _audioService.StopAmbience();
                _audioService.StopConveyorLoop();

                if (_audioLibrary != null)
                {
                    AudioClip clipToPlay = _audioLibrary.CelebrationSfx != null ? _audioLibrary.CelebrationSfx : _audioLibrary.WinSfx;
                    if (clipToPlay != null)
                    {
                        _audioService.PlaySfx(clipToPlay);
                    }
                }
            }

            if (_celebrationPanel != null)
            {
                _celebrationPanel.SetActive(true);
            }

            // Logo rises from below with a scale bounce, then pulses
            if (_logoTransform != null)
            {
                _logoTransform.anchoredPosition = _logoTargetPosition + _logoStartOffset;
                _logoTransform.localScale = Vector3.zero;

                _celebrationSequence?.Kill();
                _celebrationSequence = DOTween.Sequence().SetUpdate(true);

                _celebrationSequence.Append(_logoTransform.DOAnchorPos(_logoTargetPosition, _logoMoveDuration).SetEase(_logoEase));
                _celebrationSequence.Join(_logoTransform.DOScale(_logoTargetScale, _logoMoveDuration).SetEase(Ease.OutBack));
                _celebrationSequence.OnComplete(() =>
                {
                    _logoTransform.DOScale(_logoTargetScale * 1.06f, 0.6f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                });
            }

            PlayConfettiEffects();

            StartCoroutine(CelebrationToSuccessRoutine());
        }

        private void PlayConfettiEffects()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(SpawnConfettiBurstsRoutine());
            }
            else
            {
                // Edit Mode: single instant wave
                SpawnSingleConfettiWave();
            }
        }

        private IEnumerator SpawnConfettiBurstsRoutine()
        {
            for (int burst = 0; burst < _burstCount; burst++)
            {
                SpawnSingleConfettiWave();
                if (burst < _burstCount - 1 && _burstInterval > 0)
                {
                    yield return new WaitForSecondsRealtime(_burstInterval);
                }
            }
        }

        private void SpawnSingleConfettiWave()
        {
            // Particle systems already placed in the scene
            if (_confettiParticles != null)
            {
                foreach (var ps in _confettiParticles)
                {
                    if (ps != null)
                    {
                        ApplySortingOrder(ps.gameObject);
                        ps.transform.localScale = Vector3.one * _confettiScaleMultiplier;
                        ps.gameObject.SetActive(true);
                        ps.Play();
                    }
                }
            }

            // Prefabs spawned at the spawn points (or the logo), with a small random spread
            if (_confettiPrefabs != null && _confettiPrefabs.Length > 0)
            {
                for (int i = 0; i < _confettiPrefabs.Length; i++)
                {
                    var prefab = _confettiPrefabs[i];
                    if (prefab == null) continue;

                    Vector3 spawnPos = Vector3.zero;
                    Quaternion spawnRot = Quaternion.identity;

                    if (_confettiSpawnPoints != null && i < _confettiSpawnPoints.Length && _confettiSpawnPoints[i] != null)
                    {
                        spawnPos = _confettiSpawnPoints[i].position;
                        spawnRot = _confettiSpawnPoints[i].rotation;
                    }
                    else if (_logoTransform != null)
                    {
                        spawnPos = _logoTransform.position;
                    }

                    if (_randomSpreadRadius > 0)
                    {
                        Vector2 randomOffset = Random.insideUnitCircle * _randomSpreadRadius;
                        spawnPos += new Vector3(randomOffset.x, randomOffset.y, 0f);
                    }

                    GameObject fxInstance = Instantiate(prefab, spawnPos, spawnRot);
                    fxInstance.transform.localScale = prefab.transform.localScale * _confettiScaleMultiplier;

                    // First sibling under the celebration panel so it renders behind the logo
                    if (_celebrationPanel != null)
                    {
                        fxInstance.transform.SetParent(_celebrationPanel.transform, true);
                        fxInstance.transform.SetAsFirstSibling();
                    }

                    ApplySortingOrder(fxInstance);

                    if (Application.isPlaying)
                    {
                        _spawnedConfettiInstances.Add(fxInstance);
                        StartCoroutine(DestroyFxUnscaled(fxInstance, 4f));
                    }
                    else
                    {
#if UNITY_EDITOR
                        _editorSpawnedFx.Add(fxInstance);
                        fxInstance.hideFlags = HideFlags.DontSave;
#endif
                    }
                }
            }
        }

        private IEnumerator DestroyFxUnscaled(GameObject fx, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (fx != null)
            {
                _spawnedConfettiInstances.Remove(fx);
                Destroy(fx);
            }
        }

        private void DestroySpawnedConfetti()
        {
            for (int i = _spawnedConfettiInstances.Count - 1; i >= 0; i--)
            {
                if (_spawnedConfettiInstances[i] != null)
                {
                    Destroy(_spawnedConfettiInstances[i]);
                }
            }
            _spawnedConfettiInstances.Clear();
        }

        private void ApplySortingOrder(GameObject obj)
        {
            if (obj == null) return;

            var renderers = obj.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
            {
                if (!string.IsNullOrEmpty(_confettiSortingLayerName))
                {
                    r.sortingLayerName = _confettiSortingLayerName;
                }
                r.sortingOrder = _confettiSortingOrder;
            }
        }

        private IEnumerator CelebrationToSuccessRoutine()
        {
            yield return new WaitForSecondsRealtime(_celebrationDuration);

            if (_celebrationPanel != null)
            {
                _celebrationPanel.SetActive(false);
            }

            ShowCompleted();
        }

        private void ShowCompleted()
        {
            Time.timeScale = 0f;
            _panelCompleted.SetActive(true);

            UpdateTexts();
            _levelProgression?.AwardLevelCoins();

            if (_audioService != null && _audioLibrary != null && _audioLibrary.WinSfx != null)
            {
                _audioService.PlaySfx(_audioLibrary.WinSfx);
            }
        }

        private void UpdateTexts()
        {
            if (_currentLevelProvider == null || _currentLevelProvider.Current == null) return;

            int levelId = _currentLevelProvider.Current.LevelId;
            if (_levelText != null)
            {
                _levelText.text = $"Level {levelId}";
            }

            int reward = LevelProgression.GetRewardCoins(_currentLevelProvider.Current.Difficulty);
            if (_coinRewardText != null)
            {
                _coinRewardText.text = reward.ToString();
            }
        }

        private void OnNextClicked()
        {
            _panelCompleted.SetActive(false);
            Time.timeScale = 1f;

            _boardCleanup.ClearBeltAndExitStack();
            DestroySpawnedConfetti();
            _levelProgression.AdvanceSavedLevel();

            _signalBus.Fire<LevelEndedSignal>();
        }
    }
}

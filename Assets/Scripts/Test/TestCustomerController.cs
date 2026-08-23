using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class TestCustomerController : MonoBehaviour
    {
        public enum HairMode
        {
            /// <summary>
            /// Kafa kemiğinin altına dizdiğiniz hazır saç objelerini açıp kapatır (En garanti yöntem; pozisyon/rotasyon kaymaz).
            /// </summary>
            ChildVariantsToggle,
            /// <summary>
            /// Kafa kemiği (Hair Parent) altına prefab'ı dinamik instantiate eder.
            /// </summary>
            InstantiatePrefab
        }

        [Header("Mode Selection")]
        [SerializeField] private HairMode _hairMode = HairMode.ChildVariantsToggle;

        [Header("Mode 1: Child Variants (Önerilen)")]
        [Tooltip("Head kemiğinin altına eklediğiniz ve pozisyon/rotasyonlarını ayarladığınız saç GameObjeleri")]
        [SerializeField] private GameObject[] _hairVariants;

        [Header("Mode 2: Instantiate Prefab")]
        [Tooltip("Saçın doğacağı kafa kemiği (Head bone) veya HairAnchor")]
        [SerializeField] private Transform _hairParent;
        [Tooltip("Inspector'dan atanacak saç prefabları")]
        [SerializeField] private GameObject[] _hairPrefabs;

        [Header("Hair Colors")]
        [SerializeField] private Color[] _hairColors = new Color[]
        {
            new Color(0x1D / 255f, 0x14 / 255f, 0x0E / 255f, 1f), // #1D140EFF - Koyu Kahve / Siyah
            new Color(0x56 / 255f, 0x3C / 255f, 0x2B / 255f, 1f), // #563C2BFF - Kahverengi
            new Color(0xDA / 255f, 0x6B / 255f, 0x2D / 255f, 1f), // #DA6B2DFF - Kızıl / Turuncu
            new Color(0xFD / 255f, 0xBA / 255f, 0x50 / 255f, 1f)  // #FDBA50FF - Sarı / Kumral
        };

        [Header("Animation Settings")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _idleStateName = "Idle";
        [Tooltip("Müşterilerin hepsi aynı anda aynı hareketi yapmasın diye Idle animasyonunu rastgele kareden başlatır")]
        [SerializeField] private bool _randomizeIdleOffset = true;

        [Header("Eat Reactions")]
        [Tooltip("Final sipariş servis edildiğinde rastgele seçilecek 2 sevinme animasyon trigger ismi")]
        [SerializeField] private string[] _happyTriggers = new string[] { "EatSuccess1", "EatSuccess2" };
        
        [Tooltip("2. siparişi olan müşterilerde 1. sipariş teslim edildiğinde çalışacak özel animasyon trigger'ı")]
        [SerializeField] private string _firstOrderServedTrigger = "FirstOrderServed";

        [Tooltip("Yemek alamayınca/üzülünce çalışacak trigger")]
        [SerializeField] private string _failTrigger = "EatFail";

        [Header("Auto Randomize")]
        [SerializeField] private bool _randomizeOnStart = true;

        private GameObject _instantiatedHair;

        private void Start()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_randomizeOnStart)
            {
                RandomizeHair();
            }

            if (_randomizeIdleOffset)
            {
                InitializeIdleAnimation();
            }
        }

        /// <summary>
        /// Tüm müşterilerin aynı anda aynı hareketi yapmasını önlemek için Idle animasyonunu rastgele bir zamandan başlatır.
        /// </summary>
        [ContextMenu("Initialize Idle (Random Offset)")]
        public void InitializeIdleAnimation()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                float randomOffset = Random.Range(0f, 1f);
                _animator.Play(_idleStateName, 0, randomOffset);
                _animator.speed = Random.Range(0.95f, 1.05f); // Zamanla tekrar senkron olmalarını da engeller
            }
        }

        #region Hair Management

        /// <summary>
        /// Saçı rastgele seçip aktif eder / üretir ve renk uygular.
        /// </summary>
        [ContextMenu("Randomize Hair")]
        public void RandomizeHair()
        {
            if (_hairMode == HairMode.ChildVariantsToggle)
            {
                RandomizeChildVariant();
            }
            else
            {
                RandomizePrefabSpawn();
            }
        }

        private void RandomizeChildVariant()
        {
            // Eğer liste boşsa kafa altındaki 'Hair' objesinin child'larını otomatik bul
            if (_hairVariants == null || _hairVariants.Length == 0)
            {
                AutoFindHairVariants();
            }

            if (_hairVariants == null || _hairVariants.Length == 0)
            {
                Debug.LogWarning("[TestCustomerController] Hair Variants listesi boş!", this);
                return;
            }

            int selectedIndex = Random.Range(0, _hairVariants.Length);
            Color? selectedColor = GetRandomColor();

            for (int i = 0; i < _hairVariants.Length; i++)
            {
                if (_hairVariants[i] != null)
                {
                    bool isActive = (i == selectedIndex);
                    _hairVariants[i].SetActive(isActive);

                    if (isActive && selectedColor.HasValue)
                    {
                        ApplyColorToObj(_hairVariants[i], selectedColor.Value);
                    }
                }
            }
        }

        private void AutoFindHairVariants()
        {
            Transform hairRoot = transform.Find("Hair");
            if (hairRoot == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.ToLower().Contains("hair") && t.childCount > 0)
                    {
                        hairRoot = t;
                        break;
                    }
                }
            }

            if (hairRoot != null)
            {
                List<GameObject> list = new List<GameObject>();
                for (int i = 0; i < hairRoot.childCount; i++)
                {
                    list.Add(hairRoot.GetChild(i).gameObject);
                }
                _hairVariants = list.ToArray();
            }
        }

        private void RandomizePrefabSpawn()
        {
            if (_hairPrefabs == null || _hairPrefabs.Length == 0)
            {
                Debug.LogWarning("[TestCustomerController] Hair Prefabs listesi boş!", this);
                return;
            }

            if (_hairParent == null)
            {
                Debug.LogWarning("[TestCustomerController] Hair Parent (Kafa Kemiği) atanmamış!", this);
                return;
            }

            // Önceki saçı temizle
            if (_instantiatedHair != null)
            {
                Destroy(_instantiatedHair);
            }

            int selectedIndex = Random.Range(0, _hairPrefabs.Length);
            GameObject selectedPrefab = _hairPrefabs[selectedIndex];

            if (selectedPrefab != null)
            {
                _instantiatedHair = Instantiate(selectedPrefab, _hairParent);
                _instantiatedHair.transform.localPosition = Vector3.zero;
                _instantiatedHair.transform.localRotation = Quaternion.identity;
                _instantiatedHair.transform.localScale = Vector3.one;

                Color? selectedColor = GetRandomColor();
                if (selectedColor.HasValue)
                {
                    ApplyColorToObj(_instantiatedHair, selectedColor.Value);
                }
            }
        }

        private Color? GetRandomColor()
        {
            if (_hairColors != null && _hairColors.Length > 0)
            {
                return _hairColors[Random.Range(0, _hairColors.Length)];
            }
            return null;
        }

        private void ApplyColorToObj(GameObject obj, Color color)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend == null) rend = obj.GetComponentInChildren<Renderer>();
            if (rend == null) return;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", color);
            propBlock.SetColor("_Color", color);
            rend.SetPropertyBlock(propBlock);
        }

        #endregion

        #region Animation Triggers

        [ContextMenu("Play Idle")]
        public void PlayIdle()
        {
            TriggerAnimation(_idleStateName);
        }

        /// <summary>
        /// Final siparişi alan müşteri için 2 farklı sevinme animasyonundan birini rastgele tetikler.
        /// </summary>
        [ContextMenu("Play Eat Success (Happy - Random 1 of 2)")]
        public void PlayEatSuccess()
        {
            if (_happyTriggers != null && _happyTriggers.Length > 0)
            {
                int randomIndex = Random.Range(0, _happyTriggers.Length);
                string chosenTrigger = _happyTriggers[randomIndex];
                if (!string.IsNullOrEmpty(chosenTrigger))
                {
                    TriggerAnimation(chosenTrigger);
                }
            }
        }

        /// <summary>
        /// 2. siparişi olan müşterilerde 1. sipariş servis edildiğinde çalışacak özel animasyon.
        /// </summary>
        [ContextMenu("Play First Order Served (Second Order Pending)")]
        public void PlayFirstOrderServed()
        {
            if (!string.IsNullOrEmpty(_firstOrderServedTrigger))
            {
                TriggerAnimation(_firstOrderServedTrigger);
            }
        }

        [ContextMenu("Play Eat Fail (Sad)")]
        public void PlayEatFail()
        {
            if (!string.IsNullOrEmpty(_failTrigger))
            {
                TriggerAnimation(_failTrigger);
            }
        }

        /// <summary>
        /// Sipariş tamamlandığında çağrılacak ana metot.
        /// hasSecondOrderRemaining true ise 1. sipariş animasyonunu, false ise final sevinme animasyonunu oynatır.
        /// </summary>
        public void OnOrderServed(bool hasSecondOrderRemaining)
        {
            if (hasSecondOrderRemaining)
            {
                PlayFirstOrderServed();
            }
            else
            {
                PlayEatSuccess();
            }
        }

        public void TriggerAnimation(string triggerName)
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null && !string.IsNullOrEmpty(triggerName))
            {
                _animator.SetTrigger(triggerName);
            }
        }

        public void PlayState(string stateName)
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null && !string.IsNullOrEmpty(stateName))
            {
                _animator.Play(stateName);
            }
        }

        #endregion
    }
}


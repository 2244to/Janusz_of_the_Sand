using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 4.0f;
        public float SprintSpeed = 6.0f;
        public float RotationSpeed = 1.0f;
        public float SpeedChangeRate = 10.0f;
        public float Gravity = -15.0f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        [Header("Cinemachine / Kamera Głowy")]
        [Tooltip("W to miejsce wrzuć pusty obiekt (HeadCameraRoot), który jest dzieckiem kości głowy Janusza!")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;
        [Tooltip("Przypisz tu kość głowy (Head), szyi (Neck) lub kręgosłupa (Spine) ze swojego modelu!")]
        public Transform KoscGlowy;
        [Header("Limity Głowy Modelu")]
        public float MaxGlowaWGore = -40f;
        public float MaxGlowaWDol = 50f;
        public float MaxLuzKamery = 10.0f;
        [Header("Ustawienia Kamery")]
        public LayerMask LayersToHide;

        [Header("Animacje")]
        public Animator animator;
        public bool Bieg;
        public bool Idzie;

        // --- ZMIENNE: HITBOXY ---
        [Header("Hitbox - Normalny (Stoi/Biegnie)")]
        public float NormalHeight = 2.0f;
        public float NormalRadius = 0.5f;
        public Vector3 NormalCenter = new Vector3(0f, 1.0f, 0f);

        [Header("Hitbox - Kucanie (Idzie)")]
        public float CrouchHeight = 1.0f;
        public float CrouchRadius = 0.5f;
        public Vector3 CrouchCenter = new Vector3(0f, 0.5f, 0f);

        [Header("Hitbox - Kucanie (Stoi w miejscu)")]
        public float CrouchIdleHeight = 0.6f;
        public float CrouchIdleRadius = 0.4f;
        public Vector3 CrouchIdleCenter = new Vector3(0f, 0.3f, 0f);

        [Header("Opcje Kucania")]
        public bool czyKuca;
        public float CrouchSpeed = 2.0f;
        public float CrouchTransitionSpeed = 10.0f;

        // --- ZMIENNE: HP, STAMINA I PIASEK ---
        [Header("Zdrowie i Stamina")]
        public float MaxHP = 100f;
        public float CurrentHP;
        public float MaxStamina = 100f;
        public float CurrentStamina;
        public float StaminaDrainRate = 15f;
        public float StaminaRegenRate = 10f;
        public float MinStaminaToSprint = 30f;

        [Header("Tajemniczy Piasek")]
        public float MaxPiasek = 100f;
        public float CurrentPiasek = 0f;

        [Header("UI (Interfejs - Panele Fill)")]
        public Image HpBarFill;
        public Image StaminaBarFill;
        public TMP_Text HpText;
        public Image PiasekBarFill;

        // --- BUDOWANIE PALI ---
        [Header("Budowanie Pali (Osobne)")]
        [Tooltip("Wrzuć tu PREFAB (plik z projektu) palika parawanu, który ma się ZESPAWNOWAĆ")]
        public GameObject PalPrefab;
        [Tooltip("Wrzuć tu ten obiekt pala, który Janusz TRZYMA W RĘCE (przypięty do dłoni)")]
        public GameObject PalWRece;
        public Transform PunktBudowania;
        public float OpoznienieSpawnu = 0.5f;

        [Header("Ograniczenia Budowania")]
        public float MinOdlegloscOdInnegoPala = 1.5f;
        public LayerMask WarstwaPali;

        private bool _czyBuduje = false;

        // --- ŁĄCZENIE PALI (PARAWAN) ---
        [Header("Łączenie Pali (Parawan - Materiał)")]
        [Tooltip("Wrzuć tu Twój ulubiony materiał w paski!")]
        public Material ParawanMaterial;
        [Tooltip("Jak wysoki ma być rozciągnięty materiał parawanu?")]
        public float WysokoscParawanu = 1.2f;
        public float ZasiegLaczeniaPali = 6.0f;
        private bool _trybBudowyParawanu = false;
        private GameObject _wybranyPal = null;

        // cinemachine
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private bool _isSprinting = false;
        private float _fallTimeoutDelta;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }

            if (_mainCamera != null)
            {
                Camera cam = _mainCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.cullingMask &= ~LayersToHide;
                }
            }
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            _fallTimeoutDelta = FallTimeout;
            CurrentHP = MaxHP;
            CurrentStamina = MaxStamina;
            CurrentPiasek = 0f;

            if (PalWRece != null) PalWRece.SetActive(false);

            UpdateUI();
        }

        private void Update()
        {
            ApplyGravity();
            GroundedCheck();
            Crouch();
            HandleStamina();
            Move();

            BudowanieMechanika();
            BudowanieParawanuMechanika();

            UpdateUI();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        // ==========================================
        //  TWORZENIE MATERIAŁU PARAWANU MIĘDZY PALAMI
        // ==========================================
        private void BudowanieParawanuMechanika()
        {
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _trybBudowyParawanu = !_trybBudowyParawanu;
                _wybranyPal = null;

                if (_trybBudowyParawanu)
                    Debug.Log("Tryb łączenia włączony! Kliknij LPM na pierwszy pal.");
                else
                    Debug.Log("Tryb łączenia wyłączony.");
            }

            if (_trybBudowyParawanu && !_czyBuduje && Input.GetMouseButtonDown(0))
            {
                RaycastPal();
            }
        }

        private void RaycastPal()
        {
            if (_mainCamera == null) return;

            Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, ZasiegLaczeniaPali, WarstwaPali))
            {
                GameObject trafionyPal = hit.collider.gameObject;

                if (_wybranyPal == null)
                {
                    _wybranyPal = trafionyPal;
                    Debug.Log("Wybrano pierwszy pal! Kliknij na drugi.");
                }
                else if (_wybranyPal != trafionyPal)
                {
                    StworzParawanMaterial(_wybranyPal, trafionyPal);
                    _wybranyPal = null;
                }
            }
        }

        private void StworzParawanMaterial(GameObject pal1, GameObject pal2)
        {
            if (ParawanMaterial == null)
            {
                Debug.LogWarning("Dodaj Material parawanu w inspektorze skryptu gracza!");
                return;
            }

            // Pobieramy pozycję obu pali
            Vector3 poz1 = pal1.transform.position;
            Vector3 poz2 = pal2.transform.position;

            // Ignorujemy pochylenia góra/dół (zazwyczaj parawan stawiamy na prostym piasku)
            // Dzięki temu parawan jest ułożony poziomo.
            poz1.y = Mathf.Min(poz1.y, poz2.y);
            poz2.y = poz1.y;

            // Odległość to nasz rozmiar (szerokość materiału)
            float odleglosc = Vector3.Distance(poz1, poz2);
            Vector3 srodek = (poz1 + poz2) / 2f;
            Vector3 kierunek = poz2 - poz1;

            // Zamiast Plane'a (który jest widoczny tylko z jednej strony), 
            // tworzymy bardzo cienki sześcian. Będzie widoczny dla plażowiczów po obu stronach!
            GameObject plotnoParawanu = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Możesz usunąć poniższą linijkę, jeśli nie chcesz żeby przez materiał parawanu dało się przechodzić
            // Destroy(plotnoParawanu.GetComponent<BoxCollider>());

            // 1. Obrót: celujemy płótnem w kierunku od pala 1 do pala 2
            if (kierunek != Vector3.zero)
            {
                plotnoParawanu.transform.rotation = Quaternion.LookRotation(kierunek);
            }

            // 2. Skalowanie: grubość(0.05f), wysokość w pionie(Y), długość między palami(Z)
            plotnoParawanu.transform.localScale = new Vector3(0.05f, WysokoscParawanu, odleglosc);

            // 3. Pozycja: ustalamy środek między palami, lekko unosząc do góry (o połowę wysokości materiału)
            plotnoParawanu.transform.position = srodek + new Vector3(0, WysokoscParawanu / 2f, 0);

            // 4. Nakładamy materiał polski!
            MeshRenderer renderer = plotnoParawanu.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = ParawanMaterial;
            }

            Debug.Log("Zbudowano polski parawan!");
        }
        // ==========================================

        private void BudowanieMechanika()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && !_czyBuduje)
            {
                if (MoznaPostawicPal()) StartCoroutine(BudujPal());
                else Debug.Log("Za blisko innego pala! Poszukaj innego miejsca.");
            }
        }

        private bool MoznaPostawicPal()
        {
            if (PunktBudowania == null) return false;

            Collider[] znalezioneObiekty = Physics.OverlapSphere(
                PunktBudowania.position, MinOdlegloscOdInnegoPala, WarstwaPali, QueryTriggerInteraction.Collide
            );

            if (znalezioneObiekty.Length > 0) return false;
            return true;
        }

        private IEnumerator BudujPal()
        {
            _czyBuduje = true;

            if (PalWRece != null) PalWRece.SetActive(true);
            if (animator != null) animator.SetTrigger("WbijPal");

            yield return new WaitForSeconds(OpoznienieSpawnu);

            if (PalPrefab != null && PunktBudowania != null)
            {
                Instantiate(PalPrefab, PunktBudowania.position, PunktBudowania.rotation);
            }

            if (PalWRece != null) PalWRece.SetActive(false);

            yield return new WaitForSeconds(0.5f);
            _czyBuduje = false;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold && !_czyBuduje)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
                _rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
                transform.Rotate(Vector3.up * _rotationVelocity);
            }

            if (KoscGlowy != null)
            {
                float bezpiecznyKatGlowy = Mathf.Clamp(_cinemachineTargetPitch, MaxGlowaWGore, MaxGlowaWDol);
                float nadmiarowyKat = _cinemachineTargetPitch - bezpiecznyKatGlowy;
                nadmiarowyKat = Mathf.Clamp(nadmiarowyKat, -MaxLuzKamery, MaxLuzKamery);
                float ostatecznyKatKamery = bezpiecznyKatGlowy + nadmiarowyKat;

                KoscGlowy.rotation = Quaternion.AngleAxis(bezpiecznyKatGlowy, transform.right) * KoscGlowy.rotation;
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(ostatecznyKatKamery, transform.eulerAngles.y, 0f);
            }
            else
            {
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
            }
        }

        private void Crouch()
        {
            if (_czyBuduje) czyKuca = false;
            else
            {
#if ENABLE_INPUT_SYSTEM
                czyKuca = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
#else
                czyKuca = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
            }

            bool stoiWMiejscu = _input.move == Vector2.zero;
            float targetHeight, targetRadius;
            Vector3 targetCenter;

            if (czyKuca)
            {
                if (stoiWMiejscu && !Idzie && !Bieg)
                {
                    targetHeight = CrouchIdleHeight;
                    targetRadius = CrouchIdleRadius;
                    targetCenter = CrouchIdleCenter;
                }
                else
                {
                    targetHeight = CrouchHeight;
                    targetRadius = CrouchRadius;
                    targetCenter = CrouchCenter;
                }
            }
            else
            {
                targetHeight = NormalHeight;
                targetRadius = NormalRadius;
                targetCenter = NormalCenter;
            }

            _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * CrouchTransitionSpeed);
            _controller.radius = Mathf.Lerp(_controller.radius, targetRadius, Time.deltaTime * CrouchTransitionSpeed);
            _controller.center = Vector3.Lerp(_controller.center, targetCenter, Time.deltaTime * CrouchTransitionSpeed);

            if (animator != null) animator.SetBool("Kuca", czyKuca);
        }

        private void HandleStamina()
        {
            bool isTryingToSprint = _input.sprint && _input.move != Vector2.zero && !czyKuca && !_czyBuduje;

            if (isTryingToSprint)
            {
                if (_isSprinting) { if (CurrentStamina <= 0) _isSprinting = false; }
                else { if (CurrentStamina >= MinStaminaToSprint) _isSprinting = true; }
            }
            else _isSprinting = false;

            if (_isSprinting)
            {
                CurrentStamina -= StaminaDrainRate * Time.deltaTime;
                CurrentStamina = Mathf.Max(CurrentStamina, 0f);
            }
            else
            {
                CurrentStamina += StaminaRegenRate * Time.deltaTime;
                CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
            }
        }

        private void Move()
        {
            float targetSpeed = czyKuca ? CrouchSpeed : (_isSprinting ? SprintSpeed : MoveSpeed);

            if (_input.move == Vector2.zero || _czyBuduje) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else _speed = targetSpeed;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
            }
            _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            Idzie = _input.move != Vector2.zero && !_czyBuduje;
            Bieg = _isSprinting && !_czyBuduje;

            if (animator != null)
            {
                animator.SetBool("Idzie", Idzie);
                animator.SetBool("Bieg", Bieg);
            }
        }

        private void ApplyGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;
            }
            else { if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime; }

            if (_verticalVelocity < _terminalVelocity) _verticalVelocity += Gravity * Time.deltaTime;
        }

        private void UpdateUI()
        {
            if (HpBarFill != null) HpBarFill.fillAmount = CurrentHP / MaxHP;
            if (StaminaBarFill != null) StaminaBarFill.fillAmount = CurrentStamina / MaxStamina;
            if (PiasekBarFill != null) PiasekBarFill.fillAmount = CurrentPiasek / MaxPiasek;
            if (HpText != null) HpText.text = Mathf.RoundToInt(CurrentHP).ToString();
        }

        public void TakeDamage(float damageAmount)
        {
            CurrentHP -= damageAmount;
            CurrentHP = Mathf.Max(CurrentHP, 0f);
            if (CurrentHP <= 0f) Debug.Log("Gracz zginął!");
        }

        public void Heal(float healAmount)
        {
            CurrentHP += healAmount;
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
        }

        public void DodajPiasek(float ilosc)
        {
            CurrentPiasek += ilosc;
            CurrentPiasek = Mathf.Min(CurrentPiasek, MaxPiasek);
        }

        public void ZabierzPiasek(float ilosc)
        {
            CurrentPiasek -= ilosc;
            CurrentPiasek = Mathf.Max(CurrentPiasek, 0f);
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);

            if (PunktBudowania != null)
            {
                Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f);
                Gizmos.DrawSphere(PunktBudowania.position, MinOdlegloscOdInnegoPala);
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSMovement : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR SETTINGS
    // ─────────────────────────────────────────────

    private Animator m_Animator;

    [Header("Movement Speeds")]
    public float walkSpeed      = 2.5f;
    public float runSpeed       = 5.5f;
    public float crouchSpeed    = 1.2f;

    [Header("Jump")]
    public float jumpHeight        = 1.2f;
    public float gravityMultiplier = 2.5f;

    [Header("Crouch")]
    public float standingHeight        = 1.8f;
    public float crouchHeight          = 0.9f;
    public float crouchTransitionSpeed = 10f;

    [Header("Camera")]
    public Transform cameraTransform;
    public float mouseSensitivity = 80f;
    public float lookClampAngle   = 80f;

    [Header("Camera Bob")]
    public float bobFrequency   = 2.0f;
    public float bobAmplitudeY  = 0.05f;
    public float bobAmplitudeX  = 0.025f;
    public float bobReturnSpeed = 5f;

    [Header("Footstep Sounds")]
    public AudioClip[] footstepClips;
    public float stepIntervalWalk = 0.55f;
    public float stepIntervalRun  = 0.32f;

    [Header("Stamina")]
    public float maxStamina      = 5f;
    public float staminaDrain    = 1f;
    public float staminaRecovery = 0.4f;

    // ─────────────────────────────────────────────
    //  ANIMATOR PARAMETER HASHES
    //
    //  In your Animator Controller, create:
    //    Float   →  Speed
    //    Trigger →  IsGrounded
    //    Trigger →  IsCrouching
    //    Trigger →  Jump
    //
    //  States:
    //    Idle     (default) - when Speed = 0
    //    Walk     (Speed > 0 and < runSpeed threshold, set in blend tree)
    //    Run      (Speed >= runSpeed threshold, set in blend tree)
    //    Jump     (Jump trigger)
    //
    //  Transitions:
    //    Idle <-> Walk <-> Run (via Speed parameter in blend tree)
    //    Any → Jump (Jump trigger)
    //    ... (crouch handled separately)
    // ─────────────────────────────────────────────

    private static readonly int AnimSpeed       = Animator.StringToHash("Speed");
    private static readonly int AnimIsGrounded  = Animator.StringToHash("grounded");
    private static readonly int AnimIsCrouching = Animator.StringToHash("crouch");
    private static readonly int AnimJump        = Animator.StringToHash("jump");

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private CharacterController _cc;
    private AudioSource         _audioSource;

    private Vector3 _velocity;
    private float   _currentSpeed;
    private bool    _isGrounded;
    private bool    _wasGrounded;
    private float   _gravity = -9.81f;

    private bool    _isCrouching;
    private float   _targetHeight;
    private Vector3 _cameraStandPos;
    private Vector3 _cameraCrouchPos;

    private float   _xRotation;
    private float   _bobTimer;
    private Vector3 _bobOffset;
    private float   _nextStepTime;

    private float   _currentStamina;
    private bool    _isExhausted;
    


    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _cc.height = standingHeight;
        _cc.center = new Vector3(0f, standingHeight / 2f, 0f);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;

        _currentStamina = maxStamina;
        _targetHeight   = standingHeight;

        if (cameraTransform != null)
        {
            _cameraStandPos  = new Vector3(0f, standingHeight - 0.15f, 0f);
            _cameraCrouchPos = new Vector3(0f, crouchHeight   - 0.10f, 0f);
            cameraTransform.localPosition = _cameraStandPos;
        }
        else
        {
            Debug.LogWarning("[FPSMovement] Camera Transform not assigned!");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        if (m_Animator == null)
            Debug.LogWarning("[FPSMovement] No Animator found — attach an Animator component.");
    }

    void Update()
    {
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        HandleCameraHeightLerp();
        HandleCameraBob();
        HandleFootsteps();
        HandleStamina();
        UpdateAnimator();   // always last — reads final state for this frame
    }

    // ─────────────────────────────────────────────
    //  ANIMATOR UPDATE
    //  All animator writes happen here, once per frame,
    //  after every other system has settled its state.
    // ─────────────────────────────────────────────

    private void UpdateAnimator()
    {
        if (m_Animator == null) return;

        bool moving = IsMovingOnGround();
        bool wantsToRun = _currentSpeed >= runSpeed;

        // Set the Speed parameter for the blend tree
        // Values: 0 = idle, 0.333333 = walk, 0.666667 = run
        if (!_isCrouching)
        {
            if (moving && wantsToRun)
                m_Animator.SetFloat(AnimSpeed, 0.666667f);  // Run
            else if (moving)
                m_Animator.SetFloat(AnimSpeed, 0.333333f);  // Walk
            else
                m_Animator.SetFloat(AnimSpeed, 0f);         // Idle
        }
        else
        {
            // When crouching, set speed to 0 so the blend tree goes to idle
            m_Animator.SetFloat(AnimSpeed, 0f);
        }

        // Set other state parameters as triggers
        if (!_isGrounded)
            m_Animator.SetTrigger("grounded");
        if (_isCrouching)
            m_Animator.SetTrigger("crouch");

        _wasGrounded = _isGrounded;
    }

    // ─────────────────────────────────────────────
    //  MOUSE LOOK
    // ─────────────────────────────────────────────

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation  = Mathf.Clamp(_xRotation, -lookClampAngle, lookClampAngle);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────

    private void HandleMovement()
    {
        _isGrounded = _cc.isGrounded;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool wantsToRun = Input.GetKey(KeyCode.LeftShift)
                          && !_isCrouching
                          && !_isExhausted
                          && _currentStamina > 0f
                          && v > 0.1f;

        if (_isCrouching)
            _currentSpeed = crouchSpeed;
        else if (wantsToRun)
            _currentSpeed = runSpeed;
        else
            _currentSpeed = walkSpeed;

        Vector3 move = transform.right * h + transform.forward * v;
        if (move.magnitude > 1f) move.Normalize();

        _cc.Move(move * _currentSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    //  JUMP
    // ─────────────────────────────────────────────

    private void HandleJump()
    {
        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (Input.GetButtonDown("Jump") && _isGrounded && !_isCrouching)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(_gravity));

            // SetTrigger is safe here — fires exactly once per button press
            if (m_Animator != null)
                m_Animator.SetTrigger("jump");
        }
    }

    // ─────────────────────────────────────────────
    //  GRAVITY
    // ─────────────────────────────────────────────

    private void ApplyGravity()
    {
        _velocity.y += _gravity * gravityMultiplier * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    //  CROUCH
    // ─────────────────────────────────────────────

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            if (_isCrouching) TryStandUp();
            else              StartCrouch();
        }
    }

    private void StartCrouch()
    {
        _isCrouching  = true;
        _targetHeight = crouchHeight;
    }

    private void TryStandUp()
    {
        Vector3 top = transform.position + Vector3.up * standingHeight;
        if (Physics.CheckSphere(top, _cc.radius * 0.9f, ~LayerMask.GetMask("Player")))
            return;

        _isCrouching  = false;
        _targetHeight = standingHeight;
    }

    private void HandleCameraHeightLerp()
    {
        float newHeight = Mathf.Lerp(_cc.height, _targetHeight, crouchTransitionSpeed * Time.deltaTime);
        _cc.height = newHeight;
        _cc.center = new Vector3(0f, newHeight / 2f, 0f);

        if (cameraTransform == null) return;
        Vector3 targetCamPos = _isCrouching ? _cameraCrouchPos : _cameraStandPos;
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            targetCamPos,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────
    //  CAMERA BOB
    // ─────────────────────────────────────────────

    private void HandleCameraBob()
    {
        if (cameraTransform == null) return;

        bool moving = IsMovingOnGround();

        if (moving)
        {
            float speedFactor = _currentSpeed / walkSpeed;
            _bobTimer += Time.deltaTime * bobFrequency * speedFactor;
            _bobOffset = new Vector3(
                Mathf.Sin(_bobTimer / 2f) * bobAmplitudeX,
                Mathf.Sin(_bobTimer)      * bobAmplitudeY,
                0f
            );
        }
        else
        {
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, bobReturnSpeed * Time.deltaTime);
        }

        Vector3 basePos = _isCrouching ? _cameraCrouchPos : _cameraStandPos;
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            basePos + _bobOffset,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────
    //  FOOTSTEPS
    // ─────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (!IsMovingOnGround() || footstepClips == null || footstepClips.Length == 0) return;

        float interval = (_currentSpeed >= runSpeed) ? stepIntervalRun : stepIntervalWalk;

        if (Time.time >= _nextStepTime)
        {
            _nextStepTime = Time.time + interval;
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            _audioSource.PlayOneShot(clip, Random.Range(0.85f, 1.0f));
        }
    }

    // ─────────────────────────────────────────────
    //  STAMINA
    // ─────────────────────────────────────────────

    private void HandleStamina()
    {
        bool isSprinting = (_currentSpeed >= runSpeed) && IsMovingOnGround();

        if (isSprinting)
        {
            _currentStamina -= staminaDrain * Time.deltaTime;
            _currentStamina  = Mathf.Max(_currentStamina, 0f);
            if (_currentStamina <= 0f) _isExhausted = true;
        }
        else
        {
            _currentStamina += staminaRecovery * Time.deltaTime;
            _currentStamina  = Mathf.Min(_currentStamina, maxStamina);
            if (_isExhausted && _currentStamina >= maxStamina * 0.25f)
                _isExhausted = false;
        }
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private bool IsMovingOnGround()
    {
        Vector3 h = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        return _isGrounded && h.magnitude > 0.1f;
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    public float StaminaNormalized => _currentStamina / maxStamina;
    public bool  IsCrouching       => _isCrouching;
    public bool  IsSprinting       => _currentSpeed >= runSpeed && IsMovingOnGround();
}
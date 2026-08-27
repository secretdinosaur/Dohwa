using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    #region Components & References
    [Header("Core References")]
    [HideInInspector] public Transform cameraTransform;
    [HideInInspector] public Rigidbody _rb;
    #endregion

    [Header("Fall Damage Settings")]
    public float safeFallDistance = 10f;
    public float fatalFallDistance = 30f;
    public float deathYLevel = -50f;
    public float maxSlopeAngle = 40f;
    public float maxJumpSlopeAngle = 60f;

    #region Movement Settings
    [Header("Movement Settings")]
    [SerializeField] private float dashForce = 10f;
    [SerializeField] private float sprintMultiplier = 3f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float walkMultiplier = 0.5f;
    [SerializeField] private float JumpForce = 5f;
    [SerializeField] private float sprintLockoutDuration = 3f;
    [SerializeField] private float jumpHorizontalVelocityMultiplier = 12f;
    [SerializeField] private float jumpCooldown = 0.5f;
    #endregion

    #region State Flags & Internal Variables
    [Header("Status Flags")]
    public bool isGrounded = true;
    public bool isKnockedDown = false;
    private float _lastJumpTime = 0f;
    private Vector2 _inputMove;
    private Vector2 _smoothInput;
    private bool _isSprinting;

    private bool _prevIsSprinting = false;
    private Vector2 _prevInputMove = Vector2.zero;

    private int _currentSprintDir = 0;
    private bool _isSprintTransitionActive = false;
    private float _sprintTransitionStartTime = 0f;

    private bool _isStopping = false;
    private float _animSpeed;

    public bool isKnockedBack = false;

    private bool _isWalkingRequested = false;
    private bool _isWalking = false;
    private bool _isFallingDown = false;
    private float _highestFallPoint;
    private bool _isDashCooldown = false;
    private bool _isDashing = false;
    private float sprintLockoutTimer = 0f;
    private RaycastHit slopeHit;
    private float rotationSpeed = 10f;

    private bool _isSprintBtnHeld = false;
    private float _sprintPressTime = 0f;
    #endregion

    #region Tuning Settings
    [Header("Tuning Settings")]

    private float jumpDelayBeforeForce = 0.16f;
    private float jumpLandCheckDelay = 0.15f;
    private float sphereCastRadius = 0.2f;
    private float sphereCastHeightOffset = 0.5f;
    private float sphereCastMaxDistance = 100f;
    private float landingHeightThreshold = 0.05f;
    private float landingVelocityThreshold = 0.01f;
    private float landingPredictThresholdDefault = 0.1f;
    private float landingPredictThresholdLockOn = 0.12f;
    private float landingAnimBufferTime = 0.5f;
    private float wakeUpAnimDuration = 2f;
    private float airborneWakeUpDelay = 3f;
    [HideInInspector] public float knockbackForceThreshold = 9f;
    private float knockbackGuardReduction = 0.7f;
    private float knockbackDurationDivisor = 30f;
    private float knockbackResetDelay = 0.33f;
    private float dashPhaseFirstMaxWait = 0.2f;
    private float backwardSpeedReductionDivisor = 1.5f;
    private float guardSpeedMultiplier = 0.33f;
    private float fallVelocityThreshold = -2.0f;
    private float fallStopVelocityThreshold = -0.5f;
    private float slopeRaycastOffset = 0.3f;
    private float slopeRaycastDistance = 0.5f;

    public float SprintMP;
    #endregion

    #region Constants
    private const float InputThresholdMin = 0.1f;
    private const float InputMagnitudeZeroCheck = 0.01f;
    private const float DirectionSqrMagnitudeMin = 0.0001f;
    private const float DiagonalAngleOffset = 45f;
    private const float AnimValueWalk = 0.5f;
    private const float AnimValueRun = 1f;
    private const float AnimValueSprint = 2f;
    private const float AnimDampTime = 0.25f;
    private const float AnimSpeedLerpDivisor = 2f;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody>();
        }

        if (_rb != null)
        {
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    private void Update()
    {
        if (sprintLockoutTimer > 0f)
        {
            sprintLockoutTimer -= Time.deltaTime;
        }


        if (Player.Instance == null || !Player.Instance.CanMove() || (Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isAttacking))
        {
            Player.Instance.Animator_SetFloat("inputX", 0f);
            Player.Instance.Animator_SetFloat("inputY", 0f);
            Player.Instance.Animator_SetFloat("MoveSpeed", 0f);
            _smoothInput = Vector2.zero;
            _animSpeed = 0f;
            return;
        }

        bool ctrlPressed = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
        _isWalking = _isWalkingRequested || ctrlPressed;

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        CheckFallDamage();
        CheckLedgeFall();
        bool isAttacking = Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isAttacking;

        if (isAttacking || isKnockedBack || Player.Instance.IsGrabbed || Player.Instance.isWakingUp)
        {
            return;
        }

        if (Player.Instance == null || !Player.Instance.CanMove() || _isStopping)
        {
            if (_rb != null && !_rb.isKinematic)
            {
                Vector3 targetZeroVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
                _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, targetZeroVelocity, acceleration * Time.fixedDeltaTime);
            }

            _animSpeed = Mathf.MoveTowards(_animSpeed, 0f, acceleration * Time.fixedDeltaTime / AnimSpeedLerpDivisor);
            if (Player.Instance != null)
            {
                Player.Instance.Animator_SetFloat("MoveSpeed", _animSpeed);
            }

            return;
        }

        HandleMovementAndRotation();
    }
    #endregion

    #region Input Callbacks
    public void OnMove(InputAction.CallbackContext context) => _inputMove = context.ReadValue<Vector2>();

    public void OnWalk(InputAction.CallbackContext context)
    {
        if (context.performed) _isWalkingRequested = true;
        else if (context.canceled) _isWalkingRequested = false;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (Player.Instance == null) return;

        bool isLockedOn = Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isLockedOn;
        bool considerLockedOn = isLockedOn || Player.Instance.PlayerAttack.lastLockOffTime + 1 > Time.time;

        if (context.started || (context.performed && !_isSprintBtnHeld))
        {
            _isSprintBtnHeld = true;
            _sprintPressTime = Time.time;
        }
        else if (context.canceled)
        {
            _isSprintBtnHeld = false;

            if (considerLockedOn && Time.time - _sprintPressTime <= 0.25f)
            {
                if (!_isDashCooldown && isGrounded &&
                    Player.Instance.IsAlive && !Player.Instance.isCinematic &&
                    !Player.Instance.IsStunned && !Player.Instance.isEating && !Player.Instance.isWakingUp)
                {
                    Player.Instance.CancelCurrentAction();
                    StartCoroutine(DashRoutine());
                }
            }
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && Player.Instance != null && Player.Instance.CanJump() && isGrounded && !Player.Instance.isWakingUp && !DialogueManager.Instance.IsDialogueActive)
        {
            if (Time.time - _lastJumpTime < jumpCooldown) return;

            if (Physics.Raycast(transform.position + Vector3.up * slopeRaycastOffset, Vector3.down, out RaycastHit hit, slopeRaycastDistance, groundLayer))
            {
                if (Vector3.Angle(Vector3.up, hit.normal) >= maxJumpSlopeAngle) return;
            }

            _lastJumpTime = Time.time;
            StartCoroutine(JumpRoutine());
        }
    }
    #endregion

    #region Movement & Rotation Logic
    private void HandleMovementAndRotation()
    {
        if (_isDashing) return;

        bool isLockedOn = Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isLockedOn;
        bool considerLockedOn = isLockedOn || Player.Instance.PlayerAttack.lastLockOffTime + 1 > Time.time;

        bool canSprint = _isSprintBtnHeld
                         && _inputMove.y >= 0
                         && _inputMove.magnitude > InputMagnitudeZeroCheck
                         && sprintLockoutTimer <= 0f
                         && Player.Instance.MP > 0f
                         && Player.Instance.CanDash();

        if (considerLockedOn && Time.time - _sprintPressTime <= 0.25f)
        {
            canSprint = false;
        }

        _isSprinting = canSprint;

        bool justStartedSprinting = !_prevIsSprinting && _isSprinting;
        bool wasMoving = _prevInputMove.magnitude > InputThresholdMin;

        int targetSprintDir = 0;
        if (_isSprinting)
        {
            if (_inputMove.x <= -InputThresholdMin) targetSprintDir = -1;
            else if (_inputMove.x >= InputThresholdMin) targetSprintDir = 1;
        }

        if (targetSprintDir != _currentSprintDir)
        {
            if (_isSprintTransitionActive)
            {
                _isSprintTransitionActive = false;
            }

            if (targetSprintDir != 0 && justStartedSprinting && wasMoving)
            {
                _sprintTransitionStartTime = Time.time;
                _isSprintTransitionActive = true;
            }

            _currentSprintDir = targetSprintDir;
        }

        if (_isSprintTransitionActive && Time.time - _sprintTransitionStartTime >= 0.5f)
        {
            _isSprintTransitionActive = false;
        }

        _smoothInput = Vector2.Lerp(_smoothInput, _inputMove, Time.fixedDeltaTime * acceleration);

        Transform target = Player.Instance.PlayerAttack != null ? Player.Instance.PlayerAttack.currentTarget : null;

        if (isLockedOn && target == null)
        {
            Player.Instance.PlayerAttack.isLockedOn = false;
            isLockedOn = false;
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector3 moveDir = (forward * _smoothInput.y) + (right * _smoothInput.x);
        if (moveDir.sqrMagnitude > InputMagnitudeZeroCheck) moveDir.Normalize();

        if (OnSlope())
        {
            moveDir = GetSlopeMoveDirection(moveDir);
        }

        float currentSpeedVal;

        if (Player.Instance != null && Player.Instance.isEating)
        {
            currentSpeedVal = StatManager.Instance.MoveSpeed * walkMultiplier;
            _isSprinting = false;
        }
        else
        {
            currentSpeedVal = _isSprinting ? StatManager.Instance.MoveSpeed * sprintMultiplier : StatManager.Instance.MoveSpeed;

            if (_isWalking && moveDir.sqrMagnitude > InputMagnitudeZeroCheck)
            {
                currentSpeedVal *= walkMultiplier;
            }
        }

        if (isLockedOn && _inputMove.y < 0) currentSpeedVal /= backwardSpeedReductionDivisor;

        Vector3 targetVelocity = moveDir * (currentSpeedVal * _smoothInput.magnitude);

        if (!isGrounded && moveDir.sqrMagnitude > InputMagnitudeZeroCheck)
        {
            if (Physics.Raycast(transform.position + Vector3.up * sphereCastHeightOffset, moveDir, out RaycastHit wallHit, 0.6f, groundLayer))
            {
                if (Vector3.Angle(Vector3.up, wallHit.normal) >= maxSlopeAngle)
                {
                    targetVelocity.x = 0f;
                    targetVelocity.z = 0f;
                }
            }
        }

        float targetAnimValue = _inputMove.sqrMagnitude <= InputMagnitudeZeroCheck ? 0f : _isWalking ? AnimValueWalk : _isSprinting ? AnimValueSprint : AnimValueRun;
        if (_inputMove.y < 0) targetAnimValue = AnimValueRun;
        if (Player.Instance != null && Player.Instance.isEating && moveDir.sqrMagnitude > InputMagnitudeZeroCheck)
        {
            targetAnimValue = AnimValueWalk;
        }

        if (_isSprinting && !_isWalking) Player.Instance.MP -= Time.fixedDeltaTime * (StatManager.Instance.MpRecoveryAmount + SprintMP);

        if (Player.Instance.MP <= 0f)
        {
            _isSprinting = false;
            sprintLockoutTimer = sprintLockoutDuration;
        }

        Quaternion nextRotation = _rb != null ? _rb.rotation : transform.rotation;

        if (isLockedOn && target != null)
        {
            Vector3 originPos = _rb != null ? (Vector3)_rb.position : transform.position;
            Vector3 desiredDir = target.position - originPos;
            desiredDir.y = 0;

            if (desiredDir.sqrMagnitude > DirectionSqrMagnitudeMin)
            {
                nextRotation = Quaternion.LookRotation(desiredDir.normalized);
            }
        }
        else if (cameraTransform != null && moveDir.sqrMagnitude > InputMagnitudeZeroCheck)
        {
            Vector3 finalLookDir = forward;

            if (Mathf.Abs(_inputMove.x) > InputThresholdMin && Mathf.Abs(_inputMove.y) > InputThresholdMin)
            {
                float angleOffset = DiagonalAngleOffset * Mathf.Sign(_inputMove.x) * Mathf.Sign(_inputMove.y);
                finalLookDir = Quaternion.Euler(0, angleOffset, 0) * finalLookDir;
            }

            nextRotation = Quaternion.Slerp(_rb != null ? _rb.rotation : transform.rotation, Quaternion.LookRotation(finalLookDir), rotationSpeed * Time.fixedDeltaTime);
        }

        float guardMultiplier = Player.Instance.isGuarding ? guardSpeedMultiplier : 1f;
        targetVelocity *= guardMultiplier;

        if (_rb != null)
        {
            if (!OnSlope() || !isGrounded)
            {
                targetVelocity.y = _rb.linearVelocity.y;
            }
        }

        _animSpeed = Mathf.MoveTowards(_animSpeed, targetAnimValue, acceleration * Time.fixedDeltaTime / AnimSpeedLerpDivisor);
        Player.Instance.Animator_SetFloat("MoveSpeed", _animSpeed);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            if (isLockedOn || (!isLockedOn && moveDir.sqrMagnitude > InputMagnitudeZeroCheck)) _rb.MoveRotation(nextRotation);
        }
        else
        {
            transform.position += targetVelocity * Time.fixedDeltaTime;
            transform.rotation = nextRotation;
        }

        _prevIsSprinting = _isSprinting;
        _prevInputMove = _inputMove;
    }
    public LayerMask groundLayer;
    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position + Vector3.up * slopeRaycastOffset, Vector3.down, out slopeHit, slopeRaycastDistance, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private void UpdateAnimation()
    {
        float dampTime = _inputMove.sqrMagnitude > InputMagnitudeZeroCheck ? AnimDampTime : AnimDampTime;

        float animX = _inputMove.x;
        float animY = _inputMove.y;

        if (Mathf.Abs(_inputMove.x) > InputThresholdMin && Mathf.Abs(_inputMove.y) > InputThresholdMin)
        {
            animX = 0f;
            animY = _inputMove.y > 0 ? 1f : -1f;
        }

        Player.Instance.Animator_SetFloat("inputX", animX, dampTime, Time.deltaTime);
        Player.Instance.Animator_SetFloat("inputY", animY, dampTime, Time.deltaTime);
    }
    #endregion

    #region Jump & Fall Damage Logic
    private void CheckFallDamage()
    {
        if (_rb == null || Player.Instance == null || !Player.Instance.IsAlive) return;

        if (transform.position.y < deathYLevel)
        {
            Player.Instance.HP = 0;
            return;
        }

        if (!isGrounded && _rb.linearVelocity.y < fallVelocityThreshold)
        {
            if (!_isFallingDown)
            {
                _isFallingDown = true;
                _highestFallPoint = transform.position.y;
            }
            else if (transform.position.y > _highestFallPoint)
            {
                _highestFallPoint = transform.position.y;
            }
        }
        else if (_isFallingDown && _rb.linearVelocity.y >= fallStopVelocityThreshold)
        {
            _isFallingDown = false;
            float fallDistance = _highestFallPoint - transform.position.y;

            if (fallDistance > safeFallDistance)
            {
                float damageRatio = Mathf.Clamp01((fallDistance - safeFallDistance) / (fatalFallDistance - safeFallDistance));

                float fallDamage = StatManager.Instance.MaxHp * damageRatio;

                if (fallDistance >= fatalFallDistance)
                {
                    fallDamage = StatManager.Instance.MaxHp;
                }

                Player.Instance.TakeFallDamage(fallDamage);
            }
        }
    }
    private void CheckLedgeFall()
    {
        if (isGrounded && _rb != null && _rb.linearVelocity.y < fallVelocityThreshold && !isKnockedBack && !Player.Instance.IsStunned && !Player.Instance.isAirborne && !Player.Instance.isWakingUp)
        {
            int layerMask = ~LayerMask.GetMask("Player");
            Vector3 castOrigin = transform.position + Vector3.up * (sphereCastRadius + sphereCastHeightOffset);

            if (!Physics.SphereCast(castOrigin, sphereCastRadius, Vector3.down, out RaycastHit hit, sphereCastHeightOffset + 1.5f, layerMask))
            {
                isGrounded = false;
                Player.Instance.Animator_SetBool("IsJump", true);
                StartCoroutine(WaitAndDetectLandingRoutine());
            }
        }
    }

    private IEnumerator WaitAndDetectLandingRoutine()
    {
        int layerMask = ~LayerMask.GetMask("Player");

        while (true)
        {
            yield return null;
            float v = _rb != null ? _rb.linearVelocity.y : 0f;
            Vector3 castOrigin = transform.position + Vector3.up * (sphereCastRadius + sphereCastHeightOffset);

            if (Physics.SphereCast(castOrigin, sphereCastRadius, Vector3.down, out RaycastHit hit, sphereCastMaxDistance, layerMask))
            {
                float h = Mathf.Max(0f, hit.distance - sphereCastHeightOffset);
                bool isHighLanding = (v <= 0.5f && h <= landingHeightThreshold);
                bool isPredictLanded = false;

                if (v <= landingVelocityThreshold)
                {
                    float g = Physics.gravity.y;
                    float discriminant = (v * v) - (2f * g * h);
                    if (discriminant >= 0f)
                    {
                        float t = (-v - Mathf.Sqrt(discriminant)) / g;
                        float threshold = (Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isLockedOn) ? landingPredictThresholdLockOn : landingPredictThresholdDefault;
                        if (h <= landingHeightThreshold || (t >= 0f && t <= threshold)) isPredictLanded = true;
                    }
                }

                if (isHighLanding || isPredictLanded)
                {
                    Player.Instance.Animator_SetBool("IsJump", false);

                    if (Player.Instance.isAirborne)
                    {
                        _isStopping = true;

                        yield return new WaitForSeconds(airborneWakeUpDelay);

                        Player.Instance.StartWakeUp(wakeUpAnimDuration);
                        yield return new WaitForSeconds(wakeUpAnimDuration);
                        _isStopping = false;
                    }
                    else
                    {
                        _isStopping = true;
                        yield return new WaitForSeconds(landingAnimBufferTime);
                        _isStopping = false;
                    }

                    isGrounded = true;
                    break;
                }
            }
        }
    }

    private IEnumerator JumpRoutine()
    {
        _isStopping = false;
        isGrounded = false;
        Player.Instance.Animator_SetBool("IsJump", true);
        yield return new WaitForSeconds(jumpDelayBeforeForce);

        if (_rb != null)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpForce, _rb.linearVelocity.z);
        }

        yield return new WaitForSeconds(jumpLandCheckDelay);

        yield return StartCoroutine(WaitAndDetectLandingRoutine());
    }
    #endregion

    #region Knockback & Dash Mechanics
    private Coroutine currentKnockBackRoutine;

    public void CancelKnockBack()
    {
        if (currentKnockBackRoutine != null)
        {
            StopCoroutine(currentKnockBackRoutine);
            currentKnockBackRoutine = null;
        }

        isKnockedBack = false;
        isKnockedDown = false;

        // 💡 추가됨: 강제 넉백 종료 시 카메라도 즉시 원상 복구
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetKnockdownCamera(false);
        }

        if (Player.Instance != null)
        {
            Player.Instance.isWakingUp = false;
            Player.Instance.isAirborne = false;
            Player.Instance.PreventMove = false;
            Player.Instance.PreventAttack = false;

            if (Player.Instance.animator != null)
            {
                Player.Instance.animator.ResetTrigger("KnockBack");
                Player.Instance.animator.ResetTrigger("KnockDown1");
                Player.Instance.animator.ResetTrigger("KnockDown2");
                Player.Instance.animator.ResetTrigger("Airborne");

                if (Player.Instance.animator.layerCount > 1)
                {
                    Player.Instance.animator.SetLayerWeight(1, 1f);
                }
            }
        }

        if (_rb != null && (Player.Instance == null || !Player.Instance.IsGrabbed))
        {
            _rb.linearVelocity = Vector3.zero;
        }
    }
    private IEnumerator LookAtTargetOnKnockDown()
    {
        if (Player.Instance.PlayerAttack == null || !Player.Instance.PlayerAttack.isLockedOn || Player.Instance.PlayerAttack.currentTarget == null)
            yield break;

        Transform target = Player.Instance.PlayerAttack.currentTarget;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            if (target == null) break;

            Vector3 dir = target.position - transform.position;
            dir.y = 0;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                if (_rb != null) _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, Time.deltaTime * 50f));
                else transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 50f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    public void KnockBack(Vector3 force, int knockdownType = 0, float knockdownDuration = 1.5f)
    {
        if (force.magnitude == 0 && knockdownType == 0) return;

        if (Player.Instance != null && (Player.Instance.isGuarding || Player.Instance.isParrying))
        {
            knockdownType = 0;
            force.y = Mathf.Min(force.y, 0f);
        }

        if (Player.Instance != null) Player.Instance.ClearStun();

        Player.Instance.CancelCurrentAction();
        _isStopping = false;

        if (currentKnockBackRoutine != null)
        {
            StopCoroutine(currentKnockBackRoutine);
        }
        currentKnockBackRoutine = StartCoroutine(KnockBackRoutine(force, knockdownType, knockdownDuration));
    }

    private IEnumerator KnockBackRoutine(Vector3 force, int knockdownType, float knockdownDuration)
    {
        Vector3 applyForce = force;
        float forceMag = applyForce.magnitude;

        bool becomeAirborne = force.y >= 10f;
        bool shouldPlayAnim = (forceMag > knockbackForceThreshold || knockdownType > 0) && (!Player.Instance.IsStunned || knockdownType > 0) && !Player.Instance.isGuarding;

        isKnockedBack = true;
        if (knockdownType > 0 || becomeAirborne)
        {
            isKnockedDown = true;
            // 💡 추가됨: 넉다운 되거나 에어본이 되면 카메라 전환
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetKnockdownCamera(true);
            }
        }
        StartCoroutine(LookAtTargetOnKnockDown());
        if (becomeAirborne || shouldPlayAnim)
        {
            if (Player.Instance.animator.layerCount > 1)
            {
                Player.Instance.animator.SetLayerWeight(1, 0f);
            }

            Player.Instance.animator.ResetTrigger("Hitted1");
            Player.Instance.animator.ResetTrigger("Hitted2");
            Player.Instance.animator.ResetTrigger("Stagger");
            Player.Instance.animator.ResetTrigger("AirborneHit");
            Player.Instance.animator.ResetTrigger("KnockBack");
            Player.Instance.animator.ResetTrigger("KnockDown1");
            Player.Instance.animator.ResetTrigger("KnockDown2");
        }

        if (becomeAirborne)
        {
            Player.Instance.isAirborne = true;
            isGrounded = false;
            Player.Instance.Animator_SetTrigger("Airborne");
            StartCoroutine(WaitAndDetectLandingRoutine());
        }
        else if (shouldPlayAnim)
        {
            if (knockdownType > 0)
            {
                Player.Instance.Animator_SetTrigger("KnockDown" + knockdownType);
            }
            else
            {
                Player.Instance.Animator_SetTrigger("KnockBack");
            }
        }

        if (Player.Instance != null && (Player.Instance.isGuarding || Player.Instance.isParrying))
            applyForce *= knockbackGuardReduction;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;

            if (!Player.Instance.IsGrabbed)
            {
                _rb.AddForce(applyForce, ForceMode.Impulse);
            }
        }

        if (!becomeAirborne && (forceMag > knockbackForceThreshold || knockdownType > 0))
        {
            if (knockdownType > 0)
            {
                yield return new WaitForSeconds(knockdownDuration);

                yield return new WaitForSeconds(3f);

                Player.Instance.StartWakeUp(wakeUpAnimDuration);
            }
            else
            {
                yield return new WaitForSeconds(forceMag / knockbackDurationDivisor);

                Player.Instance.animator.ResetTrigger("KnockBack");
                Player.Instance.Animator_SetTrigger("KnockBackEnd");

                if (Player.Instance.animator.layerCount > 1)
                {
                    Player.Instance.animator.SetLayerWeight(1, 1f);
                }
            }
        }
        else if (becomeAirborne)
        {
            yield return new WaitWhile(() => Player.Instance.isAirborne || Player.Instance.isWakingUp);
        }

        if (!becomeAirborne)
        {
            yield return new WaitForSeconds(knockbackResetDelay);
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
        }

        isKnockedBack = false;
        isKnockedDown = false;

        // 💡 추가됨: 넉다운 상태가 끝나고 기상이 완료되면 원래 카메라로 복구
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetKnockdownCamera(false);
        }

        if (Player.Instance != null && Player.Instance.currentStunRoutine == null)
        {
            Player.Instance.PreventMove = false;
            Player.Instance.PreventAttack = false;
        }

        currentKnockBackRoutine = null;
    }

    public void TriggerAirborneHit()
    {
        if (Player.Instance.isAirborne && Player.Instance != null)
        {
            StartCoroutine(DelayedAirborneHitRoutine());
        }
    }

    private IEnumerator DelayedAirborneHitRoutine()
    {
        yield return null;
        Player.Instance.animator.ResetTrigger("AirborneHit");
        Player.Instance.Animator_SetTrigger("AirborneHit");
    }

    private IEnumerator DashRoutine()
    {
        _isDashCooldown = true;
        _isDashing = true;
        Player.Instance.PreventAttack = true;

        Vector3 dashDirection = Vector3.zero;
        bool isLockedOn = Player.Instance.PlayerAttack != null && Player.Instance.PlayerAttack.isLockedOn;
        Transform target = Player.Instance.PlayerAttack != null ? Player.Instance.PlayerAttack.currentTarget : null;

        if (isLockedOn && target == null)
        {
            Player.Instance.PlayerAttack.isLockedOn = false;
            isLockedOn = false;
        }

        if (isLockedOn && target != null)
        {
            Vector3 offset = target.position - transform.position;
            offset.y = 0;
            Vector3 dirToTarget = offset.normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, dirToTarget).normalized;
            dashDirection = (dirToTarget * _inputMove.y) + (tangent * _inputMove.x);
        }
        else
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
            forward.y = 0; right.y = 0;
            dashDirection = (forward.normalized * _inputMove.y) + (right.normalized * _inputMove.x);
        }

        if (dashDirection.sqrMagnitude < DirectionSqrMagnitudeMin) dashDirection = transform.forward;
        else dashDirection.Normalize();

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(dashDirection * dashForce, ForceMode.Impulse);
        }

        Player.Instance.Animator_SetTrigger("isDash");

        float firstWait = Mathf.Min(dashPhaseFirstMaxWait, Mathf.Max(0f, StatManager.Instance.dashCooldown));
        yield return new WaitForSeconds(firstWait);

        _isDashing = false;
        Player.Instance.PreventAttack = false;

        float remaining = Mathf.Max(0f, StatManager.Instance.dashCooldown - firstWait);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        _isDashCooldown = false;
    }

    public void ApplyAnimatorDelta(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (_rb != null && !_rb.isKinematic)
        {
            _rb.MovePosition(_rb.position + deltaPosition);
            _rb.MoveRotation(_rb.rotation * deltaRotation);
        }
        else
        {
            transform.position += deltaPosition;
            transform.rotation *= deltaRotation;
        }
    }

    public void SetLookRotationImmediate(Vector3 lookDirection)
    {
        if (lookDirection.sqrMagnitude <= DirectionSqrMagnitudeMin) return;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);

        if (_rb != null && !_rb.isKinematic) _rb.MoveRotation(targetRotation);
        else transform.rotation = targetRotation;
    }
    #endregion
}
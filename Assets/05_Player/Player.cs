using System;
using System.Collections;
using System.IO;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour, IDamageable
{
    #region Components & References
    [Header("Core Components")]
    public static Player Instance;
    [HideInInspector] public Animator animator;
    [HideInInspector] public CinemachineImpulseSource impulseSource;
    public Material LowHPMat;
    [HideInInspector] public GameObject Sword;

    [HideInInspector] public PlayerAttack PlayerAttack;
    [HideInInspector] public PlayerMove PlayerMove;

    [Header("Equipment")]
    public Weapon equippedWeapon;
    public Armor equippedArmor;

    [Header("Audio")]
    [HideInInspector] public AudioClip DeathSound;
    [HideInInspector] public AudioClip RespawnSound;

    private AnimatorOverrideController overrideController;
    private DecalGenerator decalGenerator;
    #endregion

    #region State Flags & Properties
    [Header("State Flags")]
    public bool PreventAttack = false;
    public bool PreventMove = false;
    public bool isGuarding = false;
    public bool isParrying = false;
    public bool acquiredParring = false;
    public bool isInteracting = false;
    public bool isCinematic = false;
    public bool isDeathPanelOn = false;
    public bool isAirborne = false;
    public bool isWakingUp = false;

    private Vector3 savedGrabOffset;
    private int detectedByEnemyCount = 0;
    public bool isDetected => detectedByEnemyCount > 0;
    public void AddDetection() => detectedByEnemyCount++;
    public void RemoveDetection() => detectedByEnemyCount = Mathf.Max(0, detectedByEnemyCount - 1);

    private bool hasTriggered66 = false;
    private bool hasTriggered33 = false;

    private float hp;
    public float HP
    {
        get { return hp; }
        set
        {
            float oldHp = hp;
            hp = Mathf.Clamp(value, 0, StatManager.Instance.MaxHp);

            if (hp < oldHp)
            {
                float hpPercentage = hp / StatManager.Instance.MaxHp;
                if (!hasTriggered66 && hpPercentage <= 0.66f)
                {
                    hasTriggered66 = true;
                    if (decalGenerator != null) decalGenerator.GenerateWoundsByPercentage(0.3f);
                }
                if (!hasTriggered33 && hpPercentage <= 0.33f)
                {
                    hasTriggered33 = true;
                    if (decalGenerator != null) decalGenerator.GenerateWoundsByPercentage(0.3f);
                }
            }

            if (hp <= 0 && DeathRoutine == null) DeathRoutine = StartCoroutine(PlayerDead());
            UIManager.Instance.HpText.text = StatManager.Instance.MaxHp.ToString();
        }
    }

    private float mp;
    public float MP
    {
        get { return mp; }
        set
        {
            mp = Mathf.Clamp(value, 0, StatManager.Instance.MaxMp);
            UIManager.Instance.UpdateMpUI(mp / StatManager.Instance.MaxMp);
            UIManager.Instance.MpText.text = StatManager.Instance.MaxMp.ToString();
        }
    }

    public bool IsGrabbed => currentStunRoutine != null && currentStunType == StunType.Grabbed;
    public bool IsAlive => HP > 0f && DeathRoutine == null;
    public bool IsStunned => currentStunRoutine != null;
    public bool isEating => animator.GetCurrentAnimatorStateInfo(1).IsTag("Eat");
    public bool isDashingAnim => animator.GetCurrentAnimatorStateInfo(0).IsTag("Dash");

    public bool CanMove() => IsAlive && !isCinematic && !PreventMove && !IsStunned && !PlayerAttack.isAttacking && !isWakingUp && !isAirborne && !PlayerMove.isKnockedBack;
    public bool CanAttack() => IsAlive && !isCinematic && !PreventAttack && !IsStunned && !isInteracting && !isGuarding && !PlayerAttack.isSwordMoving && !isEating && !isDashingAnim && (!PlayerAttack.isAttacking || PlayerAttack.canComboInput) && !isWakingUp;
    public bool CanGuard() => IsAlive && !isCinematic && !IsStunned && !PreventAttack && !PlayerAttack.isAttacking && !PlayerAttack.isSwordMoving && PlayerAttack.isArmed && !isEating && !isWakingUp;
    public bool CanLockOn() => IsAlive && !isCinematic && !IsStunned && !PlayerAttack.isAttacking && !PlayerAttack.isSwordMoving && !isGuarding && !isParrying && !isEating && !isWakingUp;
    public bool CanDash() => CanMove() && !isCinematic && !PlayerAttack.isAttacking && !isGuarding && !isEating && !isWakingUp;
    public bool CanJump() => CanMove() && !isCinematic && !PlayerAttack.isAttacking && !isGuarding && !isInteracting && !isEating && !PlayerAttack.isSwordMoving && !isWakingUp;
    public bool CanInteract() => IsAlive && !isCinematic && !IsStunned && !PlayerAttack.isAttacking && !isInteracting && !isEating && !isWakingUp;
    public bool CanUseQuickSlot() => IsAlive && !isCinematic && !IsStunned && !PlayerAttack.isAttacking && !isInteracting && !PlayerAttack.isSwordMoving && !isParrying && !isGuarding && !isWakingUp;
    #endregion

    #region Combat & Tuning Settings
    [Header("Combat Tuning")]
    [SerializeField] private float guardBreakShakePower = 15f;
    [SerializeField] private float parryMpReductionMultiplier = 5f;
    [SerializeField] private float grabLookSpeed = 35f;

    [Header("System Timing")]
    [SerializeField] private float deathUIWaitTime = 7f;
    [SerializeField] private float respawnUIWaitTime = 2f;
    [SerializeField] private float interactionSearchRadius = 3.5f;
    [SerializeField] private float interactionViewAngle = 90f;
    [SerializeField] private float interactionSearchDelay = 0.1f;

    [HideInInspector] public bool reachedCheckPoint = false;
    public LayerMask wallLayer;
    #endregion

    #region Constants
    private const float ArmorReductionFactor = 0.06f;
    private const float FootstepCooldown = 0.2f;
    private const float GuardAngleThreshold = 0.7f;
    private const float WallCheckRadius = 0.3f;
    private const float WallCheckSkinWidth = 0.2f;
    private const float WallCheckHeightMin = 0.5f;
    private const float WallCheckHeightMax = 1.5f;
    #endregion

    #region Internal Variables
    public enum StunType { Stagger, Stun, Grabbed }
    public Coroutine currentStunRoutine;
    private Coroutine DeathRoutine;
    private StunType currentStunType;
    private Transform grabTargetTransform;
    private Vector3 grabTargetOffset;
    private bool grabIsRealtimeFollow;

    private float searchTimer;
    private InteractableObj targetInteractableObj;
    private int[] lastSoundFrames = new int[10];
    private float lastFootstepTime = -1f;

    private float _motionTimeOffset = 0f;
    private int currentSkillStateIndex = 1;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Instance = this;
        animator = GetComponent<Animator>();
        decalGenerator = GetComponent<DecalGenerator>();
        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;
        PlayerAttack = GetComponent<PlayerAttack>();
        PlayerMove = GetComponent<PlayerMove>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        transform.rotation = Quaternion.identity;
        GetComponent<Rigidbody>().MoveRotation(Quaternion.identity);
        for (int i = 0; i < lastSoundFrames.Length; i++) lastSoundFrames[i] = -1;
    }

    private void Start()
    {
        try
        {
            HP = StatManager.Instance.MaxHp;
            MP = StatManager.Instance.MaxMp;
            StartCoroutine(FullHpRoutine());
            StartCoroutine(RecoveryRoutine());
            StartCoroutine(LowHpMatRoutine());
            StartCoroutine(InitialAttackPreventRoutine());
        }
        catch { }
    }

    IEnumerator FullHpRoutine()
    {
        yield return null;

        hasTriggered66 = false;
        hasTriggered33 = false;
        decalGenerator?.ClearDecals();

        HP = StatManager.Instance.MaxHp;
        MP = StatManager.Instance.MaxMp;
    }

    private IEnumerator InitialAttackPreventRoutine()
    {
        PreventAttack = true;
        yield return new WaitForSeconds(1f);
        PreventAttack = false;
    }

    private void Update()
    {
        animator.SetFloat("MotionTime", (Time.time - _motionTimeOffset) % 1.0f);
        searchTimer += Time.deltaTime;

        if (searchTimer >= interactionSearchDelay && !isInteracting)
        {
            if (CanInteract())
            {
                searchTimer = 0f;
                targetInteractableObj = GetInteractableObj();
                if (!UIManager.Instance.InteractPanel.activeSelf && targetInteractableObj != null)
                    UIManager.Instance.ShowInteractUI(targetInteractableObj.Type);
                else if (targetInteractableObj == null)
                    UIManager.Instance.InteractPanel.SetActive(false);
            }
        }
    }

    private void OnDisable()
    {
        LowHPMat.SetFloat("_HP", 1f);
    }
    #endregion

    #region Combat Logic
    public void EquipWeapon(Weapon w)
    {
        if (equippedWeapon != null)
        {
            StatManager.Instance.AttackPower -= equippedWeapon.attack;
            equippedWeapon.equipPassive?.skillLogic.Remove();
        }
        equippedWeapon = w;
        if (w != null)
        {
            StatManager.Instance.AttackPower += w.attack * 2;
            w.equipPassive?.skillLogic.Apply();
        }
    }

    public void UnequipWeapon()
    {
        if (equippedWeapon == null) return;
        StatManager.Instance.AttackPower -= equippedWeapon.attack;
        equippedWeapon.equipPassive?.skillLogic.Remove();
        equippedWeapon = null;
    }

    public void EquipArmor(Armor a)
    {
        if (equippedArmor != null)
        {
            StatManager.Instance.Armor -= equippedArmor.defense * 2;
            equippedArmor.equipPassive?.skillLogic.Remove();
        }
        equippedArmor = a;
        if (a != null)
        {
            StatManager.Instance.Armor += a.defense;
            a.equipPassive?.skillLogic.Apply();
        }
    }

    public void UnequipArmor()
    {
        if (equippedArmor == null) return;
        StatManager.Instance.Armor -= equippedArmor.defense;
        equippedArmor.equipPassive?.skillLogic.Remove();
        equippedArmor = null;
    }

    private void TryApplyRandomWound()
    {
        if (UnityEngine.Random.value < 0.3f && decalGenerator != null)
        {
            if (decalGenerator.GetActiveDecalCount() < (decalGenerator.maxDecals * 0.5f))
            {
                decalGenerator.GenerateWound();
            }
        }
    }

    public void TakeDamage(float damage, SkillBase skill, Transform caster, HitEffectData hitEffect = null, int hitIndex = 0)
    {
        if (!IsAlive) return;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            DialogueManager.Instance.StopShowDialogue();
        }

        GuardableData gData = skill.GetGuardData(hitIndex);
        bool skillIsGuardable = gData != null ? gData.guardable : true;
        bool skillIsParryable = gData != null ? gData.parryable : true;
        float ShakePower = 1f;

        if (damage >= (hp * 0.3f)) ShakePower = 3f;
        else if (damage >= (hp * 0.5f)) ShakePower = 10f;
        else if (damage >= (hp * 0.7f)) ShakePower = 20f;

        float actualDamage = ApplyArmorReduction(damage);
        bool willDie = (HP - actualDamage) <= 0;

        if ((isParrying || isGuarding) && skillIsGuardable)
        {
            // 수정됨: Mathf.Abs 제거 (후방 공격 방어 방지)
            if (Vector3.Dot(transform.forward, (caster.position - transform.position).normalized) < GuardAngleThreshold)
            {
                isParrying = false;
                isGuarding = false;
            }
        }

        if (isParrying && skillIsParryable)
        {
            ShakePower = 2f;
            MP += StatManager.Instance.ParryMpRecovery;
            string effectName = skill.SpecialGuardEffect ? skill.SpecialGuardEffect.name : "ParryEffect";
            SkillEffectManager.Instance.GuardEffect(transform.position, transform.eulerAngles, caster, effectName);

            SoundManager.Instance.PlayMovementSound(11);
            Animator_SetTrigger("parrySuccess");

            if (caster.TryGetComponent<EnemyCore>(out var enemy))
            {
                enemy.TakeParry(damage * parryMpReductionMultiplier);
            }

            SpecialSkillManager.Instance.Trigger(transform, SkillBase.TriggerType.OnParry);
            impulseSource.GenerateImpulse(ShakePower);
            return;
        }
        else if (isGuarding && skillIsGuardable)
        {
            ShakePower = 2f;
            if (skill.SpecialGuardEffect) SkillEffectManager.Instance.GuardEffect(transform.position, transform.eulerAngles, caster, skill.SpecialGuardEffect.name);
            else SkillEffectManager.Instance.GuardEffect(transform.position, transform.eulerAngles, caster, "GuardEffect");
            float RemainingDamage = 0;
            if (MP > StatManager.Instance.GuardSuccessCostMpMult * damage)
                MP -= StatManager.Instance.GuardSuccessCostMpMult * damage;
            else
            {
                RemainingDamage = damage - MP / StatManager.Instance.GuardSuccessCostMpMult;
                MP = 0;
            }

            if (MP <= 0)
            {
                impulseSource.GenerateImpulse(guardBreakShakePower);
                SoundManager.Instance.PlayMovementSound(10);
                float actualRemainingDamage = ApplyArmorReduction(RemainingDamage);

                if (HP - actualRemainingDamage > 0)
                {
                    StartCoroutine(Animator_SetBool_Time("GuardBreak"));
                }

                if (actualRemainingDamage > 0) TryApplyRandomWound();
                HP -= actualRemainingDamage;

                if (hitEffect != null) InkBrushEffectManager.Instance.PlayHitEffect(hitEffect);
                return;
            }
            else
            {
                impulseSource.GenerateImpulse(ShakePower);
                SoundManager.Instance.PlayMovementSound(9);
                Animator_SetFloat("Random", UnityEngine.Random.Range(-1, 1));
                StartCoroutine(Animator_SetBool_Time("GuardHitted"));
            }
            return;
        }

        bool isStunApplied = false;
        if (skill.GetStunData(hitIndex) != null)
        {
            StunEffectData stunData = skill.GetStunData(hitIndex);
            if (stunData != null && stunData.stunDuration > 0 && !willDie)
            {
                bool isDown = PlayerMove != null && (PlayerMove.isKnockedDown || isAirborne || isWakingUp);
                if (!(isDown && stunData.stunType != StunType.Grabbed))
                {
                    Transform grabTarget = null;
                    if (stunData.stunType == StunType.Grabbed)
                    {
                        grabTarget = FindChildRecursive(caster, "GrabPosition_" + skill.name) ?? caster;
                    }
                    ApplyStun(stunData.stunDuration, stunData.stunType, grabTarget, stunData.offset, stunData.isRealtimeFollow);
                    ShakePower = ShakePower < 20 ? ShakePower + 5f : 20f;
                    isStunApplied = true;
                }
            }
        }

        bool hasKnockback = false;
        if (skill is Skill_Melee meleeSkill && meleeSkill.hitDataList != null && meleeSkill.hitDataList.Count > hitIndex)
        {
            hasKnockback = meleeSkill.hitDataList[hitIndex].knockbackForce.magnitude > PlayerMove.knockbackForceThreshold;
        }
        else if (skill is Skill_Range rangeSkill && rangeSkill.hitDataList != null && rangeSkill.hitDataList.Count > hitIndex)
        {
            hasKnockback = rangeSkill.hitDataList[hitIndex].knockbackForce.magnitude > PlayerMove.knockbackForceThreshold;
        }

        if (!isStunApplied && !willDie && !hasKnockback)
        {
            CancelCurrentAction();

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool isPlayingKnockMotion = state.IsName("KnockBack") || state.IsName("KnockBackEnd") ||
                                        state.IsName("KnockDown1") || state.IsName("KnockDown1End") ||
                                        state.IsName("KnockDown2") || state.IsName("KnockDown2End") ||
                                        PlayerMove.isKnockedBack;

            if (animator != null && currentStunRoutine == null && !isAirborne && !isWakingUp && !isPlayingKnockMotion)
            {
                animator.ResetTrigger("Hitted1");
                animator.ResetTrigger("Hitted2");
                Animator_SetTrigger("Hitted" + UnityEngine.Random.Range(1, 3));
            }
        }

        if (skill.HitSound != null && skill.HitSound.Length > hitIndex && skill.HitSound[hitIndex])
            SoundManager.Instance.PlaySound(skill.HitSound[hitIndex]);

        if (hitEffect != null)
            InkBrushEffectManager.Instance.PlayHitEffect(hitEffect);

        impulseSource.GenerateImpulse(ShakePower);

        if (actualDamage > 0) TryApplyRandomWound();
        HP -= actualDamage;

        if (caster.TryGetComponent<EnemyCore>(out var enemyCore))
        {
            enemyCore.TriggerPlayerHit(skill, hitIndex);
        }
    }

    public void TakeFallDamage(float damage)
    {
        if (!IsAlive || damage <= 0) return;

        CancelCurrentAction();

        bool willDie = (HP - damage) <= 0;
        HP -= damage;

        if (!willDie)
        {
            impulseSource.GenerateImpulse(5f);
            SoundManager.Instance.PlayMovementSound(10);
        }
    }

    public void ClearStun()
    {
        if (currentStunRoutine != null)
        {
            StopCoroutine(currentStunRoutine);
            currentStunRoutine = null;
        }

        currentStunType = StunType.Stagger;
        animator.SetBool("isGrabbed", false);
        animator.ResetTrigger("Stagger");
    }

    public void ApplyStun(float stunDuration, StunType type, Transform targetTransform = null, Vector3 grabOffset = default, bool isRealtimeFollow = true)
    {
        CancelCurrentAction();
        ClearStun();

        currentStunType = type;
        grabTargetTransform = targetTransform;
        grabTargetOffset = grabOffset;
        grabIsRealtimeFollow = isRealtimeFollow;

        if (type == StunType.Grabbed && targetTransform != null)
        {
            Vector3 worldTarget = targetTransform.TransformPoint(grabOffset);
            savedGrabOffset = targetTransform.root.InverseTransformPoint(worldTarget);
            transform.position = worldTarget;
        }

        currentStunRoutine = StartCoroutine(StunRoutine(stunDuration, type, targetTransform));
    }

    private IEnumerator StunRoutine(float stunDuration, StunType type, Transform targetTransform)
    {
        PreventMove = true; PreventAttack = true;
        PlayerAttack.ClearCommandBuffer();

        animator.ResetTrigger("StaggerEnd");
        animator.ResetTrigger("Hitted1");
        animator.ResetTrigger("Hitted2");

        switch (type)
        {
            case StunType.Stagger: Animator_SetTrigger("Stagger"); break;
            case StunType.Stun: Animator_SetTrigger("Stagger"); break;
            case StunType.Grabbed: Animator_SetBool("isGrabbed", true); break;
        }

        float elapsed = 0f;
        while (elapsed < stunDuration)
        {
            if (type == StunType.Grabbed && grabTargetTransform != null && grabIsRealtimeFollow)
            {
                savedGrabOffset = grabTargetTransform.root.rotation * grabTargetOffset;
                transform.position = grabTargetTransform.position + savedGrabOffset;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (type == StunType.Grabbed)
        {
            if (PlayerMove._rb != null)
            {
                PlayerMove._rb.linearVelocity = Vector3.zero;
                PlayerMove._rb.position = transform.position;
            }
        }

        animator.ResetTrigger("Stagger");

        switch (type)
        {
            case StunType.Stagger: Animator_SetTrigger("StaggerEnd"); break;
            case StunType.Stun: Animator_SetTrigger("StaggerEnd"); break;
            case StunType.Grabbed: Animator_SetBool("isGrabbed", false); break;
        }

        currentStunRoutine = null;

        if (!PlayerMove.isKnockedBack && !isWakingUp)
        {
            PreventMove = false;
            PreventAttack = false;
        }

        if (type == StunType.Grabbed && grabTargetTransform != null && grabIsRealtimeFollow)
        {
            savedGrabOffset = grabTargetTransform.root.rotation * grabTargetOffset;
            transform.position = grabTargetTransform.position + savedGrabOffset;
        }
    }

    public void StartWakeUp(float animDuration)
    {
        CancelCurrentAction();
        ClearStun();
        StartCoroutine(WakeUpRoutine(animDuration));
    }

    private IEnumerator WakeUpRoutine(float animDuration)
    {
        isWakingUp = true;
        isAirborne = false;

        Animator_SetTrigger("WakeUp");

        yield return new WaitForSeconds(animDuration);

        isWakingUp = false;

        if (animator != null && animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, 1f);
        }
    }

    private float mpRecoveryDelay;
    public void mpRecoveryDelayStart()
    {
        mpRecoveryDelay = 1f;
    }
    IEnumerator RecoveryRoutine()
    {
        while (true)
        {
            float mult = isGuarding ? 0.1f : 1f;

            HP += StatManager.Instance.HpRecoveryAmount * Time.deltaTime * mult;

            if (mpRecoveryDelay > 0)
            {
                mpRecoveryDelay -= Time.deltaTime;
            }
            else
            {
                MP += StatManager.Instance.MpRecoveryAmount * Time.deltaTime * mult;
            }

            yield return null;
        }
    }

    private float ApplyArmorReduction(float incoming)
    {
        if (StatManager.Instance.Armor <= 0f) return incoming;
        float reduction = (StatManager.Instance.Armor * ArmorReductionFactor) / (1f + ArmorReductionFactor * StatManager.Instance.Armor);
        return incoming * (1f - reduction);
    }

    public void CancelCurrentAction()
    {
        if (PlayerAttack != null)
        {
            PlayerAttack.isAttacking = false;
            PlayerAttack.isSwordMoving = false;
            PlayerAttack.CancelCurrentSkill();
        }
    }

    public IEnumerator HpRecoveryRoutine()
    {
        while (true)
        {
            if (HP < StatManager.Instance.MaxHp * 0.4 && reachedCheckPoint)
            {
                while (HP < StatManager.Instance.MaxHp * 0.7)
                {
                    HP += StatManager.Instance.MaxHp * Time.deltaTime / 3;
                    yield return null;
                }
                reachedCheckPoint = false;
            }
            yield return null;
        }
    }

    public void TakeArbitraryDamage(int damage)
    {
        HP -= damage;
    }
    #endregion

    #region Interaction & System
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed && CanInteract())
        {
            if (targetInteractableObj != null && targetInteractableObj.GetComponent<InteractableObj>() != null)
            {
                Animator_Play("");
                targetInteractableObj.GetComponent<InteractableObj>().Interact();
            }
        }
        if (context.performed && isDeathPanelOn)
        {
            Respawn();
        }
    }

    public void OnUseQuickSlot(InputAction.CallbackContext context)
    {
        if (context.performed && CanUseQuickSlot())
        {
            UIManager.Instance.CurrentSlot.GetComponent<QuickSlot>().OnClickQuickSlot();
        }
    }

    public void OnScreenshot(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            StartCoroutine(CaptureCoroutine());
        }
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Vector2 scroll = context.ReadValue<Vector2>();
        }
    }

    public InteractableObj GetInteractableObj()
    {
        Vector3 searchCenter = transform.position - transform.forward * 1f;
        Collider[] hits = Physics.OverlapSphere(searchCenter, interactionSearchRadius, LayerMask.GetMask("InteractableObj"));

        InteractableObj closestInteractableObj = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            InteractableObj interactableObj = hit.GetComponent<InteractableObj>();
            if (interactableObj == null || !interactableObj.isInteractable) continue;

            Vector3 direction = (interactableObj.transform.position - searchCenter).normalized;
            float angle = Vector3.Angle(transform.forward, direction);

            if (angle > interactionViewAngle * 0.5f) continue;

            float distance = Vector3.Distance(searchCenter, interactableObj.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestInteractableObj = interactableObj;
            }
        }

        return closestInteractableObj;
    }

    public void SetCinematicMode(bool isActive, bool rootMotion)
    {
        isCinematic = isActive;

        if (animator != null) animator.applyRootMotion = rootMotion;
        if (CameraController.Instance != null) CameraController.Instance.SetCinematicCamera(isActive);

        if (isActive)
        {
            if (PlayerMove != null && PlayerMove._rb != null)
            {
                PlayerMove._rb.linearVelocity = Vector3.zero;
                PlayerMove._rb.interpolation = RigidbodyInterpolation.None;
            }

            Animator_SetFloat("inputX", 0f);
            Animator_SetFloat("inputY", 0f);
            Animator_SetFloat("MoveSpeed", 0f);

            if (PlayerAttack != null && PlayerAttack.isLockedOn) PlayerAttack.DisableLockOn();
            if (isGuarding)
            {
                isGuarding = false;
                Animator_SetBool("isGuard", false);
            }
        }
        else
        {
            if (PlayerMove != null && PlayerMove._rb != null)
            {
                PlayerMove._rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }
    }

    private IEnumerator CaptureCoroutine()
    {
        yield return new WaitForEndOfFrame();

        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture.Apply();

        byte[] pngBytes = texture.EncodeToPNG();
        string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "screenshot");

        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, "screenshot.png");
        File.WriteAllBytes(filePath, pngBytes);

        Destroy(texture);
    }

    IEnumerator PlayerDead()
    {
        CancelCurrentAction();

        PreventMove = true;
        PreventAttack = true;
        if (PlayerAttack != null) PlayerAttack.DisableLockOn();
        Animator_SetTrigger("isDead");
        InkBrushEffectManager.Instance.PlayDeathEffect();
        SettingManager.Instance.SetSensitivityZero();
        SoundManager.Instance.PlaySound(DeathSound);

        yield return new WaitForSeconds(deathUIWaitTime);
        UIManager.Instance.DeathPanel.SetActive(true);

        yield return new WaitForSeconds(respawnUIWaitTime);
        isDeathPanelOn = true;
    }

    public void Respawn(bool RespawnCheckpoint = true)
    {
        UIManager.Instance.DeathPanel.GetComponent<DeathPanel>().HidePanel();
        InkBrushEffectManager.Instance.ClearDeathEffects();
        UIManager.Instance.BossStatus.gameObject.SetActive(false);

        if (RespawnCheckpoint) RespawnToCheckpoint();
        DungeonManager.Instance.ReviveAllEnemies();

        decalGenerator?.ClearDecals();
        hasTriggered66 = false;
        hasTriggered33 = false;

        HP = StatManager.Instance.MaxHp;
        MP = StatManager.Instance.MaxMp;
        PreventMove = false;
        PreventAttack = false;
        isGuarding = false;
        isParrying = false;
        isAirborne = false;
        isWakingUp = false;
        if (PlayerMove != null)
        {
            PlayerMove.isKnockedBack = false;
            PlayerMove.isKnockedDown = false;
        }

        if (PlayerAttack != null)
        {
            PlayerAttack.isLockedOn = false;
            PlayerAttack.ForceUnarmed();
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            if (animator.layerCount > 1) animator.SetLayerWeight(1, 1f);
        }

        detectedByEnemyCount = 0;
        isDeathPanelOn = false;
        DeathRoutine = null;
        SoundManager.Instance.PlaySound(RespawnSound);
        SettingManager.Instance.RestoreSensitivity();

        StartCoroutine(InitialAttackPreventRoutine());
    }

    private void RespawnToCheckpoint()
    {
        Vector3 respawn = DungeonManager.Instance.GetRespawnTransform();

        if (respawn != null)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                transform.position = respawn;
                rb.position = respawn;
            }
            else transform.position = respawn + Vector3.up * 3;
        }
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    #endregion

    #region Animation & Movement
    public void ResetMotionTime()
    {
        _motionTimeOffset = Time.time;
        animator.SetFloat("MotionTime", 0);
    }

    public void Animator_Play(string clipName) => animator.Play(clipName);
    public void Animator_SetBool(string boolName, bool state) => animator.SetBool(boolName, state);
    public void Animator_SetTrigger(string triggerName) => animator.SetTrigger(triggerName);
    public void Animator_SetFloat(string floatName, float value, float damp = 0f, float time = 0f) => animator.SetFloat(floatName, value, damp, time);

    public void Animator_PlaySkill(SkillBase skill, float crossFadeDuration = 0.1f)
    {
        animator.ResetTrigger("parrySuccess");

        string targetState = "SkillState" + currentSkillStateIndex;
        overrideController[targetState] = skill.clip;

        animator.CrossFadeInFixedTime(targetState, crossFadeDuration, 0);
        animator.CrossFadeInFixedTime(targetState, crossFadeDuration, 1);

        currentSkillStateIndex = currentSkillStateIndex == 1 ? 2 : 1;
    }

    public IEnumerator Animator_SetBool_Time(string boolName)
    {
        Animator_SetBool(boolName, true);
        yield return new WaitForSeconds(0.1f);
        Animator_SetBool(boolName, false);
    }

    public virtual IEnumerator WaitUntilAnimationEnd(Action action)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        while (animator.IsInTransition(0)) yield return null;

        while (true)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if ((state.IsName("SkillState1") || state.IsName("SkillState2")) && state.normalizedTime < 0.95f) yield return null;
            else break;
        }
        action?.Invoke();
    }

    public void PlayMovementSounds(AnimationEvent animEvent)
    {
        if (!animator.GetCurrentAnimatorStateInfo(0).IsTag("Movement") || animEvent.animatorClipInfo.weight < 0.15f) return;

        int soundIndex = animEvent.intParameter;
        if (soundIndex < 0 || soundIndex >= lastSoundFrames.Length) return;

        if (soundIndex == 0 || soundIndex == 1)
        {
            if (Time.time - lastFootstepTime < FootstepCooldown) return;
            lastFootstepTime = Time.time;
        }
        else
        {
            if (Time.frameCount == lastSoundFrames[soundIndex]) return;
            lastSoundFrames[soundIndex] = Time.frameCount;
        }
        SoundManager.Instance.PlayMovementSound(soundIndex);
    }

    private void OnAnimatorMove()
    {
        if (isCinematic)
        {
            if (animator != null && animator.applyRootMotion)
            {
                if (PlayerMove != null && PlayerMove._rb != null)
                {
                    PlayerMove._rb.MovePosition(PlayerMove._rb.position + animator.deltaPosition);
                    PlayerMove._rb.MoveRotation(PlayerMove._rb.rotation * animator.deltaRotation);
                }
            }
            return;
        }

        if (animator == null || isCinematic || (currentStunRoutine != null && currentStunType == StunType.Grabbed))
        {
            return;
        }

        Vector3 delta = animator.deltaPosition;
        if (delta.AlmostZero()) return;
        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
        float distance = horizontalDelta.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 dir = horizontalDelta.normalized;
            Vector3 point1 = transform.position + Vector3.up * WallCheckHeightMin;
            Vector3 point2 = transform.position + Vector3.up * WallCheckHeightMax;

            if (Physics.CapsuleCast(point1, point2, WallCheckRadius, dir, out RaycastHit hit, distance + WallCheckSkinWidth, wallLayer))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - WallCheckSkinWidth);
                Vector3 safeMove = dir * safeDistance;
                Vector3 remainingMove = dir * (distance - safeDistance);
                Vector3 slideMove = Vector3.ProjectOnPlane(remainingMove, hit.normal);
                slideMove.y = 0f;

                horizontalDelta = safeMove + slideMove;
            }
            delta = new Vector3(horizontalDelta.x, delta.y, horizontalDelta.z);
        }

        PlayerMove._rb.MovePosition(PlayerMove._rb.position + delta);
        PlayerMove._rb.MoveRotation(PlayerMove._rb.rotation * animator.deltaRotation);
    }

    IEnumerator LowHpMatRoutine()
    {
        while (true)
        {
            float max = StatManager.Instance.MaxHp;
            float ratio = Mathf.Clamp01(HP / max);

            if (ratio >= 0.98f)
            {
                if (LowHPMat.GetFloat("_HP") != 1f) LowHPMat.SetFloat("_HP", 1f);
                yield return null;
                continue;
            }

            float period = ratio >= 0.5f ? 2.0f : ratio >= 0.1f ? 2f : 1f;
            float amplitude = ratio >= 0.5f ? 0.025f : ratio >= 0.1f ? 0.0175f : 0.005f;
            float wave = Mathf.Sin(Time.time * (2f * Mathf.PI / period)) * amplitude;

            LowHPMat.SetFloat("_HP", ratio + wave);
            yield return null;
        }
    }
    #endregion
}
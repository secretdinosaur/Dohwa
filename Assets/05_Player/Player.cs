using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class Player : MonoBehaviour, IDamageable, IBleedable
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
    public bool isInvincible = false;

    [HideInInspector] public bool isTakingDoT = false;

    public event Action OnParrySuccess;

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

            List<SkillBase.Command> commandBuffer = PlayerAttack.GetCommanBuffer();
            SkillBase matched = null;
            int matchedLen = 0;

            for (int i = 0; i < PlayerAttack.acquiredSkills.Count; i++)
            {
                var skill = PlayerAttack.acquiredSkills[i];
                if (skill == null) continue;

                while (skill != null && skill.UpgradedSkill != null && skill.isUpgraded) skill = skill.UpgradedSkill;

                if (skill.isSpecialSkill || skill.commands == null || skill.commands.Length == 0) continue;

                int m = skill.commands.Length;
                if (commandBuffer.Count < m) continue;

                bool match = true;
                for (int j = 0; j < m; j++)
                {
                    if (commandBuffer[commandBuffer.Count - m + j] != skill.commands[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match && m > matchedLen)
                {
                    matched = skill;
                    matchedLen = m;
                }
            }
            if(matched != null)
            {
                if (matched.MP_cost > mp)
                    UIManager.Instance.AvailableCommandsText.color = new Color32(109, 109, 109, 255);
                else
                    UIManager.Instance.AvailableCommandsText.color = new Color32(255, 255, 255, 255);
            }
        }
    }

    public void ConsumeMP(float amount)
    {
        MP -= amount * (1 - StatManager.Instance.MpConsumptionReduction);
    }

    public bool IsGrabbed => currentStunRoutine != null && currentStunType == StunType.Grabbed;
    public bool IsAlive => HP > 0f && DeathRoutine == null;
    public bool IsStunned => currentStunRoutine != null;
    public bool isEating => animator.GetCurrentAnimatorStateInfo(1).IsTag("Eat");
    public bool isDashingAnim => animator.GetCurrentAnimatorStateInfo(0).IsTag("Dash");

    private bool CanActBase => IsAlive && !isCinematic && !IsStunned && !isWakingUp;

    public bool CanMove() => CanActBase && !PreventMove && !PlayerAttack.isAttacking && !isAirborne && !PlayerMove.isKnockedBack;

    public bool CanAttack() => CanActBase && !PreventAttack && !isInteracting && !isGuarding && !PlayerAttack.isSwordMoving && !isEating && !isDashingAnim && (!PlayerAttack.isAttacking || PlayerAttack.canComboInput) && !isAirborne && !PlayerMove.isKnockedDown;

    public bool CanGuard() => CanActBase && !PreventAttack && !PlayerAttack.isAttacking && !PlayerAttack.isSwordMoving && PlayerAttack.isArmed && !isEating && !isAirborne && !PlayerMove.isKnockedDown;

    public bool CanLockOn() => CanActBase && !PlayerAttack.isAttacking && !PlayerAttack.isSwordMoving && !isGuarding && !isParrying && !isEating && !isAirborne && !PlayerMove.isKnockedDown;

    public bool CanDash() => CanMove() && !isGuarding && !isEating;

    public bool CanJump() => CanMove() && !isGuarding && !isInteracting && !isEating && !PlayerAttack.isSwordMoving;

    public bool CanInteract() => CanActBase && !PlayerAttack.isAttacking && !isInteracting && !isEating;

    public bool CanUseQuickSlot() => CanActBase && !PlayerAttack.isAttacking && !isInteracting && !PlayerAttack.isSwordMoving && !isParrying && !isGuarding && !isAirborne && !PlayerMove.isKnockedDown;
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
    public const float ArmorReductionFactor = 0.005f;
    private const float FootstepCooldown = 0.2f;
    private const float GuardAngleThreshold = 0.7f;
    public float WallCheckRadius = 0.3f;
    public float WallCheckSkinWidth = 0.2f;
    private const float WallCheckHeightMin = 0.5f;
    private const float WallCheckHeightMax = 1.5f;
    #endregion

    #region Internal Variables
    public enum StunType { Stagger, Stun, Grabbed }
    public Coroutine currentStunRoutine;
    private Coroutine DeathRoutine;
    private Coroutine EatRoutine;
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

    private int lastStunFrame = -1;

    private float savedInputX;
    private float savedInputY;
    private float savedMoveSpeed;
    private float savedIsArmed;
    private bool isGrabStateSaved = false;
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
        HP = StatManager.Instance.MaxHp;
        MP = StatManager.Instance.MaxMp;
        StartCoroutine(FullHpRoutine());
        StartCoroutine(RecoveryRoutine());
        StartCoroutine(LowHpMatRoutine());
        StartCoroutine(InitialAttackPreventRoutine());
    }

    IEnumerator FullHpRoutine()
    {
        yield return null;
        yield return null;
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
        yield return new WaitForSeconds(3f);
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
            StatManager.Instance.Armor -= equippedArmor.defense;
            StatManager.Instance.MaxHp -= equippedArmor.hp;
            equippedArmor.equipPassive?.skillLogic.Remove();
        }
        equippedArmor = a;
        if (a != null)
        {
            StatManager.Instance.MaxHp += a.hp * 2;
            StatManager.Instance.Armor += a.defense * 2;
            a.equipPassive?.skillLogic.Apply();
        }
    }

    public void UnequipArmor()
    {
        if (equippedArmor == null) return;
        StatManager.Instance.MaxHp -= equippedArmor.hp;
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
        if (!IsAlive || isInvincible) return;

        bool currentIsUnstoppable = false;
        bool currentIsSuperUnstoppable = false; // 💡 추가됨
        float currentDamageReduction = 0f;

        if (PlayerAttack != null && PlayerAttack.isAttacking && PlayerAttack.lastActivatedSkill != null)
        {
            currentIsUnstoppable = PlayerAttack.lastActivatedSkill.isUnstoppable;
            currentIsSuperUnstoppable = PlayerAttack.lastActivatedSkill.isSuperUnstoppable;
            currentDamageReduction = PlayerAttack.lastActivatedSkill.damageReductionDuringCast;
        }

        // 💡 슈퍼 아머나 하이퍼 아머 상태가 아닐 때만 캔슬
        if (!isTakingDoT && !currentIsUnstoppable && !currentIsSuperUnstoppable)
        {
            if (PlayerAttack != null) PlayerAttack.CancelCurrentSkill();
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
                DialogueManager.Instance.StopShowDialogue();
        }

        GuardableData gData = skill.GetGuardData(hitIndex);
        bool skillIsGuardable = gData != null ? gData.guardable : true;
        bool skillIsParryable = gData != null ? gData.parryable : true;

        skillIsGuardable = isTakingDoT ? false : skillIsGuardable;
        skillIsParryable = isTakingDoT ? false : skillIsParryable;

        float ShakePower = 1f;
        if (damage >= (hp * 0.3f)) ShakePower = 3f;
        else if (damage >= (hp * 0.5f)) ShakePower = 10f;
        else if (damage >= (hp * 0.7f)) ShakePower = 20f;

        float actualDamage = isTakingDoT ? damage : ApplyArmorReduction(damage);

        // 방해불가/슈퍼방해불가 데미지 감소 적용
        actualDamage *= (1f - currentDamageReduction);

        bool willDie = (HP - actualDamage) <= 0;

        if ((isParrying || isGuarding) && skillIsGuardable)
        {
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
            Animator_SetFloat("Random", UnityEngine.Random.Range(-1, 1));
            StartCoroutine(Animator_SetBool_Time("GuardHitted"));

            if (caster.TryGetComponent<EnemyCore>(out var enemy))
            {
                enemy.TakeParry(damage * parryMpReductionMultiplier);
            }

            SpecialSkillManager.Instance.Trigger(transform, SkillBase.TriggerType.OnParry);

            OnParrySuccess?.Invoke();

            impulseSource.GenerateImpulse(ShakePower);
            return;
        }
        else if (isGuarding && skillIsGuardable)
        {
            ShakePower = 2f;
            if (skill.SpecialGuardEffect) SkillEffectManager.Instance.GuardEffect(transform.position, transform.eulerAngles, caster, skill.SpecialGuardEffect.name);
            else SkillEffectManager.Instance.GuardEffect(transform.position, transform.eulerAngles, caster, "GuardEffect");

            float RemainingDamage = 0;
            float mpCost = StatManager.Instance.GuardSuccessCostMpMult * damage;

            if (MP > mpCost)
                ConsumeMP(mpCost);
            else
            {
                RemainingDamage = damage - MP / StatManager.Instance.GuardSuccessCostMpMult;
                ConsumeMP(MP);
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

        bool hasKnockback = false;
        Vector3 knockbackForce = Vector3.zero;

        if (skill is Skill_Melee meleeSkill && meleeSkill.hitDataList != null && meleeSkill.hitDataList.Count > hitIndex && !isTakingDoT)
        {
            knockbackForce = meleeSkill.hitDataList[hitIndex].knockbackForce;
            hasKnockback = knockbackForce.magnitude > PlayerMove.knockbackForceThreshold;
        }
        else if (skill is Skill_Range rangeSkill && rangeSkill.hitDataList != null && rangeSkill.hitDataList.Count > hitIndex && !isTakingDoT)
        {
            knockbackForce = rangeSkill.hitDataList[hitIndex].knockbackForce;
            hasKnockback = knockbackForce.magnitude > PlayerMove.knockbackForceThreshold;
        }

        bool isStunApplied = false;
        if (skill.GetStunData(hitIndex) != null && !isTakingDoT)
        {
            StunEffectData stunData = skill.GetStunData(hitIndex);
            if (stunData != null && stunData.stunDuration > 0 && !willDie)
            {
                // 💡 [SuperUnstoppable 1] 슈퍼 방해불가 상태면 하드 CC(스턴, 잡기)를 무시함
                if (!currentIsSuperUnstoppable)
                {
                    if (!hasKnockback && PlayerMove != null)
                        PlayerMove.CancelKnockBack();

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

        // 3. 일반 피격 및 넉백 분기 처리
        if (!isStunApplied && !isTakingDoT) // (수정됨) willDie 조건 제거: 죽을 때도 넉백되도록 허용
        {
            if (hasKnockback)
            {
                // [SuperUnstoppable 2] 슈퍼 방해불가 상태면 넉백, 에어본도 무시함
                if (!currentIsSuperUnstoppable)
                {
                    CancelCurrentAction();
                    ClearStun();

                    if (knockbackForce.y > 0)
                    {
                        isAirborne = true;
                        if (animator != null) Animator_SetTrigger("Airborne");
                    }
                    else
                    {
                        if (PlayerMove != null) PlayerMove.isKnockedDown = true;
                        if (animator != null) Animator_SetTrigger("KnockDown");
                    }
                }
            }
            else if (!willDie) // 💡 (추가됨) 넉백 없는 일반 공격에 맞아 죽을 때는 경직 모션 없이 바로 죽음 처리
            {
                // 일반 경직 피격일 때
                if (!currentIsUnstoppable && !currentIsSuperUnstoppable)
                {
                    CancelCurrentAction();
                    ClearStun();

                    if (PlayerMove != null) PlayerMove.CancelKnockBack();

                    if (animator != null)
                    {
                        animator.ResetTrigger("Hitted1");
                        animator.ResetTrigger("Hitted2");
                        Animator_SetTrigger("Hitted" + UnityEngine.Random.Range(1, 3));
                    }
                }
            }
        }

        if (!isTakingDoT)
        {
            if (skill.HitSound != null && skill.HitSound.Length > hitIndex && skill.HitSound[hitIndex])
                SoundManager.Instance.PlaySound(skill.HitSound[hitIndex]);

            impulseSource.GenerateImpulse(ShakePower);
        }

        if (hitEffect != null && !isTakingDoT)
            InkBrushEffectManager.Instance.PlayHitEffect(hitEffect);

        if (actualDamage > 0 && !isTakingDoT) TryApplyRandomWound();
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
        if (Time.frameCount == lastStunFrame) return;
        ForceClearStun();
    }

    private void RestoreGrabState()
    {
        if (isGrabStateSaved)
        {
            Animator_SetFloat("inputX", savedInputX);
            Animator_SetFloat("inputY", savedInputY);
            Animator_SetFloat("MoveSpeed", savedMoveSpeed);
            Animator_SetFloat("isArmed", savedIsArmed);
            isGrabStateSaved = false;
        }
    }

    private void ForceClearStun()
    {
        if (currentStunRoutine != null)
        {
            StopCoroutine(currentStunRoutine);
            currentStunRoutine = null;
        }

        RestoreGrabState();

        currentStunType = StunType.Stagger;
        animator.SetBool("isGrabbed", false);
        animator.ResetTrigger("Stagger");

        PreventMove = false;
        PreventAttack = false;
    }

    public void ApplyStun(float stunDuration, StunType type, Transform targetTransform = null, Vector3 grabOffset = default, bool isRealtimeFollow = true)
    {
        CancelCurrentAction();
        ForceClearStun();

        lastStunFrame = Time.frameCount;

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
            case StunType.Grabbed:
                if (!isGrabStateSaved)
                {
                    savedInputX = animator.GetFloat("inputX");
                    savedInputY = animator.GetFloat("inputY");
                    savedMoveSpeed = animator.GetFloat("MoveSpeed");
                    savedIsArmed = animator.GetFloat("isArmed");
                    isGrabStateSaved = true;
                }
                Animator_SetFloat("inputX", 0f);
                Animator_SetFloat("inputY", 0f);
                Animator_SetFloat("MoveSpeed", 0f);
                Animator_SetFloat("isArmed", 0f);

                Animator_Play("MoveTree");
                Animator_SetBool("isGrabbed", true);
                break;
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
            case StunType.Grabbed:
                Animator_SetBool("isGrabbed", false);
                RestoreGrabState();
                break;
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
        ForceClearStun();
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
        mpRecoveryDelay = 0.8f;
    }
    IEnumerator RecoveryRoutine()
    {
        while (true)
        {
            float mult = isGuarding ? 0.1f : 1f;

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

        float reduction = 0.15f * Mathf.Log(1f + 0.01f * StatManager.Instance.Armor);

        reduction = Mathf.Min(reduction, 0.8f);

        return incoming * (1f - reduction);
    }

    public void CancelCurrentAction()
    {
        if (PlayerAttack != null)
        {
            PlayerAttack.isAttacking = false;
            PlayerAttack.isSwordMoving = false;
            PlayerAttack.CancelDrawSword();
            PlayerAttack.CancelCurrentSkill();
        }

        isInvincible = false;
    }

    public IEnumerator HpRecoveryRoutine()
    {
        while (true)
        {
            if (HP < StatManager.Instance.MaxHp * 0.4 && reachedCheckPoint)
            {
                while (HP < StatManager.Instance.MaxHp * 0.7)
                {
                    HP += StatManager.Instance.MaxHp * Time.deltaTime / 3 * StatManager.Instance.HpRecoveryAmount;
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

    public EnemyCore GetTargetForStrongAttack(float searchRadius = 5f, int enemyLayerMask = -1)
    {
        enemyLayerMask = LayerMask.GetMask("Enemy");

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayerMask);
        EnemyCore closestVulnerableEnemy = null;
        float minDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            EnemyCore enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && enemy.currentState == EnemyState.Stun && !enemy.IsDead)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestVulnerableEnemy = enemy;
                }
            }
        }
        return closestVulnerableEnemy;
    }

    private Coroutine bleedCoroutine;

    public void ApplyBleed(int ticksPerSecond, SkillBase bleedSkill, HitEffectData bleedEffect, SkillBase.EnemyHitEffectDirection effectDir)
    {
        if (HP <= 0) return;

        if (bleedCoroutine != null)
        {
            StopCoroutine(bleedCoroutine);
        }
        bleedCoroutine = StartCoroutine(BleedRoutine(ticksPerSecond, bleedSkill, bleedEffect, effectDir));
    }

    private IEnumerator BleedRoutine(int ticksPerSecond, SkillBase bleedSkill, HitEffectData bleedEffect, SkillBase.EnemyHitEffectDirection effectDir)
    {
        float interval = 1f / ticksPerSecond;
        int totalTicks = 10 * ticksPerSecond;

        for (int i = 0; i < totalTicks; i++)
        {
            if (HP <= 0)
            {
                bleedCoroutine = null;
                yield break;
            }

            float tickDamage = (HP * 0.1f) / totalTicks;

            isTakingDoT = true;
            TakeDamage(tickDamage * (1 - StatManager.Instance.BleedResistance), bleedSkill, transform, bleedEffect, 0);
            isTakingDoT = false;

            PlayBleedHitEffect(bleedSkill, effectDir);
            yield return new WaitForSeconds(interval);
        }

        bleedCoroutine = null;
    }

    private void PlayBleedHitEffect(SkillBase skill, SkillBase.EnemyHitEffectDirection effectDir)
    {
        if (SkillEffectManager.Instance == null) return;

        Vector3 hitPoint = GetComponent<Collider>().bounds.center;
        Quaternion effectRotation = CalculateBleedRotation(effectDir);
        SkillEffectManager.Instance.PlayHitEffect(gameObject, skill, hitPoint, effectRotation, 0);
    }

    private Quaternion CalculateBleedRotation(SkillBase.EnemyHitEffectDirection effectDir)
    {
        int dirIndex = (int)effectDir - 1;
        if (dirIndex <= 11)
        {
            float rotY = transform.eulerAngles.y;
            float rotX = dirIndex * 30f;
            return Quaternion.Euler(rotX, rotY - 90f, 0f);
        }

        Vector3 effectDirection = -transform.forward;
        Vector3 effectUp = transform.up;

        if (effectDir.ToString() == "Forward")
        {
            effectDirection = transform.up;
            effectUp = transform.forward;
        }
        else if (effectDir.ToString() == "Backward")
        {
            effectDirection = transform.up;
            effectUp = -transform.forward;
        }

        if (effectDirection == Vector3.zero) effectDirection = -transform.forward;
        return Quaternion.LookRotation(effectDirection, effectUp);
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
            CancelCurrentAction();

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
        // 💡 넉백/에어본 상태에서 죽었다면 땅에 떨어져 뒹굴 때까지 대기
        if (isAirborne || (PlayerMove != null && (PlayerMove.isKnockedBack || PlayerMove.isKnockedDown)))
        {
            while (true)
            {
                if (PlayerMove == null) break;
                if (PlayerMove.isGrounded) break;

                // 물리적으로 바닥에 닿았는지 자체 레이캐스트로 확실히 체크
                if (PlayerMove._rb != null && PlayerMove._rb.linearVelocity.y <= 0.1f)
                {
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 0.8f, PlayerMove.groundLayer))
                    {
                        break;
                    }
                }
                yield return null;
            }

            // 바닥에 떨어지고 나서 쓰러진 모션을 잠깐(0.5초) 유지한 후 사망
            yield return new WaitForSeconds(0.5f);
        }

        CancelCurrentAction();
        ForceClearStun();

        // 💡 넉백 대기로 인해 남아있는 기상(WakeUp) 관련 찌꺼기 코루틴 완벽 차단
        if (PlayerMove != null) PlayerMove.CancelKnockBack();

        if (animator != null)
        {
            animator.ResetTrigger("Hitted1");
            animator.ResetTrigger("Hitted2");
            animator.ResetTrigger("Stagger");
            animator.ResetTrigger("Airborne");
            animator.ResetTrigger("KnockDown");
        }

        PreventMove = true;
        PreventAttack = true;
        if (PlayerAttack != null) PlayerAttack.DisableLockOn();

        Animator_SetTrigger("isDead");

        InkBrushEffectManager.Instance.PlayDeathEffect();
        SettingManager.Instance.SetSensitivityZero();
        SoundManager.Instance.PlaySound(DeathSound);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.MuffleBGMOnDeath();
        }
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
        MapDecalManager.Instance.ClearAllDecals();
        decalGenerator?.ClearDecals();
        hasTriggered66 = false;
        hasTriggered33 = false;
        isTakingDoT = false;

        HP = StatManager.Instance.MaxHp;
        MP = StatManager.Instance.MaxMp;
        PreventMove = false;
        PreventAttack = false;
        isGuarding = false;
        isParrying = false;
        isAirborne = false;
        isWakingUp = false;
        isInvincible = false;

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
        SoundManager.Instance.RestoreBGMOnRespawn();
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

    public void StartUseItemRoutine(InventorySlot Slot)
    {
        if (!CanInteract()) return;
        InventoryItem item = Slot.item;
        if ((item == null || EatRoutine != null) ||
            (item.consumable == null && item.nonConsumable == null))
        {
            return;
        }
        if (item.nonConsumable != null && item.nonConsumable.isStackable)
            if (item.nonConsumable.LeftAmount <= 0)
                return;
        if ((item.consumable != null && item.consumable.isEatable) || (item.nonConsumable != null && item.nonConsumable.isEatable))
            EatRoutine = StartCoroutine(UseItemRoutine(Slot));
    }

    IEnumerator UseItemRoutine(InventorySlot Slot)
    {
        if (UIManager.Instance.Inventory.activeSelf)
            UIManager.Instance.ToggleInventory();
        Animator_SetTrigger("Eat");
        yield return new WaitForSeconds(1.16f);

        InventoryItem item = Slot.item;
        if (item.consumable != null)
        {
            item.consumable.Use();
            item.Count--;
        }
        else if (item.nonConsumable != null)
            item.nonConsumable.Use();
        if (Slot.GetComponent<QuickSlot>() != null)
        {
            Slot.GetComponent<QuickSlot>().OriginalSlot.item.Count--;
            UIManager.Instance.SyncGameQuickSlots();
        }
        Slot.GetComponent<CanvasGroup>().blocksRaycasts = true;
        EatRoutine = null;
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

            float pullBackDist = 1.0f;
            float checkRadius = 0.8f;

            Vector3 origin = transform.position - (dir * pullBackDist);
            Vector3 point1 = origin + Vector3.up * 0.1f;
            Vector3 point2 = origin + Vector3.up * 2.0f;

            LayerMask obstacleLayer = wallLayer | LayerMask.GetMask("Ground");

            if (Physics.CapsuleCast(point1, point2, checkRadius, dir, out RaycastHit hit, distance + pullBackDist + WallCheckSkinWidth, obstacleLayer))
            {
                float hitAngle = Vector3.Angle(Vector3.up, hit.normal);
                if (hitAngle > 65)
                {
                    return;
                }
            }
        }

        Vector3 finalDelta = new Vector3(horizontalDelta.x, delta.y, horizontalDelta.z);
        Vector3 targetPosition = PlayerMove._rb.position + finalDelta;

        Vector3 rayOrigin = transform.position + Vector3.up * 2.0f + horizontalDelta;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 5.0f, LayerMask.GetMask("Ground")))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, groundHit.normal);
            if (slopeAngle <= 45f)
            {
                targetPosition.y = groundHit.point.y;
            }
            else
            {
                return;
            }
        }
        else
        {
            targetPosition.y = PlayerMove._rb.position.y;
        }

        PlayerMove._rb.MovePosition(targetPosition);
        PlayerMove._rb.MoveRotation(PlayerMove._rb.rotation * animator.deltaRotation);
    }

    IEnumerator LowHpMatRoutine()
    {
        while (true)
        {
            if (Time.timeScale == 0f)
            {
                yield return null;
                continue;
            }

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
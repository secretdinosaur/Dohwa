using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Analytics.IAnalytic;

public class PlayerAttack : MonoBehaviour
{
    #region Components & References
    [Header("Core References")]
    [HideInInspector] public Camera mainCamera;
    [HideInInspector] public LayerMask enemyLayer;
    [HideInInspector] public GameObject Effects;

    private Scabbard scabbard;
    #endregion

    #region State Flags & Properties
    [Header("Status Flags")]
    public bool isLockedOn = false;
    public bool isAttacking = false;
    public bool isArmed = false;
    public bool isSwordMoving = false;
    [HideInInspector] public bool canComboInput = false;

    [Header("Combat Targets & Skills")]
    public Transform currentTarget;
    public List<SkillBase> acquiredSkills = new List<SkillBase>();
    public SkillBase ViewSkill;
    public SkillBase strongAttackSkill;

    // 💡 강공격 타겟 저장을 위한 변수
    private EnemyCore currentStrongAttackTarget;

    SkillEffect CurrentEffect;
    [HideInInspector] public float lastAttackTime = 0f;
    [HideInInspector] public float lastAttackEndTime = 0f;

    private float lastGuardTime = 0f;
    private float lastLockOnTime = -10f;
    [HideInInspector] public float lastLockOffTime = -10f;
    private bool isGuardPressed = false;
    private float sheathTimer;
    private int currentAttackID = 0;

    public Coroutine DrawRoutine;
    private Coroutine SheathRoutine;
    private Coroutine GuardRoutine;
    private Coroutine AttackFaceRoutine;
    private Coroutine currentAttackRoutine;

    private List<SkillBase.Command> commandBuffer = new List<SkillBase.Command>();
    public List<SkillBase.Command> GetCommanBuffer() { return commandBuffer; }
    private HashSet<int> processedHitIndices = new HashSet<int>();
    private Dictionary<string, GameObject> _skillEffects = new Dictionary<string, GameObject>();
    private SkillBase lastActivatedSkill = null;
    #endregion

    #region Combat & Tuning Settings
    [Header("Lock-On Settings")]
    [SerializeField] private float detectionRadius = 25f;
    private float lockOnMaintainTime = 0.5f;
    private float lockOnCooldown = 0.5f;
    private float lockOnStepDelay = 0.6f;

    [Header("Auto Aim Settings")]
    private float autoAimRadius = 3f;
    private float autoAimAngle = 22.5f;

    [Header("Timing Settings")]
    private float guardCooldown = 0.3f;
    private float attackComboThreshold = 0.125f;
    private float autoSheathDelay = 20f;
    private float drawAnimDuration = 0.25f;
    private float sheathAnimDuration = 0.1f;
    private float sheathPostDelay = 2f;
    #endregion

    #region Constants
    private const int MaxCommandBuffer = 8;
    private const float LockOnBreakMultiplier = 1.5f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        isAttacking = false;
        scabbard = GetComponentInChildren<Scabbard>();
        InitializeEffects();
    }
    private void Update()
    {
        if (isLockedOn)
        {
            sheathTimer = autoSheathDelay;

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy || IsTargetDead(currentTarget) ||
                Vector3.Distance(transform.position, currentTarget.position) > detectionRadius * LockOnBreakMultiplier)
            {
                DisableLockOn();
            }
        }
        if (commandBuffer.Count > 0)
            UpdateCommandUI();
    }

    private void OnDrawGizmos()
    {
        if (ViewSkill is Skill_Melee slashSkill)
        {
            slashSkill.DrawSkillGizmos(transform);
        }
    }
    #endregion

    #region Command System
    public void OnCommandW(InputAction.CallbackContext context) { if (context.started && !Player.Instance.isCinematic) AddCommand(SkillBase.Command.W); }
    public void OnCommandA(InputAction.CallbackContext context) { if (context.started && !Player.Instance.isCinematic) AddCommand(SkillBase.Command.A); }
    public void OnCommandS(InputAction.CallbackContext context) { if (context.started && !Player.Instance.isCinematic) AddCommand(SkillBase.Command.S); }
    public void OnCommandD(InputAction.CallbackContext context) { if (context.started && !Player.Instance.isCinematic) AddCommand(SkillBase.Command.D); }
    public void OnCommandJump(InputAction.CallbackContext context) { if (context.started && !Player.Instance.isCinematic) AddCommand(SkillBase.Command.J); }

    public void UpdateCommandUI() { UIManager.Instance?.UpdateCommandUI(CommandsToString(commandBuffer), FindAvailableCommands().ToArray()); }

    private void AddCommand(SkillBase.Command cmd)
    {
        if (Player.Instance.isAirborne || Player.Instance.isCinematic || Player.Instance.IsStunned || Player.Instance.PlayerMove.isKnockedDown) return;
        commandBuffer.Add(cmd);
        if (commandBuffer.Count > MaxCommandBuffer) commandBuffer.RemoveAt(0);

        UpdateCommandUI();
    }

    private string CommandsToString(IEnumerable<SkillBase.Command> cmds, bool forText = true)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var c in cmds) sb.Append(forText ? CommandToChar(c) : CharToCommand(c));
        return sb.ToString();
    }

    private string CommandToChar(SkillBase.Command c)
    {
        switch (c)
        {
            case SkillBase.Command.W: return "<sprite=0>";
            case SkillBase.Command.A: return "<sprite=3>";
            case SkillBase.Command.S: return "<sprite=2>";
            case SkillBase.Command.D: return "<sprite=1>";
            case SkillBase.Command.J: return "<sprite=4>";
            default: return "?";
        }
    }

    private string CharToCommand(SkillBase.Command c)
    {
        switch (c)
        {
            case SkillBase.Command.W: return "W";
            case SkillBase.Command.A: return "A";
            case SkillBase.Command.S: return "S";
            case SkillBase.Command.D: return "D";
            case SkillBase.Command.J: return "J";
            default: return "?";
        }
    }

    private List<string> FindAvailableCommands()
    {
        List<string> result = new List<string>();
        if (commandBuffer.Count == 0) return result;

        string bestMatch = null;
        int bestLength = 0;

        foreach (var baseSkill in acquiredSkills)
        {
            if (baseSkill == null) continue;
            SkillBase targetSkill = baseSkill;

            while (targetSkill != null && targetSkill.UpgradedSkill != null && targetSkill.isUpgraded)
                targetSkill = targetSkill.UpgradedSkill;

            if (targetSkill.commands == null || targetSkill.commands.Length == 0 || targetSkill.isSpecialSkill) continue;

            int cmdLength = targetSkill.commands.Length;
            if (commandBuffer.Count < cmdLength) continue;

            bool match = true;
            int startIndex = commandBuffer.Count - cmdLength;

            for (int i = 0; i < cmdLength; i++)
            {
                if (commandBuffer[startIndex + i] != targetSkill.commands[i])
                {
                    match = false;
                    break;
                }
            }

            if (match && cmdLength > bestLength)
            {
                bestLength = cmdLength;

                // 💡 마나가 부족한 경우 UI에 흐리게 표시하기 위한 처리를 위해 
                // 텍스트에 HTML 태그(Alpha)를 씌우거나 별도 상태를 관리할 수 있습니다.
                bool hasEnoughMp = Player.Instance != null && Player.Instance.MP >= targetSkill.MP_cost;

                if (hasEnoughMp)
                {
                    bestMatch = targetSkill.skillName;
                }
                else
                {
                    // 마나가 부족하면 투명도가 들어간 HTML 텍스트 태그 적용 (예: 50% 알파값)
                    bestMatch = $"<color=#808080B3>{targetSkill.skillName}</color>";
                }
            }
        }

        if (bestMatch != null) result.Add(bestMatch);

        return result;
    }

    public void ClearCommandBuffer()
    {
        commandBuffer.Clear();
    }
    #endregion

    #region Attack & Skill Logic
    private void InitializeEffects()
    {
        Transform[] allChildren = Effects.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.gameObject == Effects) continue;
            if (!_skillEffects.ContainsKey(child.name)) _skillEffects.Add(child.name, child.gameObject);
        }
    }

    public void SetActiveEffects(string skillPrefix, bool isActive)
    {
        foreach (var key in _skillEffects.Keys.Where(k => k.StartsWith(skillPrefix)))
        {
            _skillEffects[key].SetActive(isActive);
        }
    }

    public void PlayEffects(string skillPrefix) => SetActiveEffects(skillPrefix, true);
    public void StopEffects(string skillPrefix) => SetActiveEffects(skillPrefix, false);

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.started || DialogueManager.Instance.IsDialogueActive) return;
        if (Player.Instance == null || !Player.Instance.CanAttack()) return;

        if (isSwordMoving || DrawRoutine != null) return;
        if (isAttacking && !canComboInput) return;
        if (!isAttacking && Time.time - lastAttackEndTime < attackComboThreshold) return;

        EnemyCore targetEnemy = GetClosestStunnedEnemy();
        if (targetEnemy != null && strongAttackSkill != null)
        {
            // 💡 이미 강공격이 실행 중이거나 공격 중인 상태라면 연타 입력 무시
            if (isAttacking || currentAttackRoutine != null) return;

            ExecuteStrongAttack(targetEnemy);
            return;
        }

        StartCoroutine(AttackRoutine());
    }

    private EnemyCore GetClosestStunnedEnemy(float searchRadius = 5f)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayer);
        EnemyCore closestEnemy = null;
        float minDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            EnemyCore enemy = hit.GetComponent<EnemyCore>();
            if (enemy != null && enemy.currentState == EnemyState.Stun && !enemy.IsDead)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestEnemy = enemy;
                }
            }
        }
        return closestEnemy;
    }
    private IEnumerator DrawSwordImmediateAndAttack(SkillBase skillToUse)
    {
        yield return StartCoroutine(DrawSword());
        Attack(skillToUse);
    }

    private IEnumerator AttackRoutine()
    {
        SkillBase matched = null;
        int matchedLen = 0;

        for (int i = 0; i < acquiredSkills.Count; i++)
        {
            var skill = acquiredSkills[i];
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

        SkillBase skillToUse = matched ?? null;
        while (skillToUse != null && skillToUse.UpgradedSkill != null && skillToUse.isUpgraded) skillToUse = skillToUse.UpgradedSkill;

        if (!isArmed)
        {
            if (!isSwordMoving && !isAttacking && DrawRoutine == null)
            {
                DrawRoutine = StartCoroutine(DrawSword());
                yield return DrawRoutine;
            }
            yield break;
        }

        if (skillToUse == null || !Player.Instance.CanAttack()) yield break;

        if (skillToUse.MP_cost > Player.Instance.MP)
        {
            UIManager.Instance?.UpdateCommandUI(CommandsToString(commandBuffer), FindAvailableCommands().ToArray());
            yield break;
        }

        if (!isLockedOn) RotateToNearestEnemy();

        Player.Instance.mpRecoveryDelayStart();
        Attack(skillToUse);
    }

    private SoundObject currentSkillSound;
    public void Attack(SkillBase skillToUse)
    {
        if (currentAttackRoutine != null) StopCoroutine(currentAttackRoutine);

        if (lastActivatedSkill == strongAttackSkill || currentStrongAttackTarget != null)
        {
            Player.Instance.isInvincible = false;
            currentStrongAttackTarget = null;
        }

        if (lastActivatedSkill != null) StopEffects(lastActivatedSkill.name);

        Player.Instance.Animator_SetFloat("MoveSpeed", 0f);
        ClearCommandBuffer();
        UIManager.Instance?.UpdateCommandUI(CommandsToString(commandBuffer), FindAvailableCommands().ToArray());

        isAttacking = true;
        canComboInput = false;
        lastAttackTime = Time.time;
        lastActivatedSkill = skillToUse;

        sheathTimer = autoSheathDelay;
        if (SheathRoutine == null) SheathRoutine = StartCoroutine(SheathSwordRoutine());

        if (isLockedOn && currentTarget != null)
        {
            Vector3 lookDir = currentTarget.position - Player.Instance.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                if (Player.Instance.PlayerMove != null) Player.Instance.PlayerMove.SetLookRotationImmediate(lookDir);
                else Player.Instance.transform.rotation = Quaternion.LookRotation(lookDir);
            }
            if (AttackFaceRoutine != null) StopCoroutine(AttackFaceRoutine);
            AttackFaceRoutine = StartCoroutine(FaceTargetDuringAttack());
        }

        currentAttackID++;
        processedHitIndices.Clear();
        CameraController.Instance.OnSkillStarted();
        CurrentEffect = skillToUse.Activate(Player.Instance.transform);
        currentSkillSound = SoundManager.Instance.PlaySound(skillToUse.sound);

        PlayEffects(skillToUse.name);
        Player.Instance.Animator_PlaySkill(skillToUse, 0.1f);

        Player.Instance.ConsumeMP(skillToUse.MP_cost);
        GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.UseSKill);

        currentAttackRoutine = StartCoroutine(AttackLifecycleRoutine(skillToUse));
    }

    private IEnumerator AttackLifecycleRoutine(SkillBase skillToUse)
    {
        Player.Instance.Animator_SetFloat("motiontimeforskill", 0f);

        float clipLength = skillToUse.clip.length;
        float cancelTime = clipLength;

        if (skillToUse.clip != null)
        {
            float lastHitEventTime = -1f;
            foreach (AnimationEvent animEvent in skillToUse.clip.events)
            {
                if (animEvent.functionName == "OnAttackHitEvent")
                {
                    if (animEvent.time > lastHitEventTime)
                    {
                        lastHitEventTime = animEvent.time + 0.2f;
                    }
                }
            }

            if (lastHitEventTime >= 0f)
            {
                cancelTime = lastHitEventTime;
            }
        }

        float elapsedTime = 0f;

        while (elapsedTime < clipLength)
        {
            elapsedTime += Time.deltaTime;
            Player.Instance.Animator_SetFloat("motiontimeforskill", Mathf.Clamp01(elapsedTime / clipLength));

            if (elapsedTime >= cancelTime && !canComboInput)
            {
                canComboInput = true;
            }

            yield return null;
        }

        EndAttack(skillToUse);
    }

    private void EndAttack(SkillBase skillToUse)
    {
        isAttacking = false;
        canComboInput = false;

        // 💡 강공격 종료 시 무적 상태 해제
        if (skillToUse == strongAttackSkill)
        {
            Player.Instance.isInvincible = false;
            currentStrongAttackTarget = null;
        }

        lastActivatedSkill = null;
        StopEffects(skillToUse.name);
        CameraController.Instance.OnSkillEnd();

        Player.Instance.Animator_SetFloat("MoveSpeed", 0f);

        if (AttackFaceRoutine != null)
        {
            StopCoroutine(AttackFaceRoutine);
            AttackFaceRoutine = null;
        }
        lastAttackEndTime = Time.time;
        currentAttackRoutine = null;
    }

    private IEnumerator FaceTargetDuringAttack()
    {
        while (isAttacking)
        {
            if (currentTarget != null && Player.Instance != null)
            {
                Vector3 lookDir = currentTarget.position - Player.Instance.transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    if (Player.Instance.PlayerMove != null) Player.Instance.PlayerMove.SetLookRotationImmediate(lookDir);
                    else Player.Instance.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
            yield return null;
        }
    }

    public void CancelCurrentSkill()
    {
        if (currentSkillSound != null)
        {
            SoundManager.Instance.StopSound(currentSkillSound);
            currentSkillSound = null;
        }

        if (currentAttackRoutine != null)
        {
            StopCoroutine(currentAttackRoutine);
            currentAttackRoutine = null;
        }

        if (lastActivatedSkill == strongAttackSkill || currentStrongAttackTarget != null)
        {
            Player.Instance.isInvincible = false;
            currentStrongAttackTarget = null;
        }

        if (lastActivatedSkill != null)
        {
            StopEffects(lastActivatedSkill.name);
            SkillEffectManager.Instance.ReturnSkillEffect(CurrentEffect, lastActivatedSkill.name);
            lastActivatedSkill = null;
        }

        isAttacking = false;
        canComboInput = false;
    }

    public void CancelDrawSword()
    {
        if (DrawRoutine != null)
        {
            StopCoroutine(DrawRoutine);
            DrawRoutine = null;
        }
        isSwordMoving = false;

        // 💡 발도 중 취소되었을 때를 대비한 무적 안전 해제
        if (currentStrongAttackTarget != null)
        {
            Player.Instance.isInvincible = false;
            currentStrongAttackTarget = null;
        }
    }

    public void OnAttackHitEvent(int HitIndex)
    {
        if (lastActivatedSkill == null || Time.time - lastAttackTime < 0.1f || processedHitIndices.Contains(HitIndex)) return;

        processedHitIndices.Add(HitIndex);
        float baseDamage = StatManager.Instance.AttackPower / 100f;

        if (lastActivatedSkill == strongAttackSkill)
        {
            if (currentStrongAttackTarget == null || currentStrongAttackTarget.gameObject == null)
            {
                currentStrongAttackTarget = GetClosestStunnedEnemy();
            }

            if (currentStrongAttackTarget != null && !currentStrongAttackTarget.IsDead)
            {
                HitEffectData hitEffectData = null;
                if (lastActivatedSkill is Skill_Melee meleeSkill && meleeSkill.hitDataList != null && meleeSkill.hitDataList.Count > HitIndex)
                {
                    hitEffectData = meleeSkill.hitDataList[HitIndex].playerHitEffect;
                }
                else if (lastActivatedSkill is Skill_Range rangeSkill && rangeSkill.hitDataList != null && rangeSkill.hitDataList.Count > HitIndex)
                {
                    hitEffectData = rangeSkill.hitDataList[HitIndex].playerHitEffect;
                }

                bool isGuarding = currentStrongAttackTarget.isGuarding;
                Vector3 hitPoint = currentStrongAttackTarget.transform.position;
                Quaternion effectRot = Quaternion.identity;

                bool isCriticalChecked = false;
                bool isCriticalHit = false;
                bool isCasterPlayer = true;

                float finalDamage = lastActivatedSkill.ProcessCriticalHit(
                    ref isCriticalChecked, ref isCriticalHit, isCasterPlayer,
                    currentStrongAttackTarget.gameObject, hitPoint, effectRot,
                    baseDamage, lastActivatedSkill.damageMultipliers[HitIndex], isGuarding, HitIndex);

                currentStrongAttackTarget.TakeDamage(finalDamage, lastActivatedSkill, Player.Instance.transform, hitEffectData, HitIndex);

                if (lastActivatedSkill.HitSound != null && lastActivatedSkill.HitSound.Length > HitIndex && lastActivatedSkill.HitSound[HitIndex] != null)
                {
                    SoundManager.Instance.PlaySound(lastActivatedSkill.HitSound[HitIndex]);
                }
            }
            return;
        }

        lastActivatedSkill.ExecuteHitCheck(Player.Instance.transform, baseDamage, HitIndex);
    }

    [SerializeField] private Vector3 strongAttackOffset = new Vector3(0f, 0f, 1.5f);

    private void ExecuteStrongAttack(EnemyCore targetEnemy)
    {
        ClearCommandBuffer();
        UIManager.Instance?.UpdateCommandUI(CommandsToString(commandBuffer), FindAvailableCommands().ToArray());
        CancelCurrentSkill();

        currentStrongAttackTarget = targetEnemy;
        Player.Instance.isInvincible = true;

        // 💡 1. 타겟 기준 설정된 Offset 위치 계산 (타겟의 로컬 좌표계를 기준으로 변환)
        Vector3 targetIdealPos = targetEnemy.transform.TransformPoint(strongAttackOffset);

        // 💡 2. 이동할 위치가 벽이나 뚫릴 수 있는 지형인지 검사 후 안전한 위치로 보정
        Vector3 safePosition = GetSafeStrongAttackPosition(targetEnemy.transform.position, targetIdealPos);

        // 💡 3. 계산된 안전한 위치로 플레이어 이동 (Rigidbody를 통한 즉시 이동 또는 스냅)
        if (Player.Instance.PlayerMove != null && Player.Instance.PlayerMove._rb != null)
        {
            Player.Instance.PlayerMove._rb.MovePosition(safePosition);
        }
        else
        {
            transform.position = safePosition;
        }

        // 💡 4. 타겟을 바라보도록 회전
        Vector3 direction = (targetEnemy.transform.position - transform.position).normalized;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            if (Player.Instance.PlayerMove != null) Player.Instance.PlayerMove.SetLookRotationImmediate(direction);
            else transform.rotation = Quaternion.LookRotation(direction);
        }

        if (!isArmed)
        {
            if (DrawRoutine == null) DrawRoutine = StartCoroutine(DrawSwordImmediateAndAttack(strongAttackSkill));
        }
        else
        {
            Attack(strongAttackSkill);
        }
    }

    private Vector3 GetSafeStrongAttackPosition(Vector3 enemyPos, Vector3 idealPos)
    {
        Vector3 startPos = transform.position;
        Vector3 dirToIdeal = idealPos - startPos;
        float totalDist = dirToIdeal.magnitude;

        LayerMask obstacleLayer = LayerMask.GetMask("Wall") | LayerMask.GetMask("Ground");

        // 플레이어 현재 위치에서 이상적인 위치까지 장애물(벽/급경사)이 있는지 캡슐캐스트로 검사
        if (Physics.CapsuleCast(startPos + Vector3.up * 0.5f, startPos + Vector3.up * 1.5f, 0.4f, dirToIdeal.normalized, out RaycastHit hit, totalDist, obstacleLayer))
        {
            float hitAngle = Vector3.Angle(Vector3.up, hit.normal);
            // 벽이나 45도 이상의 급경사에 부딪힌다면, 충돌 지점 바로 앞까지만 이동하도록 보정
            if (hitAngle > 45f)
            {
                Vector3 safeMove = startPos + dirToIdeal.normalized * Mathf.Max(0f, hit.distance - 0.5f);
                return GetValidNavMeshPosition(safeMove, enemyPos);
            }
        }

        // 장애물이 없더라도 이상적인 위치가 NavMesh 위이거나 유효한 바닥인지 검사
        return GetValidNavMeshPosition(idealPos, enemyPos);
    }

    private Vector3 GetValidNavMeshPosition(Vector3 targetPos, Vector3 enemyPos)
    {
        // NavMesh를 이용해 해당 위치 주변에서 가장 갈 수 있는 안전한 지형을 찾음
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return navHit.position;
        }

        // NavMesh를 찾지 못했다면 적어도 적 바로 앞의 바닥 높이를 Raycast로 쏴서 Y축을 맞춤
        Vector3 rayOrigin = targetPos + Vector3.up * 2.0f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 5.0f, LayerMask.GetMask("Ground")))
        {
            return groundHit.point;
        }

        // 모든 방법이 실패하면 안전하게 현재 플레이어 위치 반환 (제자리 공격)
        return transform.position;
    }
    #endregion

    #region Guard & Weapon Mechanics
    public void OnGuard(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (Player.Instance == null || !Player.Instance.CanGuard() || Time.time - lastGuardTime < guardCooldown) return;

            isGuardPressed = true;
            if (GuardRoutine == null)
            {
                lastGuardTime = Time.time;
                GuardRoutine = StartCoroutine(Guard());
            }
        }
        else if (context.canceled)
        {
            isGuardPressed = false;
        }
    }

    private IEnumerator Guard()
    {
        float elapsed = 0f;
        Player.Instance.Animator_SetBool("isGuard", true);
        SoundManager.Instance.PlayMovementSound(8);

        float guardInitialDelay = 0.05f;
        while (elapsed < guardInitialDelay)
        {
            if (!isGuardPressed)
            {
                Player.Instance.Animator_SetBool("isGuard", false);
                GuardRoutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Player.Instance.isGuarding = true;
        elapsed = 0f;

        if (Player.Instance.acquiredParring)
        {
            Player.Instance.Animator_SetBool("parring", true);
            Player.Instance.isParrying = true;
        }

        while (isGuardPressed && Player.Instance.MP > 0)
        {
            if (elapsed >= StatManager.Instance.ParringTime)
            {
                Player.Instance.Animator_SetBool("parring", false);
                Player.Instance.isParrying = false;
            }
            elapsed += Time.deltaTime;
            sheathTimer = autoSheathDelay;
            yield return null;
        }

        Player.Instance.Animator_SetBool("isGuard", false);
        Player.Instance.isGuarding = false;
        Player.Instance.isParrying = false;
        GuardRoutine = null;
    }

    private IEnumerator DrawSword()
    {
        if (isSwordMoving || isArmed) yield break;

        isSwordMoving = true;

        if (Player.Instance.PlayerMove != null && !Player.Instance.PlayerMove.isGrounded)
        {
            yield return new WaitUntil(() => Player.Instance.PlayerMove.isGrounded);
        }

        if (Player.Instance.animator != null)
        {
            Player.Instance.animator.ResetTrigger("DrawSword");
            Player.Instance.animator.ResetTrigger("SheathSword");
        }

        Player.Instance.Animator_SetTrigger("DrawSword");
        yield return new WaitForSeconds(0.2f);
        SoundManager.Instance.PlayMovementSound(3);

        float elapsedTime = 0f;
        yield return new WaitForSeconds(1f);

        while (elapsedTime < drawAnimDuration)
        {
            elapsedTime += Time.deltaTime;
            Player.Instance.Animator_SetFloat("isArmed", Mathf.Lerp(0f, 1f, elapsedTime / drawAnimDuration));
            yield return null;
        }

        isArmed = true;
        isSwordMoving = false;
        Player.Instance.Animator_SetFloat("isArmed", 1f);

        sheathTimer = autoSheathDelay;
        if (SheathRoutine == null) SheathRoutine = StartCoroutine(SheathSwordRoutine());

        DrawRoutine = null;
    }

    private IEnumerator SheathSwordRoutine()
    {
        while (sheathTimer > 0)
        {
            if (!isLockedOn)
            {
                if (Player.Instance.PlayerMove != null && Player.Instance.PlayerMove.isGrounded)
                {
                    sheathTimer -= 1f;
                }
            }
            else
            {
                sheathTimer = autoSheathDelay;
            }
            yield return new WaitForSeconds(1f);
        }

        if (Player.Instance.PlayerMove != null && !Player.Instance.PlayerMove.isGrounded)
        {
            yield return new WaitUntil(() => Player.Instance.PlayerMove.isGrounded);
        }

        if (isArmed && !isSwordMoving && !isLockedOn && Player.Instance.HP >= 0)
        {
            StartCoroutine(SheathSword());
        }
        SheathRoutine = null;
    }

    private IEnumerator SheathSword()
    {
        if (isSwordMoving || !isArmed) yield break;

        isSwordMoving = true;

        if (Player.Instance.PlayerMove != null && !Player.Instance.PlayerMove.isGrounded)
        {
            yield return new WaitUntil(() => Player.Instance.PlayerMove.isGrounded);
        }

        float elapsedTime = 0f;
        bool stepTriggered = false;
        SoundManager.Instance.PlayMovementSound(4);

        while (elapsedTime < sheathAnimDuration)
        {
            elapsedTime += Time.deltaTime;
            Player.Instance.Animator_SetFloat("isArmed", Mathf.Lerp(1f, 0f, elapsedTime / sheathAnimDuration));

            if (elapsedTime >= sheathAnimDuration * 0.5f && !stepTriggered)
            {
                Player.Instance.Animator_SetBool("isStep", false);
                Player.Instance.Animator_SetTrigger("SheathSword");
                stepTriggered = true;
            }
            yield return null;
        }

        isArmed = false;
        Player.Instance.Animator_SetFloat("isArmed", 0f);
        Player.Instance.Animator_SetBool("isStep", false);

        yield return new WaitForSeconds(sheathPostDelay);
        isSwordMoving = false;
    }
    public void ForceUnarmed()
    {
        CancelDrawSword();
        if (SheathRoutine != null)
        {
            StopCoroutine(SheathRoutine);
            SheathRoutine = null;
        }

        isArmed = false;
        isSwordMoving = false;
        sheathTimer = 0f;

        if (Player.Instance != null && Player.Instance.animator != null)
        {
            Player.Instance.Animator_SetFloat("isArmed", 0f);
            Player.Instance.Animator_SetBool("isStep", false);
        }

        Sheath();
    }
    private void Draw() => scabbard?.Draw();
    private void Sheath() => scabbard?.Sheath();
    #endregion

    #region Lock-On System
    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (!context.started || Player.Instance == null || !Player.Instance.CanLockOn()) return;

        if (isLockedOn)
        {
            if (Time.time - lastLockOnTime < lockOnMaintainTime) return;
            DisableLockOn();
        }
        else
        {
            if (Time.time - lastLockOffTime < lockOnCooldown) return;
            EnableLockOn();
        }
    }

    private void EnableLockOn()
    {
        Transform target = FindBestTarget();
        if (target != null)
        {
            lastLockOnTime = Time.time;

            if (!isArmed && !isSwordMoving && !isAttacking && DrawRoutine == null)
            {
                DrawRoutine = StartCoroutine(DrawSword());
            }

            currentTarget = target;
            isLockedOn = true;

            if (EnemyLockOnImage.Instance != null)
                EnemyLockOnImage.Instance.SetTarget(currentTarget);

            Camera.main.GetComponent<CameraController>().SetLockOn(currentTarget);
            StartCoroutine(LockOnTransition());
        }
    }

    private IEnumerator LockOnTransition()
    {
        if (Player.Instance.PlayerMove != null && !Player.Instance.PlayerMove.isGrounded)
        {
            yield return new WaitUntil(() => Player.Instance.PlayerMove.isGrounded);
        }

        yield return new WaitForSeconds(lockOnStepDelay);
        Player.Instance.Animator_SetBool("isStep", true);
    }

    public void DisableLockOn()
    {
        if (!isLockedOn) return;

        isLockedOn = false;
        currentTarget = null;
        lastLockOffTime = Time.time;

        if (EnemyLockOnImage.Instance != null)
            EnemyLockOnImage.Instance.SetTarget(null);

        Camera.main.GetComponent<CameraController>().DisableLockOn();
        Player.Instance.Animator_SetBool("isStep", false);

        if (isArmed && SheathRoutine == null)
        {
            sheathTimer = autoSheathDelay;
            SheathRoutine = StartCoroutine(SheathSwordRoutine());
        }
    }

    private Transform FindBestTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        Transform bestTarget = null;
        float bestScore = Mathf.Infinity;

        foreach (Collider col in enemies)
        {
            if (IsTargetDead(col.transform)) continue;
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(col.transform.position);

            if (viewportPos.z < 0 || viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1) continue;

            float distFromCenter = Vector2.Distance(new Vector2(viewportPos.x, viewportPos.y), new Vector2(0.5f, 0.5f));
            if (distFromCenter < bestScore)
            {
                bestScore = distFromCenter;
                bestTarget = col.transform;
            }
        }
        return bestTarget;
    }

    public void RotateToNearestEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, autoAimRadius, enemyLayer);
        Transform bestTarget = null;
        float closestDist = Mathf.Infinity;

        foreach (Collider enemy in enemies)
        {
            if (IsTargetDead(enemy.transform)) continue;

            Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
            directionToEnemy.y = 0;

            if (Vector3.Angle(transform.forward, directionToEnemy) < autoAimAngle)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = enemy.transform;
                }
            }
        }

        if (bestTarget != null)
        {
            Vector3 lookDir = bestTarget.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    private bool IsTargetDead(Transform target)
    {
        var enemy = target.GetComponent<EnemyCore>();
        if (enemy)
            return enemy.IsDead;
        else
            return false;
    }
    #endregion
}
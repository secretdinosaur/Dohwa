using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyState { Idle, Chase, Attack, Stun, Dead, BackWalk, CooldownWalk }

public class EnemyCore : MonoBehaviour, IDamageable, IBleedable
{
    public EnemyState currentState = EnemyState.Idle;
    [HideInInspector] public Transform cameraTarget;
    public Item dropItem;

    public GameObject[] EnemyWeapons;
    public GameObject[] FakeWeapons;

    [Header("One-Time Enemy Settings")]
    public bool isOnetimeEnemy = false;
    public string onetimeSaveKey = "";

    [Header("Core Settings")]
    public SkillBase ViewSkill;
    [HideInInspector] public Animator animator;
    [HideInInspector] public bool wasParried = false;
    [HideInInspector] public float BaseMovementSpeed = 1;

    [HideInInspector] public bool isGuarding = false;
    public event Action OnGuardSuccess;

    public Action<Vector3, float> CmdMoveTo;
    public Action CmdStopMovement;
    public Action<Vector3> CmdLookAt;
    public Action<Vector3> CmdFastLookAt;
    public Action<Vector3, float> CmdMoveDirection;
    public Action CmdWarpToOriginal;
    public Action CmdTrySnapToNavMesh;
    public Action CmdForceDetectPlayer;

    public Func<bool> QueryIsPathPending;
    public Func<Vector3> QueryOriginalPosition;

    public Action<SkillBase> CmdPerformAttack;
    public Action<SkillBase> CmdPerformInstantAttack;
    public Action CmdCancelAttack;
    public Action<float> OnAttackCompleted;
    public Action<SkillBase, int> PlayHitSound;

    public Action<SkillBase, int> OnPlayerHit;

    public Action<float> CmdConsumeMP;
    public Func<float, bool> QueryHasEnoughMP;
    public Action<bool> CmdSetIgnorePlayerCollision;
    public Func<float> QueryCurrentHP;

    public event Action<float> OnDamageTaken;
    public event Action<float> OnParryDamageTaken;
    public event Action OnDeath;
    public event Action OnParried;
    public event Action OnStunned;
    public event Action OnStunEnd;
    public event Action OnRevived;
    public event Action OnPlayerDetected;
    public event Action<float, float> OnHpChanged;
    public event Action<float, float> OnMpChanged;
    public event Action<int> OnPhaseTransitionStart;

    public bool IsDead => currentState == EnemyState.Dead;
    public Transform Transform => transform;

    [HideInInspector] public bool isDetectingPlayer = false;
    [HideInInspector] public bool isForceDetected = false;
    [HideInInspector] public bool isIgnoringPlayer = false;
    [HideInInspector] public bool isTakingDoT = false;
    [HideInInspector] public bool isCulled;

    [HideInInspector] public SkillSelector skillSelector;

    private float destroyDelay = 60f;
    private Material unifiedMaterial;
    private float originalStep1, originalStep2, originalStep3;
    private Dictionary<string, GameObject> _skillEffects = new Dictionary<string, GameObject>();
    [HideInInspector] public SkillBase HittedSkill;
    [HideInInspector] public int HitIndex;

    private DecalGenerator decalGenerator;

    private struct WeaponOriginData
    {
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }
    private Dictionary<GameObject, WeaponOriginData> fakeWeaponOriginMap = new Dictionary<GameObject, WeaponOriginData>();

    private float targetMoveX;
    private float targetMoveZ;
    private float targetRun;

    private Coroutine bleedCoroutine;

    protected virtual void Awake()
    {
        

        animator = GetComponent<Animator>();
        decalGenerator = GetComponent<DecalGenerator>();
        skillSelector = GetComponent<SkillSelector>();

        CreateCameraTarget();
        InitializeEffects();
        CacheAndUnifyMaterials();

        GetComponent<Rigidbody>().linearDamping = 10f;
        GetComponent<Rigidbody>().isKinematic = true;

        CmdForceDetectPlayer += HandleForceDetect;

        if (EnemyWeapons != null)
        {
            foreach (var weapon in EnemyWeapons)
            {
                if (weapon != null)
                {
                    Collider[] cols = weapon.GetComponentsInChildren<Collider>();
                    foreach (var col in cols) col.enabled = false;
                }
            }
        }

        if (FakeWeapons != null)
        {
            foreach (var fakeWeapon in FakeWeapons)
            {
                if (fakeWeapon != null)
                {
                    fakeWeaponOriginMap[fakeWeapon] = new WeaponOriginData
                    {
                        Parent = fakeWeapon.transform.parent,
                        LocalPosition = fakeWeapon.transform.localPosition,
                        LocalRotation = fakeWeapon.transform.localRotation,
                        LocalScale = fakeWeapon.transform.localScale
                    };
                    fakeWeapon.SetActive(false);
                }
            }
        }

        CmdSetIgnorePlayerCollision += HandleSetIgnorePlayerCollision;
    }

    private void OnDestroy()
    {
        CmdForceDetectPlayer -= HandleForceDetect;
        CmdSetIgnorePlayerCollision -= HandleSetIgnorePlayerCollision;
        SetPlayerDetection(false);
    }

    private void Start()
    {
        // 일회성 몬스터이며 이미 클리어된 상태라면 스스로 파괴
        if (isOnetimeEnemy && !string.IsNullOrEmpty(onetimeSaveKey) && DataManager.Instance != null)
        {
            if (DataManager.Instance.IsEventCleared(onetimeSaveKey))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
        }
    }
    private void Update()
    {
        if (animator != null && animator.gameObject.activeInHierarchy)
        {
            animator.SetFloat("MoveX", targetMoveX, 0.25f, Time.deltaTime);
            animator.SetFloat("MoveZ", targetMoveZ, 0.25f, Time.deltaTime);
            animator.SetFloat("Speed", targetRun, 0.25f, Time.deltaTime);
        }
    }

    public void ApplyBleed(int ticksPerSecond, SkillBase bleedSkill, HitEffectData bleedEffect, SkillBase.EnemyHitEffectDirection effectDir)
    {
        if (IsDead) return;

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
            if (IsDead)
            {
                bleedCoroutine = null;
                yield break;
            }

            float currentHp = QueryCurrentHP != null ? QueryCurrentHP.Invoke() : 0f;
            float tickDamage = (currentHp * 0.1f) / totalTicks;

            isTakingDoT = true;
            TakeDamage(tickDamage, bleedSkill, null, bleedEffect, 0);
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

    private void HandleSetIgnorePlayerCollision(bool ignore)
    {
        isIgnoringPlayer = ignore;
    }

    private void HandleForceDetect()
    {
        if (IsDead) return;

        isForceDetected = true;
        SetPlayerDetection(true);
        TriggerPlayerDetected();

        if (currentState == EnemyState.Idle)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    public void SetPlayerDetection(bool isDetecting)
    {
        if (isDetectingPlayer == isDetecting) return;

        isDetectingPlayer = isDetecting;

        if (Player.Instance != null)
        {
            if (isDetecting) Player.Instance.AddDetection();
            else Player.Instance.RemoveDetection();
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (currentState == EnemyState.Dead && newState != EnemyState.Idle) return;

        if (currentState == newState) return;
        currentState = newState;

        if (currentState == EnemyState.Idle || currentState == EnemyState.Dead)
        {
            SetPlayerDetection(false);
        }
        else if (currentState != EnemyState.Stun)
        {
            SetPlayerDetection(true);
        }
    }

    public void TakeDamage(float damageValue, SkillBase skill, Transform caster, HitEffectData hitEffect = null, int hitIndex = 0)
    {
        if (IsDead) return;

        if (UnityEngine.Random.value < 0.3f && decalGenerator != null)
        {
            if (decalGenerator.GetActiveDecalCount() < (decalGenerator.maxDecals * 0.5f))
            {
                decalGenerator.GenerateWound();
            }
        }

        HittedSkill = skill;
        HitIndex = hitIndex;
        OnDamageTaken?.Invoke(damageValue);
    }

    public void TakeParry(float mpDamage)
    {
        if (IsDead) return;
        OnParryDamageTaken?.Invoke(mpDamage);
    }

    public void TriggerDeath()
    {
        // 일회성 몬스터 처치 시 저장 데이터 갱신
        if (isOnetimeEnemy && !string.IsNullOrEmpty(onetimeSaveKey) && DataManager.Instance != null)
        {
            DataManager.Instance.SetEventCleared(onetimeSaveKey);
        }

        isForceDetected = false;
        SetPlayerDetection(false);

        ChangeState(EnemyState.Dead);
        SetMoveAnimations(0f, 0f, 0f);
        animator.ResetTrigger("Hitted1");
        animator.ResetTrigger("Hitted2");
        animator.ResetTrigger("Parried");

        animator.Play(UnityEngine.Random.Range(0, 2) == 0 ? "Dead_1" : "Dead_2");
        StopAllEffects();
        GetComponent<Collider>().enabled = false;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        if (EnemyWeapons != null)
        {
            foreach (var weapon in EnemyWeapons)
            {
                if (weapon != null) weapon.SetActive(false);
            }
        }

        if (FakeWeapons != null)
        {
            foreach (var fakeWeapon in FakeWeapons)
            {
                if (fakeWeapon != null)
                {
                    fakeWeapon.SetActive(true);
                    fakeWeapon.transform.SetParent(null);

                    Rigidbody rb = fakeWeapon.GetComponent<Rigidbody>();
                    if (rb == null) rb = fakeWeapon.AddComponent<Rigidbody>();
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                    Vector3 randomForce = UnityEngine.Random.onUnitSphere * 5f;
                    randomForce.y = Mathf.Abs(randomForce.y) + 3f;
                    rb.AddForce(randomForce, ForceMode.Impulse);
                    rb.AddTorque(UnityEngine.Random.onUnitSphere * 10f, ForceMode.Impulse);
                }
            }
        }

        StartCoroutine(AlignToGroundRotation());
        StartCoroutine(FadeShaderPropertiesToZero(10f));

        if (dropItem != null)
        {
            dropItem.Interact(false);
            DOVirtual.DelayedCall(3f, () => { if (dropItem != null) dropItem.ShowItemGainPanel(); }).SetTarget(gameObject);
        }
        OnDeath?.Invoke();
    }

    private IEnumerator AlignToGroundRotation()
    {
        Vector3 rayStart = transform.position + (Vector3.up * 1f);
        int groundLayer = LayerMask.GetMask("Ground");

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3f, groundLayer))
        {
            Quaternion startRot = transform.rotation;
            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

            if (forwardOnPlane == Vector3.zero)
                forwardOnPlane = transform.forward;

            Quaternion targetRot = Quaternion.LookRotation(forwardOnPlane, hit.normal);
            Vector3 startPos = transform.position;
            Vector3 targetPos = new Vector3(transform.position.x, hit.point.y, transform.position.z);

            float elapsed = 0f;
            float duration = 1.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                transform.rotation = Quaternion.Lerp(startRot, targetRot, smoothT);
                transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                yield return null;
            }

            transform.rotation = targetRot;
            transform.position = targetPos;
        }
    }

    public void TriggerGuardSuccess()
    {
        OnGuardSuccess?.Invoke();
    }

    public void TriggerParried()
    {
        wasParried = true;
        animator.SetTrigger("Parried");
        OnParried?.Invoke();
        StopAllEffects();
    }

    public void TriggerStun()
    {
        ChangeState(EnemyState.Stun);
        OnStunned?.Invoke();
        StopAllEffects();

        if (EnemyLockOnImage.Instance != null &&
            EnemyLockOnImage.Instance.target != null &&
            EnemyLockOnImage.Instance.target.IsChildOf(this.transform))
        {
            EnemyLockOnImage.Instance.TriggerStrongAttackEffect();
        }
    }

    public void TriggerStunEnd()
    {
        if (!IsDead)
        {
            ChangeState(EnemyState.Idle);
            wasParried = false;

            if (EnemyLockOnImage.Instance != null &&
                EnemyLockOnImage.Instance.target != null &&
                EnemyLockOnImage.Instance.target.IsChildOf(this.transform))
            {
                EnemyLockOnImage.Instance.CancelStrongAttackEffect();
            }
        }
        OnStunEnd?.Invoke();
    }

    public void TriggerPlayerDetected()
    {
        OnPlayerDetected?.Invoke();
    }

    public void TriggerPlayerHit(SkillBase hitSkill, int hitIndex)
    {
        OnPlayerHit?.Invoke(hitSkill, hitIndex);
    }

    public void TriggerHpChanged(float currentHp, float maxHp) => OnHpChanged?.Invoke(currentHp, maxHp);
    public void TriggerMpChanged(float currentMp, float maxMp) => OnMpChanged?.Invoke(currentMp, maxMp);
    public void TriggerPhaseTransitionStart(int phaseIndex) => OnPhaseTransitionStart?.Invoke(phaseIndex);

    public virtual void Revive()
    {
        StopAllCoroutines();
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        isForceDetected = false;
        isTakingDoT = false; // 부활 시 출혈 상태 초기화 버그 픽스
        SetPlayerDetection(false);

        ChangeState(EnemyState.Idle);
        wasParried = false;
        isGuarding = false;
        GetComponent<Collider>().enabled = true;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        SetMoveAnimations(0f, 0f, 0f);
        animator.ResetTrigger("Parried");
        animator.ResetTrigger("Hitted1");
        animator.ResetTrigger("Hitted2");
        animator.Play("Idle", 0, 0f);

        if (EnemyWeapons != null)
        {
            foreach (var weapon in EnemyWeapons)
            {
                if (weapon != null) weapon.SetActive(true);
            }
        }

        if (FakeWeapons != null)
        {
            foreach (var fakeWeapon in FakeWeapons)
            {
                if (fakeWeapon != null && fakeWeaponOriginMap.TryGetValue(fakeWeapon, out var originData))
                {
                    Rigidbody rb = fakeWeapon.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    fakeWeapon.transform.SetParent(originData.Parent);
                    fakeWeapon.transform.localPosition = originData.LocalPosition;
                    fakeWeapon.transform.localRotation = originData.LocalRotation;
                    fakeWeapon.transform.localScale = originData.LocalScale;

                    fakeWeapon.SetActive(false);
                }
            }
        }

        StopAllSkillEffects();
        StartCoroutine(RestoreShaderProperties(0f, 0.1f));

        decalGenerator?.ClearDecals();

        OnRevived?.Invoke();
    }

    private void CreateCameraTarget()
    {
        cameraTarget = transform.Find("CameraTarget");
        if (cameraTarget) return;
        GameObject targetObj = new GameObject("CameraTarget");
        targetObj.transform.SetParent(this.transform);
        cameraTarget = targetObj.transform;
        Collider col = GetComponent<Collider>();
        cameraTarget.position = col != null ? col.bounds.center : transform.position + Vector3.up * 1.5f;
    }

    private void InitializeEffects()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name.StartsWith("Effect_"))
            {
                string key = child.name.Replace("Effect_", "");
                if (!_skillEffects.ContainsKey(key)) _skillEffects.Add(key, child.gameObject);
            }
        }
    }

    public void PlayEffects(string skillPrefix) => StartCoroutine(SetActiveEffects(skillPrefix, true));
    public void StopEffects(string skillPrefix, float delay = 0f) => StartCoroutine(SetActiveEffects(skillPrefix, false, delay));
    public void StopAllEffects(float delay = 0f)
    {
        foreach (var key in _skillEffects.Keys) StartCoroutine(SetActiveEffects(key, false, delay));
    }
    private IEnumerator SetActiveEffects(string skillPrefix, bool isActive, float delay = 0f)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        var targets = _skillEffects.Keys.Where(k => k.StartsWith(skillPrefix));
        foreach (var key in targets) _skillEffects[key].SetActive(isActive);
    }

    public void StopAllSkillEffects()
    {
        foreach (var effectObj in _skillEffects.Values) if (effectObj != null) effectObj.SetActive(false);
    }

    private void CacheAndUnifyMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        List<Renderer> targetRenderers = new List<Renderer>();
        foreach (var r in renderers) if (!r.name.StartsWith("Effect_")) targetRenderers.Add(r);

        if (targetRenderers.Count > 0)
        {
            unifiedMaterial = targetRenderers[0].material;
            if (unifiedMaterial.HasFloat("_Step1"))
            {
                originalStep1 = unifiedMaterial.GetFloat("_Step1");
                originalStep2 = unifiedMaterial.GetFloat("_Step2");
                originalStep3 = unifiedMaterial.GetFloat("_Step3");
            }
            foreach (var r in targetRenderers) r.sharedMaterial = unifiedMaterial;
        }
    }

    protected IEnumerator FadeShaderPropertiesToZero(float fadeDuration)
    {
        yield return new WaitForSeconds(2);
        if (unifiedMaterial == null || !unifiedMaterial.HasFloat("_Step1")) yield break;
        float s1 = unifiedMaterial.GetFloat("_Step1"), s2 = unifiedMaterial.GetFloat("_Step2"), s3 = unifiedMaterial.GetFloat("_Step3");
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            unifiedMaterial.SetFloat("_Step1", Mathf.Lerp(s1, 0f, t));
            unifiedMaterial.SetFloat("_Step2", Mathf.Lerp(s2, 0f, t));
            unifiedMaterial.SetFloat("_Step3", Mathf.Lerp(s3, 0f, t));
            yield return null;
        }
    }

    protected IEnumerator RestoreShaderProperties(float delay, float fadeDuration = 5f)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (unifiedMaterial == null || !unifiedMaterial.HasFloat("_Step1")) yield break;
        float currentStep1 = unifiedMaterial.GetFloat("_Step1");
        float currentStep2 = unifiedMaterial.GetFloat("_Step2");
        float currentStep3 = unifiedMaterial.GetFloat("_Step3");
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            unifiedMaterial.SetFloat("_Step1", Mathf.Lerp(currentStep1, originalStep1, t));
            unifiedMaterial.SetFloat("_Step2", Mathf.Lerp(currentStep2, originalStep2, t));
            unifiedMaterial.SetFloat("_Step3", Mathf.Lerp(currentStep3, originalStep3, t));
            yield return null;
        }
        unifiedMaterial.SetFloat("_Step1", originalStep1);
        unifiedMaterial.SetFloat("_Step2", originalStep2);
        unifiedMaterial.SetFloat("_Step3", originalStep3);
    }

    public void SetMoveAnimations(float moveX, float moveZ, float run = 0f)
    {
        targetMoveX = moveX;
        targetMoveZ = moveZ;
        targetRun = run / 2;
    }

    public void ShakeCamera(float strength)
    {
        if (isTakingDoT) return;

        if (Player.Instance != null && Player.Instance.impulseSource != null)
        {
            Player.Instance.impulseSource.GenerateImpulse(strength);
        }
    }

    public void OnDrawGizmos()
    {
        if (ViewSkill == null) return;
        if (ViewSkill.GetType() != typeof(Skill_Melee)) return;
        Skill_Melee skill = (Skill_Melee)ViewSkill;
        Matrix4x4 originalMatrix = Gizmos.matrix;
        for (int i = 0; i < skill.hitDataList.Count; i++)
        {
            var hitData = skill.hitDataList[i];
            Gizmos.color = Color.HSVToRGB((float)i / skill.hitDataList.Count, 0.8f, 1f);

            Vector3 worldCenter = transform.position + (transform.rotation * hitData.centerOffset);
            Quaternion worldRotation = transform.rotation * Quaternion.Euler(hitData.boxRotation);

            Gizmos.matrix = Matrix4x4.TRS(worldCenter, worldRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, hitData.boxSize);
        }
        Gizmos.matrix = originalMatrix;
    }
}
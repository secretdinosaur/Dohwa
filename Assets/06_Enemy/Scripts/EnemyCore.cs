using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

public enum EnemyState { Idle, Chase, Attack, Stun, Dead, BackWalk, CooldownWalk }

[RequireComponent(typeof(Animator))]
public class EnemyCore : MonoBehaviour, IDamageable
{
    public EnemyState currentState = EnemyState.Idle;
    [HideInInspector] public Transform cameraTarget;
    public Item dropItem;
    public GameObject[] EnemyWeapons;

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
    public Action<Vector3, float> CmdMoveDirection;
    public Action CmdWarpToOriginal;
    public Action CmdTrySnapToNavMesh;
    public Action CmdForceDetectPlayer;

    public Func<bool> QueryIsPathPending;
    public Func<Vector3> QueryOriginalPosition;

    public Action<SkillBase> CmdPerformAttack;
    public Action CmdCancelAttack;
    public Action<float> OnAttackCompleted;
    public Func<SkillBase> QueryGetSkill;
    public Action<SkillBase, int> PlayHitSound;

    public Action<SkillBase> OnPlayerHit;

    public Action<float> CmdConsumeMP;
    public Func<float, bool> QueryHasEnoughMP;
    public Action<bool> CmdSetIgnorePlayerCollision;

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
    private Dictionary<GameObject, WeaponOriginData> weaponOriginMap = new Dictionary<GameObject, WeaponOriginData>();

    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        decalGenerator = GetComponent<DecalGenerator>();
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
                    weaponOriginMap[weapon] = new WeaponOriginData
                    {
                        Parent = weapon.transform.parent,
                        LocalPosition = weapon.transform.localPosition,
                        LocalRotation = weapon.transform.localRotation,
                        LocalScale = weapon.transform.localScale
                    };

                    if (weapon.TryGetComponent<Collider>(out Collider col))
                    {
                        col.enabled = false;
                    }
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
        isForceDetected = false;
        SetPlayerDetection(false);

        ChangeState(EnemyState.Dead);
        SetMoveAnimations(false, false, false);
        animator.ResetTrigger("Hitted1");
        animator.ResetTrigger("Hitted2");
        animator.ResetTrigger("Parried");

        animator.Play(UnityEngine.Random.Range(0, 2) == 0 ? "Dead_1" : "Dead_2");
        StopAllEffects();
        GetComponent<Collider>().enabled = false;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        if (EnemyWeapons != null)
        {
            foreach (var weapon in EnemyWeapons)
            {
                if (weapon != null)
                {
                    weapon.transform.SetParent(null);

                    if (!weapon.TryGetComponent<Collider>(out Collider col))
                    {
                        col = weapon.AddComponent<BoxCollider>();
                    }
                    col.enabled = true;

                    Rigidbody rb = weapon.GetComponent<Rigidbody>();
                    if (rb == null) rb = weapon.AddComponent<Rigidbody>();

                    rb.isKinematic = false;
                    rb.useGravity = true;

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
        Invoke(nameof(DeactivateObject), destroyDelay);
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
    }

    public void TriggerStunEnd()
    {
        if (!IsDead)
        {
            ChangeState(EnemyState.Idle);
            wasParried = false;
        }
        OnStunEnd?.Invoke();
    }

    public void TriggerPlayerDetected()
    {
        OnPlayerDetected?.Invoke();
    }

    public void TriggerPlayerHit(SkillBase hitSkill)
    {
        OnPlayerHit?.Invoke(hitSkill);
    }

    public void TriggerHpChanged(float currentHp, float maxHp) => OnHpChanged?.Invoke(currentHp, maxHp);
    public void TriggerMpChanged(float currentMp, float maxMp) => OnMpChanged?.Invoke(currentMp, maxMp);
    public void TriggerPhaseTransitionStart(int phaseIndex) => OnPhaseTransitionStart?.Invoke(phaseIndex);

    public virtual void Revive()
    {
        StopAllCoroutines();
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        isForceDetected = false;
        SetPlayerDetection(false);

        ChangeState(EnemyState.Idle);
        wasParried = false;
        isGuarding = false;
        GetComponent<Collider>().enabled = true;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        SetMoveAnimations(false, false, false);
        animator.ResetTrigger("Parried");
        animator.ResetTrigger("Hitted1");
        animator.ResetTrigger("Hitted2");
        animator.Play("Idle", 0, 0f);

        if (EnemyWeapons != null)
        {
            foreach (var weapon in EnemyWeapons)
            {
                if (weapon != null && weaponOriginMap.TryGetValue(weapon, out var originData))
                {
                    Rigidbody rb = weapon.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    if (weapon.TryGetComponent<Collider>(out Collider col))
                    {
                        col.enabled = false;
                    }

                    weapon.transform.SetParent(originData.Parent);
                    weapon.transform.localPosition = originData.LocalPosition;
                    weapon.transform.localRotation = originData.LocalRotation;
                    weapon.transform.localScale = originData.LocalScale;
                }
            }
        }

        StopAllSkillEffects();
        StartCoroutine(RestoreShaderProperties(0f, 0.1f));

        decalGenerator?.ClearDecals();

        OnRevived?.Invoke();
    }

    private void DeactivateObject() => gameObject.SetActive(false);

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

    public void SetMoveAnimations(bool run, bool walk, bool back)
    {
        animator.SetBool("isRun", run);
        animator.SetBool("isWalk", walk);
        animator.SetBool("isBackWalk", back);
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
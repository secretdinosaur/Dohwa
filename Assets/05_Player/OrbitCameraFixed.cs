using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic; // 💡 List 사용을 위해 필요합니다.

public class CameraController : MonoBehaviour
{
    public CinemachineCamera freeLookCam;
    public CinemachineCamera lockOnCam;
    public CinemachineCamera cutsceneCam;
    public CinemachineCamera knockdownCam;

    public CinemachineTargetGroup targetGroup;
    public CinemachineTargetGroup playerTargetGroup;
    public CinemachineTargetGroup knockdownTargetGroup;

    public Transform playerTransform;
    public int lockOnTargetIndex = 1;

    public float customLockOnBlendTime = 0.5f;
    public float customLockOffBlendTime = 0.5f;

    public float manualYawOffset = 0f;
    public float manualPitchOffset = 0f;

    Transform LastTarget;
    bool isLockedOn = false;

    CinemachineBrain cinemachineBrain;

    public static CameraController Instance;

    private void Awake()
    {
        Instance = this;
        cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
    }

    private void Start()
    {
        if (knockdownTargetGroup != null)
        {
            // 💡 수정됨: 배열(Array) 대신 리스트(List)를 사용하여 타겟 슬롯을 확장합니다.
            if (knockdownTargetGroup.Targets.Count < 2)
            {
                var newTargets = new List<CinemachineTargetGroup.Target>(knockdownTargetGroup.Targets);
                while (newTargets.Count < 2)
                {
                    newTargets.Add(new CinemachineTargetGroup.Target());
                }
                knockdownTargetGroup.Targets = newTargets;
            }

            if (playerTransform != null)
            {
                var targets = knockdownTargetGroup.Targets;
                var playerTarget = targets[0]; // Struct 복사
                playerTarget.Object = playerTransform;
                playerTarget.Weight = 1f;
                playerTarget.Radius = 1f;
                targets[0] = playerTarget; // 다시 덮어씌움
                knockdownTargetGroup.Targets = targets;
            }
        }
    }

    public void SetCinematicCamera(bool isActive, CinemachineCamera specificCam = null)
    {
        SetCameraInput(!isActive);

        CinemachineCamera targetCam = specificCam != null ? specificCam : cutsceneCam;

        if (isActive)
        {
            if (targetCam != null) targetCam.Priority = 30;

            freeLookCam.Priority = 5;
            lockOnCam.Priority = 5;
        }
        else
        {
            ResetCameraAxes();

            if (targetCam != null) targetCam.Priority = 0;

            if (isLockedOn)
            {
                lockOnCam.Priority = 20;
                freeLookCam.Priority = 5;
            }
            else
            {
                lockOnCam.Priority = 5;
                freeLookCam.Priority = 20;
            }
        }
    }

    public void SetKnockdownCamera(bool isActive)
    {
        if (knockdownCam == null) return;

        if (isActive)
        {
            if (knockdownTargetGroup != null)
            {
                knockdownCam.LookAt = knockdownTargetGroup.transform;
            }

            knockdownCam.Priority = 25;
        }
        else
        {
            knockdownCam.Priority = 0;
        }
    }

    public void ResetCameraAxes()
    {
        if (freeLookCam != null)
        {
            var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.HorizontalAxis.Value = -97.34587f;
                orbitalFollow.VerticalAxis.Value = 13.47806f;
                orbitalFollow.RadialAxis.Value = 1f;
            }
        }

        if (lockOnCam != null)
        {
            var lockOnOrbitalFollow = lockOnCam.GetComponent<CinemachineOrbitalFollow>();
            if (lockOnOrbitalFollow != null)
            {
                lockOnOrbitalFollow.HorizontalAxis.Value = -97.34587f;
                lockOnOrbitalFollow.VerticalAxis.Value = 13.47806f;
                lockOnOrbitalFollow.RadialAxis.Value = 1f;
            }
        }
    }

    public void SetLockOn(Transform enemyTransform)
    {
        Transform finalTarget = enemyTransform;
        EnemyCore enemyScript = enemyTransform.GetComponent<EnemyCore>();

        if (enemyScript != null && enemyScript.cameraTarget != null)
        {
            finalTarget = enemyScript.cameraTarget;
        }

        EnemyCore enemyCore = enemyTransform.GetComponent<EnemyCore>();
        if (enemyCore != null && enemyCore.cameraTarget != null)
        {
            finalTarget = enemyCore.cameraTarget;
        }

        // 1. 기본 타겟 그룹 락온 등록
        var targets = targetGroup.Targets;
        var targetInfo = targets[lockOnTargetIndex];
        targetInfo.Object = finalTarget;
        targetInfo.Weight = 0.04f;
        targetInfo.Radius = 0.2f;
        targets[lockOnTargetIndex] = targetInfo;
        targetGroup.Targets = targets;

        // 2. 넉다운 타겟 그룹에도 동일하게 락온 등록
        if (knockdownTargetGroup != null && lockOnTargetIndex < knockdownTargetGroup.Targets.Count)
        {
            var kdTargets = knockdownTargetGroup.Targets;
            var kdTargetInfo = kdTargets[lockOnTargetIndex];
            kdTargetInfo.Object = finalTarget;
            kdTargetInfo.Weight = 0.04f;
            kdTargetInfo.Radius = 0.2f;
            kdTargets[lockOnTargetIndex] = kdTargetInfo;
            knockdownTargetGroup.Targets = kdTargets;
        }

        LastTarget = enemyTransform;
        isLockedOn = true;

        lockOnCam.LookAt = finalTarget;
        if (cinemachineBrain != null)
        {
            var blend = cinemachineBrain.DefaultBlend;
            blend.Time = customLockOnBlendTime;
            cinemachineBrain.DefaultBlend = blend;
        }

        lockOnCam.Priority = 20;
        freeLookCam.Priority = 5;
    }

    public void DisableLockOn()
    {
        isLockedOn = false;

        // 1. 기본 타겟 그룹 락온 해제
        var targets = targetGroup.Targets;
        var targetInfo = targets[lockOnTargetIndex];
        targetInfo.Object = null;
        targetInfo.Weight = 0f;
        targets[lockOnTargetIndex] = targetInfo;
        targetGroup.Targets = targets;

        // 2. 넉다운 타겟 그룹 락온 해제
        if (knockdownTargetGroup != null && lockOnTargetIndex < knockdownTargetGroup.Targets.Count)
        {
            var kdTargets = knockdownTargetGroup.Targets;
            var kdTargetInfo = kdTargets[lockOnTargetIndex];
            kdTargetInfo.Object = null;
            kdTargetInfo.Weight = 0f;
            kdTargets[lockOnTargetIndex] = kdTargetInfo;
            knockdownTargetGroup.Targets = kdTargets;
        }

        var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
        if (orbitalFollow != null && playerTargetGroup != null)
        {
            Vector3 camEuler = Camera.main.transform.eulerAngles;
            Vector3 playerEuler = playerTargetGroup.transform.eulerAngles;

            float targetYaw = camEuler.y - playerEuler.y + manualYawOffset;

            float targetPitch = camEuler.x;
            if (targetPitch > 180f) targetPitch -= 360f;
            targetPitch += manualPitchOffset;

            orbitalFollow.HorizontalAxis.Value = targetYaw;
            orbitalFollow.VerticalAxis.Value = targetPitch;

            freeLookCam.PreviousStateIsValid = false;
        }

        if (cinemachineBrain != null)
        {
            var blend = cinemachineBrain.DefaultBlend;
            blend.Time = customLockOffBlendTime;
            cinemachineBrain.DefaultBlend = blend;
        }

        lockOnCam.Priority = 5;
        freeLookCam.Priority = 20;

        lockOnCam.LookAt = null;
        LastTarget = null;
    }

    public void SetCameraInput(bool isEnabled)
    {
        if (freeLookCam != null)
        {
            var inputController = freeLookCam.GetComponent<CinemachineInputAxisController>();

            if (inputController != null)
            {
                inputController.enabled = isEnabled;
            }
        }
    }

    private Coroutine weightCoroutine;
    public float duration = 0.25f;

    public void OnSkillStarted()
    {
        if (weightCoroutine != null) StopCoroutine(weightCoroutine);
        weightCoroutine = StartCoroutine(ChangeTargetWeights(new float[] { 0.1f, 0.1f, 0.1f, 0.9f }, duration));
    }

    public void OnSkillEnd()
    {
        if (weightCoroutine != null) StopCoroutine(weightCoroutine);
        weightCoroutine = StartCoroutine(ChangeTargetWeights(new float[] { 0.1f, 0.1f, 0.02f, 0.98f }, duration));
    }

    private System.Collections.IEnumerator ChangeTargetWeights(float[] targetWeights, float duration)
    {
        var targets = playerTargetGroup.Targets;
        float[] startWeights = new float[targets.Count];

        for (int i = 0; i < targets.Count; i++)
        {
            startWeights[i] = targets[i].Weight;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Weight = Mathf.Lerp(startWeights[i], targetWeights[i], t);
            }
            playerTargetGroup.Targets = targets;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].Weight = targetWeights[i];
        }
        playerTargetGroup.Targets = targets;
    }
}
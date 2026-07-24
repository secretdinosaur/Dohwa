using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public CinemachineCamera freeLookCam;
    public CinemachineCamera lockOnCam;
    public CinemachineCamera cutsceneCam;

    public CinemachineTargetGroup targetGroup;
    public CinemachineTargetGroup playerTargetGroup;

    public Transform playerTransform;
    public int lockOnTargetIndex = 1;

    public float customLockOnBlendTime = 0.5f;
    public float customLockOffBlendTime = 0.5f;

    Transform LastTarget;
    bool isLockedOn = false;

    CinemachineBrain cinemachineBrain;

    public static CameraController Instance;

    private void Awake()
    {
        Instance = this;
        cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
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

        var targets = targetGroup.Targets;
        var targetInfo = targets[lockOnTargetIndex];
        targetInfo.Object = finalTarget;
        targetInfo.Weight = 0.04f;
        targetInfo.Radius = 0.2f;
        targets[lockOnTargetIndex] = targetInfo;
        targetGroup.Targets = targets;

        LastTarget = enemyTransform;
        isLockedOn = true;

        lockOnCam.LookAt = finalTarget;

        // 우선순위 변경 전 락온 블렌드 타임 적용
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

        var targets = targetGroup.Targets;
        var targetInfo = targets[lockOnTargetIndex];
        targetInfo.Object = null;
        targetInfo.Weight = 0f;
        targets[lockOnTargetIndex] = targetInfo;
        targetGroup.Targets = targets;

        var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
        if (orbitalFollow != null)
        {
            Vector3 camAngles = Camera.main.transform.eulerAngles;
            orbitalFollow.HorizontalAxis.Value = camAngles.y + 180f;
            orbitalFollow.VerticalAxis.Value = camAngles.x;
        }

        // 우선순위 변경 전 락오프 블렌드 타임 적용
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
        weightCoroutine = StartCoroutine(ChangeTargetWeights(new float[] { 0.5f, 0.5f, 0.1f, 0f }, duration));
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

    private Vector3 originalPos;
}
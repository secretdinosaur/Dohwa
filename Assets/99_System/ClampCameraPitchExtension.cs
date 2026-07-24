using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
[AddComponentMenu("Cinemachine/Clamp Camera Pitch Dynamic")]
public class ClampCameraPitchExtension : CinemachineExtension
{
    public float maxPitch = 45f;
    public float minPitch = -10f;

    public float startOffsetDistance = 8f;
    public float fullOffsetDistance = 2f;
    public float maxDropOffset = -1.5f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (vcam.LookAt == null) return;

        if (stage == CinemachineCore.Stage.Body)
        {
            // 타겟과의 거리 계산
            float distance = Vector3.Distance(state.RawPosition, vcam.LookAt.position);

            // 거리에 따른 보간 비율 계산
            float t = Mathf.InverseLerp(startOffsetDistance, fullOffsetDistance, distance);
            float currentOffset = Mathf.Lerp(0f, maxDropOffset, t);

            Vector3 pos = state.RawPosition;
            pos.y += currentOffset;
            state.RawPosition = pos;
        }

        if (stage == CinemachineCore.Stage.Aim)
        {
            Vector3 euler = state.RawOrientation.eulerAngles;

            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            euler.x = pitch;

            state.RawOrientation = Quaternion.Euler(euler);
        }
    }
}
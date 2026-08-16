using System.Collections.Generic;
using UnityEngine;

public class AnimationDelayGhost : MonoBehaviour
{
    public Transform target;
    public int delayFrames = 5;

    private List<Transform> sourceBones = new List<Transform>();
    private List<Transform> ghostBones = new List<Transform>();

    // 필드 갱신을 위해 struct 대신 class 사용
    private class FrameData
    {
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        public Vector3[] boneLocalPositions;
        public Quaternion[] boneLocalRotations;
    }

    private FrameData[] historyBuffer;
    private int writeIndex = 0;
    private bool isBufferFilled = false;

    void Start()
    {
        if (target == null) return;

        // 잔상 객체의 애니메이터 비활성화 (직접 조작과 충돌 방지)
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        SkinnedMeshRenderer[] sourceRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer[] ghostRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        HashSet<Transform> uniqueSourceBones = new HashSet<Transform>();
        foreach (var renderer in sourceRenderers)
        {
            foreach (var bone in renderer.bones)
            {
                uniqueSourceBones.Add(bone);
            }
        }
        sourceBones.AddRange(uniqueSourceBones);

        foreach (var sBone in sourceBones)
        {
            Transform gBone = FindBoneByName(ghostRenderers, sBone.name);
            ghostBones.Add(gBone);
        }

        historyBuffer = new FrameData[delayFrames + 1];
        for (int i = 0; i < historyBuffer.Length; i++)
        {
            historyBuffer[i] = new FrameData
            {
                boneLocalPositions = new Vector3[sourceBones.Count],
                boneLocalRotations = new Quaternion[sourceBones.Count]
            };
        }
    }

    private Transform FindBoneByName(SkinnedMeshRenderer[] renderers, string boneName)
    {
        foreach (var renderer in renderers)
        {
            foreach (var bone in renderer.bones)
            {
                if (bone.name == boneName) return bone;
            }
        }
        return null;
    }

    void LateUpdate()
    {
        if (sourceBones.Count == 0) return;

        FrameData currentData = historyBuffer[writeIndex];

        // 루트의 월드 좌표 저장
        currentData.rootPosition = target.position;
        currentData.rootRotation = target.rotation;

        // 뼈는 로컬 좌표로 저장하여 계층 구조 오작동 방지
        for (int i = 0; i < sourceBones.Count; i++)
        {
            currentData.boneLocalPositions[i] = sourceBones[i].localPosition;
            currentData.boneLocalRotations[i] = sourceBones[i].localRotation;
        }

        writeIndex = (writeIndex + 1) % historyBuffer.Length;

        if (!isBufferFilled && writeIndex == 0)
        {
            isBufferFilled = true;
        }

        if (isBufferFilled)
        {
            FrameData pastData = historyBuffer[writeIndex];

            // 루트 이동
            transform.position = pastData.rootPosition;
            transform.rotation = pastData.rootRotation;

            for (int i = 0; i < ghostBones.Count; i++)
            {
                if (ghostBones[i] != null)
                {
                    ghostBones[i].localPosition = pastData.boneLocalPositions[i];
                    ghostBones[i].localRotation = pastData.boneLocalRotations[i];
                }
            }
        }
    }
}
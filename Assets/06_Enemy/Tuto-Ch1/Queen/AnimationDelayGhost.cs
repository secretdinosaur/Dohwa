using System.Collections.Generic;
using UnityEngine;

public class AnimationDelayGhost : MonoBehaviour
{
    public Transform target;
    public int delayFrames = 5;

    private List<Transform> sourceBones = new List<Transform>();
    private List<Transform> ghostBones = new List<Transform>();

    private struct BoneData
    {
        public Vector3[] positions;
        public Quaternion[] rotations;
    }

    private BoneData[] historyBuffer;
    private int writeIndex = 0;
    private bool isBufferFilled = false;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target이 할당되지 않았습니다.");
            return;
        }

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

        historyBuffer = new BoneData[delayFrames + 1];
        for (int i = 0; i < historyBuffer.Length; i++)
        {
            historyBuffer[i] = new BoneData
            {
                positions = new Vector3[sourceBones.Count],
                rotations = new Quaternion[sourceBones.Count]
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

        BoneData currentData = historyBuffer[writeIndex];

        for (int i = 0; i < sourceBones.Count; i++)
        {
            currentData.positions[i] = sourceBones[i].position;
            currentData.rotations[i] = sourceBones[i].rotation;
        }

        writeIndex = (writeIndex + 1) % historyBuffer.Length;

        if (!isBufferFilled && writeIndex == 0)
        {
            isBufferFilled = true;
        }

        if (isBufferFilled)
        {
            BoneData pastData = historyBuffer[writeIndex];

            for (int i = 0; i < ghostBones.Count; i++)
            {
                if (ghostBones[i] != null)
                {
                    ghostBones[i].position = pastData.positions[i];
                    ghostBones[i].rotation = pastData.rotations[i];
                }
            }
        }
    }
}
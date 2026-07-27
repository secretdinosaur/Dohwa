using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public struct BoneDecalSettings
{
    public Transform targetBone;
    public Vector2 widthRange;
    public Vector2 heightRange;
    public Vector2 rotationRange;
    public Vector3 offsetMin;
    public Vector3 offsetMax;
}

public class DecalGenerator : MonoBehaviour
{
    public GameObject decalPrefab;
    public BoneDecalSettings[] boneSettings;

    public int maxDecals = 20;
    public float depth = 0.3f;
    public float fadeDuration = 0.2f;

    private Queue<GameObject> activeDecals = new Queue<GameObject>();

    public void GenerateWoundsByPercentage(float percentage)
    {
        int generateCount = Mathf.FloorToInt(maxDecals * percentage);
        for (int i = 0; i < generateCount; i++)
        {
            GenerateWound();
        }
    }

    public int GetActiveDecalCount()
    {
        return activeDecals.Count;
    }
    public void ClearDecals()
    {
        while (activeDecals.Count > 0)
        {
            GameObject decal = activeDecals.Dequeue();
            if (decal != null)
            {
                Destroy(decal);
            }
        }
    }

    public void GenerateWound()
    {
        if (boneSettings == null || boneSettings.Length == 0)
            return;

        if (activeDecals.Count >= maxDecals)
        {
            RemoveOldestWound();
        }

        BoneDecalSettings selectedSetting = boneSettings[Random.Range(0, boneSettings.Length)];

        if (selectedSetting.targetBone == null)
            return;

        GameObject decalInstance = Instantiate(decalPrefab, selectedSetting.targetBone);

        float randomX = Random.Range(selectedSetting.offsetMin.x, selectedSetting.offsetMax.x);
        float randomY = Random.Range(selectedSetting.offsetMin.y, selectedSetting.offsetMax.y);
        float randomZ = Random.Range(selectedSetting.offsetMin.z, selectedSetting.offsetMax.z);

        Vector3 localPos = new Vector3(randomX, randomY, randomZ);
        decalInstance.transform.localPosition = localPos;

        Quaternion baseRotation = Quaternion.identity;
        if (localPos != Vector3.zero)
        {
            baseRotation = Quaternion.LookRotation(-localPos);
        }

        float randomRot = Random.Range(selectedSetting.rotationRange.x, selectedSetting.rotationRange.y);
        decalInstance.transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, randomRot);

        DecalProjector projector = decalInstance.GetComponent<DecalProjector>();
        if (projector != null)
        {
            float randomWidth = Random.Range(selectedSetting.widthRange.x, selectedSetting.widthRange.y);
            float randomHeight = Random.Range(selectedSetting.heightRange.x, selectedSetting.heightRange.y);
            projector.size = new Vector3(randomWidth, randomHeight, depth);

            projector.material = new Material(projector.material);
            StartCoroutine(FadeIn(projector.material));
        }

        activeDecals.Enqueue(decalInstance);
    }

    public void RemoveOldestWound()
    {
        if (activeDecals.Count > 0)
        {
            GameObject oldestDecal = activeDecals.Dequeue();

            if (oldestDecal != null)
            {
                DecalProjector projector = oldestDecal.GetComponent<DecalProjector>();
                if (projector != null && projector.material != null)
                {
                    StartCoroutine(FadeOutAndDestroy(oldestDecal, projector.material));
                }
                else
                {
                    Destroy(oldestDecal);
                }
            }
        }
    }

    private IEnumerator FadeIn(Material mat)
    {
        float time = 0f;
        mat.SetFloat("_Alpha", 0f);

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            mat.SetFloat("_Alpha", Mathf.Lerp(0f, 1f, time / fadeDuration));
            yield return null;
        }
        mat.SetFloat("_Alpha", 1f);
    }

    private IEnumerator FadeOutAndDestroy(GameObject decal, Material mat)
    {
        float time = 0f;
        float startAlpha = mat.GetFloat("_Alpha");

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            mat.SetFloat("_Alpha", Mathf.Lerp(startAlpha, 0f, time / fadeDuration));
            yield return null;
        }

        Destroy(mat);
        Destroy(decal);
    }

    [ContextMenu("Load Default Bone Settings")]
    public void LoadDefaultBoneSettings()
    {
        List<BoneDecalSettings> newSettings = new List<BoneDecalSettings>();
        Transform[] allBones = GetComponentsInChildren<Transform>(true);

        void TryAddSetting(string boneName, Vector2 wRange, Vector2 hRange, Vector2 rotRange, Vector3 offMin, Vector3 offMax)
        {
            Transform foundBone = null;
            for (int i = 0; i < allBones.Length; i++)
            {
                if (allBones[i].name == boneName)
                {
                    foundBone = allBones[i];
                    break;
                }
            }

            if (foundBone != null)
            {
                newSettings.Add(new BoneDecalSettings
                {
                    targetBone = foundBone,
                    widthRange = wRange,
                    heightRange = hRange,
                    rotationRange = rotRange,
                    offsetMin = offMin,
                    offsetMax = offMax
                });
            }
        }

        TryAddSetting("DEF-spine.002", new Vector2(0.03f, 0.05f), new Vector2(0.1f, 0.9f), new Vector2(-45f, 45f), new Vector3(-0.015f, -0.015f, -0.015f), new Vector3(0.015f, 0.015f, -0.015f));
        TryAddSetting("DEF-spine.002", new Vector2(0.03f, 0.05f), new Vector2(0.1f, 0.9f), new Vector2(-45f, 45f), new Vector3(-0.015f, -0.015f, 0.015f), new Vector3(0.015f, 0.015f, 0.015f));
        TryAddSetting("DEF-shin.L", new Vector2(0.03f, 0.05f), new Vector2(0.1f, 1.2f), new Vector2(-45f, 45f), new Vector3(-0.01f, -0.01f, -0.03f), new Vector3(-0.01f, 0.01f, 0.02f));
        TryAddSetting("DEF-shin.R.001", new Vector2(0.03f, 0.05f), new Vector2(0.1f, 1.2f), new Vector2(-45f, 45f), new Vector3(0.01f, -0.01f, -0.03f), new Vector3(0.01f, 0.01f, 0.02f));
        TryAddSetting("DEF-upper_arm.R.001", new Vector2(0.03f, 0.04f), new Vector2(0.1f, 0.4f), new Vector2(-120f, -90f), new Vector3(-0.01f, -0.005f, -0.02f), new Vector3(-0.01f, 0.01f, 0.02f));
        TryAddSetting("DEF-upper_arm.L.001", new Vector2(0.03f, 0.04f), new Vector2(0.1f, 0.4f), new Vector2(-120f, -90f), new Vector3(0.01f, -0.005f, -0.02f), new Vector3(0.01f, 0.01f, 0.02f));
        TryAddSetting("DEF-forearm.L.001", new Vector2(0.03f, 0.04f), new Vector2(0.1f, 0.3f), new Vector2(-120f, -90f), new Vector3(0.01f, -0.005f, -0.02f), new Vector3(0.01f, 0.01f, 0.02f));
        TryAddSetting("DEF-forearm.R.001", new Vector2(0.03f, 0.04f), new Vector2(0.1f, 0.3f), new Vector2(-120f, -90f), new Vector3(-0.01f, -0.005f, -0.02f), new Vector3(-0.01f, 0.01f, 0.02f));

        boneSettings = newSettings.ToArray();
    }
}
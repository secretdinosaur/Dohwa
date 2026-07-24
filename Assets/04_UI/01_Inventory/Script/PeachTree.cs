using UnityEngine;
using System.Collections;

public class PeachTree : MonoBehaviour
{
    public static PeachTree Instance { get; private set; }

    public Material[] mat1 = new Material[6];
    public Material[] mat2 = new Material[6];

    private readonly float[,] growthTargets = new float[7, 6]
    {
        { 0f, 0f, 0f, 0f, 0f, 0f },
        { 0.5f, 0f, 0f, 0f, 0f, 0f },
        { 0.8f, 0.5f, 0f, 0f, 0f, 0f },
        { 1f, 0.8f, 0.5f, 0f, 0f, 0f },
        { 1f, 1f, 0.8f, 0.5f, 0f, 0f },
        { 1.1f, 1f, 1f, 1f, 0.8f, 0.5f },
        { 1.1f, 1.1f, 1f, 1f, 1f, 0.8f },
    };

    private Coroutine currentGrowthCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    public void SetInitialStage(int stageLevel)
    {
        stageLevel = Mathf.Clamp(stageLevel, 0, 6);
        for (int i = 0; i < 6; i++)
        {
            float targetValue = growthTargets[stageLevel, i];

            if (mat1.Length > i && mat1[i] != null) mat1[i].SetFloat("_Growth", targetValue);
            if (mat2.Length > i && mat2[i] != null) mat2[i].SetFloat("_Growth", targetValue);
        }
    }

    public void GrowToStage(int stageLevel, float duration = 2f)
    {
        stageLevel = Mathf.Clamp(stageLevel, 0, 6);

        if (currentGrowthCoroutine != null)
        {
            StopCoroutine(currentGrowthCoroutine);
        }

        currentGrowthCoroutine = StartCoroutine(GrowCoroutine(stageLevel, duration));
    }

    private IEnumerator GrowCoroutine(int stageLevel, float duration)
    {
        float[] startValues = new float[6];
        for (int i = 0; i < 6; i++)
        {
            if (mat1.Length > i && mat1[i] != null)
                startValues[i] = mat1[i].GetFloat("_Growth");
            else
                startValues[i] = 0f;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < 6; i++)
            {
                float targetValue = growthTargets[stageLevel, i];
                float currentValue = Mathf.Lerp(startValues[i], targetValue, easeOutT);

                if (mat1.Length > i && mat1[i] != null) mat1[i].SetFloat("_Growth", currentValue);
                if (mat2.Length > i && mat2[i] != null) mat2[i].SetFloat("_Growth", currentValue);
            }
            yield return null;
        }

        SetInitialStage(stageLevel);
        currentGrowthCoroutine = null;
    }
}
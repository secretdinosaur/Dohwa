using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIFader : MonoBehaviour
{
    public GameObject[] exceptObjects;

    private List<Graphic> targetGraphics = new List<Graphic>();
    private List<Button> targetButtons = new List<Button>();
    private float[] maxAlphas;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        GatherGraphics();
    }

    public void EnableUI(float time)
    {
        gameObject.SetActive(true);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(true, time, false));
    }

    public void DisableUI(float time)
    {
        if (!gameObject.activeInHierarchy) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(false, time, true));
    }

    private void GatherGraphics()
    {
        targetGraphics.Clear();
        targetButtons.Clear();

        Graphic[] allGraphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in allGraphics)
        {
            if (g is Image || g is TextMeshProUGUI)
            {
                if (!IsExcluded(g.transform))
                {
                    targetGraphics.Add(g);
                }
            }
        }

        maxAlphas = new float[targetGraphics.Count];
        for (int i = 0; i < targetGraphics.Count; i++)
        {
            maxAlphas[i] = targetGraphics[i].color.a;
        }

        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (Button b in allButtons)
        {
                targetButtons.Add(b);
        }
    }

    private bool IsExcluded(Transform objTransform)
    {
        if (exceptObjects == null) return false;

        foreach (GameObject exObj in exceptObjects)
        {
            if (exObj != null && (objTransform == exObj.transform || objTransform.IsChildOf(exObj.transform)))
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator FadeRoutine(bool isFadeIn, float duration, bool disableOnComplete)
    {
        float elapsedTime = 0f;
        float[] currentStartAlphas = new float[targetGraphics.Count];

        for (int i = 0; i < targetGraphics.Count; i++)
        {
            if (targetGraphics[i] != null)
            {
                currentStartAlphas[i] = targetGraphics[i].color.a;
                float target = isFadeIn ? maxAlphas[i] : 0f;
            }
        }
        foreach (Button b in targetButtons)
        {
            if (b != null) b.interactable = false;
        }

        if (duration > 0)
        {
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                for (int i = 0; i < targetGraphics.Count; i++)
                {
                    if (targetGraphics[i] == null) continue;

                    float targetAlpha = isFadeIn ? maxAlphas[i] : 0f;
                    Color c = targetGraphics[i].color;
                    c.a = Mathf.Lerp(currentStartAlphas[i], targetAlpha, t);
                    targetGraphics[i].color = c;
                }
                yield return null;
            }
        }

        SetFinalState(isFadeIn);

        if (disableOnComplete)
        {
            gameObject.SetActive(false);
        }
        else
        {
            foreach (Button b in targetButtons)
            {
                if (b != null) b.interactable = true;
            }
        }
    }

    private void SetFinalState(bool isFadeIn)
    {
        for (int i = 0; i < targetGraphics.Count; i++)
        {
            if (targetGraphics[i] != null)
            {
                Color c = targetGraphics[i].color;
                c.a = isFadeIn ? maxAlphas[i] : 0f;
                targetGraphics[i].color = c;
            }
        }
    }
}
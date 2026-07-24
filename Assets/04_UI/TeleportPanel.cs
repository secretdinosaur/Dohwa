using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FadePanel : MonoBehaviour
{
    private Image panelImage;
    private Coroutine fadeCoroutine;
    public static bool isFadePanelOn;
    private void Awake()
    {
        panelImage = GetComponent<Image>();
        ImmediateActive();
        StartCoroutine(HideAfter1s());
    }

    IEnumerator HideAfter1s()
    {
        yield return new WaitForSeconds(1);
        HidePanel();
    }

    // 기존 OnEnable 역할을 명시적 메서드로 분리
    public void ShowPanel(float duration = 1f)
    {
        if (panelImage == null) panelImage = GetComponent<Image>();

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        isFadePanelOn = true;

        gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadeRoutine(panelImage.color.a, 1f, false, duration));
    }

    public void ImmediateActive()
    {
        if (panelImage == null) panelImage = GetComponent<Image>();
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        isFadePanelOn = true;
        Color color = panelImage.color;
        color.a = 1f;
        panelImage.color = color;
    }

    public void HidePanel(float duration = 2f)
    {
        if (!gameObject.activeInHierarchy) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(panelImage.color.a, 0f, true, duration));
    }

    private IEnumerator FadeRoutine(float start, float end, bool disableOnComplete, float fadeDuration)
    {
        float timer = 0f;
        Color color = panelImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            float curve = 1f - Mathf.Pow(1f - t, 3f);

            color.a = Mathf.Lerp(start, end, curve);
            panelImage.color = color;

            yield return null;
        }

        color.a = end;
        panelImage.color = color;

        if (disableOnComplete)
        {
            isFadePanelOn = false;
            gameObject.SetActive(false);
        }
    }
}
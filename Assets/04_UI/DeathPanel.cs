using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Image))]
public class DeathPanel : MonoBehaviour
{
    public float fadeDuration = 1f;
    private Image panelImage;
    private TextMeshProUGUI[] textComponents;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        panelImage = GetComponent<Image>();
        textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (panelImage == null) panelImage = GetComponent<Image>();
        if (textComponents == null) textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, false));
    }

    public void HidePanel()
    {
        if (!gameObject.activeInHierarchy) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(panelImage.color.a, 0f, true));
    }

    private IEnumerator FadeRoutine(float start, float end, bool disableOnComplete)
    {
        float timer = 0f;
        Color panelColor = panelImage.color;

        Color[] textColors = new Color[textComponents.Length];
        for (int i = 0; i < textComponents.Length; i++)
        {
            textColors[i] = textComponents[i].color;
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;
            float curve = 1f - Mathf.Pow(1f - t, 3f);
            float currentAlpha = Mathf.Lerp(start, end, curve);

            panelColor.a = currentAlpha;
            panelImage.color = panelColor;

            for (int i = 0; i < textComponents.Length; i++)
            {
                textColors[i].a = currentAlpha;
                textComponents[i].color = textColors[i];
            }

            yield return null;
        }

        panelColor.a = end;
        panelImage.color = panelColor;

        for (int i = 0; i < textComponents.Length; i++)
        {
            textColors[i].a = end;
            textComponents[i].color = textColors[i];
        }

        if (disableOnComplete)
        {
            gameObject.SetActive(false);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Burned : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        List<Renderer> targetRenderers = new List<Renderer>();
        foreach (var r in renderers)
        {
            if (!r.name.StartsWith("Effect_"))
            {
                targetRenderers.Add(r);
            }
        }

        if (targetRenderers.Count > 0)
        {
            unifiedMaterial = targetRenderers[0].material;


            for (int i = 0; i < targetRenderers.Count; i++)
            {
                targetRenderers[i].sharedMaterial = unifiedMaterial;
            }
        }
        StartCoroutine(FadeShaderPropertiesToZero(1f));
    }
    Material unifiedMaterial;

    // Update is called once per frame
    void Update()
    {
        
    }

    protected IEnumerator FadeShaderPropertiesToZero(float fadeDuration = 5)
    {
        yield return new WaitForSeconds(2);
        float startStep1 = unifiedMaterial.GetFloat("_Step1");
        float startStep2 = unifiedMaterial.GetFloat("_Step2");
        float startStep3 = unifiedMaterial.GetFloat("_Step3");

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            unifiedMaterial.SetFloat("_Step1", Mathf.Lerp(startStep1, 0f, t));
            unifiedMaterial.SetFloat("_Step2", Mathf.Lerp(startStep2, 0f, t));
            unifiedMaterial.SetFloat("_Step3", Mathf.Lerp(startStep3, 0f, t));

            yield return null;
        }
    }
}

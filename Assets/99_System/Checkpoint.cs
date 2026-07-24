using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Checkpoint : NPC
{
    public override int Type => 3;
    public Transform respawnPoint;

    public LocalizedString checkpointName;

    public ParticleSystem activeParticle;
    public Renderer targetRenderer;
    public float fadeDuration = 1.0f;

    public float enemyCheckRadius = 5f;

    private Coroutine fadeCoroutine;
    private Material cachedMaterial;
    private int colorPropId;

    public GameObject CannotCheckPanel;
    private Coroutine cannotCheckCoroutine;

    public string GetDisplayName()
    {
        if (checkpointName != null)
            return checkpointName.GetLocalizedString();
        return gameObject.name;
    }

    private void Awake()
    {
        if (respawnPoint == null) respawnPoint = transform;

        if (targetRenderer != null)
        {
            cachedMaterial = targetRenderer.material;
            string colorProp = cachedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            colorPropId = Shader.PropertyToID(colorProp);

            if (cachedMaterial.HasProperty(colorPropId))
            {
                Color color = cachedMaterial.GetColor(colorPropId);
                color.a = 0f;
                cachedMaterial.SetColor(colorPropId, color);
            }
        }

    }

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        if (Physics.CheckSphere(transform.position, enemyCheckRadius, LayerMask.GetMask("Enemy")) || Player.Instance.isDetected)
        {
            if (cannotCheckCoroutine != null) StopCoroutine(cannotCheckCoroutine);
            cannotCheckCoroutine = StartCoroutine(ShowCannotCheckPanelRoutine());
            return;
        }

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.ActivateCheckpoint(this);
            DataManager.Instance.AllSave();
        }

        if (activeParticle != null)
        {
            activeParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            activeParticle.Play();
        }

        if (targetRenderer != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeAlphaRoutine(1f));
        }
    }

    public virtual void Exit()
    {
        if (activeParticle != null && activeParticle.isPlaying)
        {
            activeParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (targetRenderer != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeAlphaRoutine(0f));
        }
    }

    private IEnumerator ShowCannotCheckPanelRoutine()
    {
        if (CannotCheckPanel == null) yield break;

        if (!CannotCheckPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
        {
            canvasGroup = CannotCheckPanel.AddComponent<CanvasGroup>();
        }

        CannotCheckPanel.SetActive(true);
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / 1f);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(1);
        elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 3f);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        CannotCheckPanel.SetActive(false);
        cannotCheckCoroutine = null;
    }

    private IEnumerator FadeAlphaRoutine(float targetAlpha)
    {
        if (cachedMaterial == null) yield break;
        if (targetAlpha == 0) DungeonManager.Instance.ReviveAllEnemies();
        else if (targetAlpha == 1)
        {
            Player.Instance.HP = StatManager.Instance.MaxHp;
            Player.Instance.MP = StatManager.Instance.MaxMp;
        }
            Color currentColor = cachedMaterial.GetColor(colorPropId);
        float startAlpha = currentColor.a;
        float elapsed = 0f;

        float distance = Mathf.Abs(targetAlpha - startAlpha);
        float currentDuration = fadeDuration * distance;

        if (currentDuration <= 0f)
        {
            fadeCoroutine = null;
            yield break;
        }

        while (elapsed < currentDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / currentDuration;

            currentColor.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            cachedMaterial.SetColor(colorPropId, currentColor);

            yield return null;
        }

        currentColor.a = targetAlpha;
        cachedMaterial.SetColor(colorPropId, currentColor);
        fadeCoroutine = null;

    }

    public Vector3 GetRespawnPosition()
    {
        Vector3 targetPos = respawnPoint != null ? respawnPoint.position : transform.position;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return targetPos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        var p = respawnPoint != null ? respawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(p + Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawIcon(transform.position, "sv_label_0", true);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, enemyCheckRadius);
    }
}
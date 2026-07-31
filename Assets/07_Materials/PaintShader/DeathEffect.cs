using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class InkBrushEffectManager : MonoBehaviour
{
    [Header("References")]
    public GameObject inkPrefab;
    public Transform canvasParent;
    public Sprite[] deathEffectSprites;
    public static InkBrushEffectManager Instance;

    [Header("Death Effect Settings")]
    public int deathSlashCount = 8;
    public float deathSlashDuration = 0.1f;
    public float deathSlashJitter = 50f;
    public float delayBeforeScatter = 0.5f;
    public int scatterCount = 20;

    [Header("Common Settings")]
    public float growthDuration = 0.5f;
    public float defaultMaxScale = 2.5f;

    [Header("Sound Settings")]
    public AudioClip[] deathInkSounds;
    public float minDeathSoundPitch = 0.4f;

    private Coroutine deathSequenceCoroutine;
    private Coroutine deathScatterCoroutine;

    public Transform deathEffectContainer;
    public Transform hitEffectContainer;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayDeathEffect()
    {
        ClearDeathEffects();
        deathSequenceCoroutine = StartCoroutine(DeathSequenceRoutine());
    }

    public void ClearDeathEffects()
    {
        if (deathSequenceCoroutine != null) StopCoroutine(deathSequenceCoroutine);
        if (deathScatterCoroutine != null) StopCoroutine(deathScatterCoroutine);

        if (deathEffectContainer != null)
        {
            foreach (Transform child in deathEffectContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void PlayHitEffect(HitEffectData data)
    {
        RectTransform parentRect = canvasParent.GetComponent<RectTransform>();
        int count = Random.Range(data.minCount, data.maxCount + 1);
        float scale = Random.Range(data.minScale, data.maxScale);
        float speed = Random.Range(data.minSpeed, data.maxSpeed);

        bool isLine = data.startPosition >= ScreenPosition.Top || data.endPosition >= ScreenPosition.Top;
        bool isStab = (data.startPosition == data.endPosition);

        if (isLine)
        {
            StartCoroutine(LineHitRoutine(count, scale, data, speed));
        }
        else
        {
            Vector2 start = data.GetRandomPosInZone(data.startPosition, parentRect);
            Vector2 end = data.GetRandomPosInZone(data.endPosition, parentRect);
            StartCoroutine(HitRoutine(start, end, count, scale, speed, isStab, data));
        }
    }

    IEnumerator LineHitRoutine(int count, float scale, HitEffectData data, float speed)
    {
        RectTransform parentRect = canvasParent.GetComponent<RectTransform>();

        int steps = Mathf.Max(2, data.lineSteps);
        int countPerStep = count / steps;
        int extraCount = count % steps;

        for (int s = 0; s < steps; s++)
        {
            float t = (float)s / (steps - 1);
            int currentStepCount = countPerStep + (s == steps - 1 ? extraCount : 0);

            for (int i = 0; i < currentStepCount; i++)
            {
                Vector2 p1 = data.GetRandomPosInZone(data.startPosition, parentRect);
                Vector2 p2 = data.GetRandomPosInZone(data.endPosition, parentRect);

                Vector2 stepBasePos = Vector2.Lerp(p1, p2, t);
                CreateHitInk(stepBasePos + new Vector2(Random.Range(45f, 75f), Random.Range(45f, 75f)), scale, data);
            }

            yield return new WaitForSeconds(speed);
        }
    }

    IEnumerator HitRoutine(Vector2 startPos, Vector2 endPos, int count, float scale, float speed, bool isStab, HitEffectData data)
    {
        float elapsedTime = 0f;
        int spawnedCount = 0;
        float[] spawnTimes = new float[count];

        for (int i = 0; i < count; i++)
            spawnTimes[i] = (count <= 1) ? 0f : Random.Range(0f, speed);

        System.Array.Sort(spawnTimes);

        while (spawnedCount < count)
        {
            while (spawnedCount < count && elapsedTime >= spawnTimes[spawnedCount])
            {
                float t = (count <= 1) ? 0.5f : (float)spawnedCount / (count - 1);
                Vector2 direction = endPos - startPos;
                Vector2 sideOffset = Vector2.zero;

                if (direction.sqrMagnitude > 0.001f)
                {
                    float curve = Mathf.Sin(t * Mathf.PI);
                    sideOffset = Vector2.Perpendicular(direction).normalized * (curve * Random.Range(-30f, 30f));
                }

                Vector2 basePos = isStab ? startPos : Vector2.Lerp(startPos, endPos, t + Random.Range(-0.2f, 0.2f)) + sideOffset;
                float jitter = isStab ? 60f : 100f;
                Vector2 randomOffset = new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));

                CreateHitInk(basePos + randomOffset, scale, data);
                spawnedCount++;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    public void ClearHitEffects()
    {
        if (hitEffectContainer != null)
        {
            foreach (Transform child in hitEffectContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
    void CreateHitInk(Vector2 anchoredPos, float scale, HitEffectData data)
    {
        Transform parentTransform = (hitEffectContainer != null) ? hitEffectContainer : canvasParent;
        GameObject obj = Instantiate(inkPrefab, parentTransform);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;

        Image img = obj.GetComponent<Image>();
        Image[] children = obj.GetComponentsInChildren<Image>();
        Image img2 = (children.Length > 1) ? children[1] : null;

        if (deathEffectSprites.Length > 0)
        {
            Sprite selectedSprite = deathEffectSprites[Random.Range(0, deathEffectSprites.Length)];
            img.sprite = selectedSprite;
            if (img2 != null) img2.sprite = selectedSprite;
        }

        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
        StartCoroutine(HitGrowRoutine(rt, scale, data, img, img2));
    }

    IEnumerator HitGrowRoutine(RectTransform target, float scale, HitEffectData data, Image img, Image img2)
    {
        float timer = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * Random.Range(scale * 0.8f, scale * 1.2f);

        while (timer < data.growthDuration)
        {
            timer += Time.deltaTime;
            float t = timer / data.growthDuration;
            float curve = 1f - Mathf.Pow(1f - t, 4f);
            if (target != null) target.localScale = Vector3.Lerp(startScale, endScale, curve);
            yield return null;
        }
        if (target != null) target.localScale = endScale;

        yield return new WaitForSeconds(data.stayDuration);

        float fadeTimer = 0f;
        float randomDuration = data.fadeDuration + Random.Range(-data.fadeDuration * 0.8f, data.fadeDuration * 1f);
        while (fadeTimer < randomDuration)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / randomDuration);
            if (img != null) { Color c = img.color; c.a = alpha; img.color = c; }
            if (img2 != null) { Color c2 = img2.color; c2.a = alpha; img2.color = c2; }
            yield return null;
        }

        if (target != null) Destroy(target.gameObject);
    }

    IEnumerator DeathSequenceRoutine()
    {
        yield return StartCoroutine(DeathSlashRoutine());
        yield return new WaitForSeconds(delayBeforeScatter);
        SpawnScatterFullScreen();
    }

    IEnumerator DeathSlashRoutine()
    {
        RectTransform parentRect = canvasParent.GetComponent<RectTransform>();
        float w = parentRect.rect.width;
        float h = parentRect.rect.height;
        float outsideX = w * 0.6f;
        float randomY = h * 0.3f;

        Vector2 startPos = new Vector2(-outsideX, Random.Range(-randomY, randomY));
        Vector2 endPos = new Vector2(outsideX, Random.Range(-randomY, randomY));

        if (Random.value > 0.5f) { Vector2 temp = startPos; startPos = endPos; endPos = temp; }

        for (int i = 0; i < deathSlashCount; i++)
        {
            float t = (float)i / Mathf.Max(1, deathSlashCount - 1);
            Vector2 basePos = Vector2.Lerp(startPos, endPos, t);
            Vector2 randomOffset = new Vector2(Random.Range(-deathSlashJitter, deathSlashJitter), Random.Range(-deathSlashJitter, deathSlashJitter));

            CreateInk(basePos + randomOffset, defaultMaxScale);

            if (deathInkSounds != null && deathInkSounds.Length > 0)
            {
                AudioClip clip = deathInkSounds[Random.Range(0, deathInkSounds.Length)];
                float currentPitch = Mathf.Lerp(1f, minDeathSoundPitch, t);

                SoundManager.Instance.PlaySound(clip, currentPitch);
            }

            if ((i + 1) % 5 == 0) yield return null;
        }
    }

    void SpawnScatterFullScreen()
    {
        RectTransform parentRect = canvasParent.GetComponent<RectTransform>();
        float w = parentRect.rect.width;
        float h = parentRect.rect.height;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(scatterCount));
        int rows = Mathf.CeilToInt((float)scatterCount / cols);
        float cellW = w / cols;
        float cellH = h / rows;
        float startX = -w / 2;
        float startY = -h / 2;

        List<Vector2> positions = new List<Vector2>();
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                if (positions.Count >= scatterCount) break;
                float randX = Random.Range(10f, cellW - 10f);
                float randY = Random.Range(10f, cellH - 10f);
                positions.Add(new Vector2(startX + (x * cellW) + randX, startY + (y * cellH) + randY));
            }
        }
        ShuffleList(positions);
        deathScatterCoroutine = StartCoroutine(DeathScatterSpawnRoutine(positions));
    }

    IEnumerator DeathScatterSpawnRoutine(List<Vector2> positions)
    {
        float currentDelay = 1f;
        float minDelay = 0.01f;
        float acceleration = 0.7f;
        float growth = 0f;

        int totalCount = positions.Count;
        int currentIndex = 0;

        foreach (var pos in positions)
        {
            CreateInk(pos, defaultMaxScale + growth);

            if (deathInkSounds != null && deathInkSounds.Length > 0)
            {
                AudioClip clip = deathInkSounds[Random.Range(0, deathInkSounds.Length)];
                float t = (float)currentIndex / Mathf.Max(1, totalCount - 1);
                float currentPitch = Mathf.Lerp(1f, minDeathSoundPitch, t);

                SoundManager.Instance.PlaySound(clip, currentPitch);
            }

            yield return new WaitForSeconds(currentDelay);
            currentDelay = Mathf.Max(minDelay, currentDelay * acceleration);
            growth += 0.35f;
            currentIndex++;
        }
    }

    void CreateInk(Vector2 anchoredPos, float scale)
    {
        Transform parentTransform = (deathEffectContainer != null) ? deathEffectContainer : canvasParent;
        GameObject obj = Instantiate(inkPrefab, parentTransform);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        Image img = obj.GetComponent<Image>();
        Image[] children = obj.GetComponentsInChildren<Image>();
        Image img2 = (children.Length > 1) ? children[1] : null;

        if (deathEffectSprites.Length > 0)
        {
            Sprite selectedSprite = deathEffectSprites[Random.Range(0, deathEffectSprites.Length)];
            img.sprite = selectedSprite;
            if (img2 != null) img2.sprite = selectedSprite;
        }

        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
        StartCoroutine(GrowRoutine(rt, scale));
    }

    IEnumerator GrowRoutine(RectTransform target, float scale)
    {
        float timer = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * Random.Range(scale * 0.8f, scale * 1.2f);

        while (timer < growthDuration)
        {
            timer += Time.deltaTime;
            float t = timer / growthDuration;
            float curve = 1f - Mathf.Pow(1f - t, 4f);
            if (target != null) target.localScale = Vector3.Lerp(startScale, endScale, curve);
            yield return null;
        }

        if (target != null) target.localScale = endScale;
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int rand = Random.Range(i, list.Count);
            list[i] = list[rand];
            list[rand] = temp;
        }
    }
}
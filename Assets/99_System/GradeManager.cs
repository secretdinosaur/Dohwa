using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

public class GradeManager : MonoBehaviour
{
    public static GradeManager Instance;

    public LocalizedString[] LocalizedGrades;
    public LocalizedString[] LocalizedGradeAbilityDescriptions;
    public LocalizedString MaxGradeText;
    public int CurrentGradeIndex = 0;
    float currentExp = 0;
    public int maxGrade = 3;
    public GameObject flowerUIObject;
    public RectTransform flowerRect;
    public UnityEngine.UI.Image backgroundImage;
    public GameObject GradeUpgradeAlertText;
    [Header("Volume Settings")]
    public Volume blurVolume;

    public UIFader gradePanel;

    [Header("Camera Animation Settings")]
    public Camera mainCamera;
    public float cameraForwardXOffset = 1.0f;

    [Header("Flower Animation Settings")]
    public float flowerUpTargetY = 400f;
    private Color originalBgColor;
    private Coroutine pulseCoroutine;
    private Coroutine panelAnimCoroutine;

    public GameObject[] petalImages = new GameObject[5];
    private bool isUpgradePending = false;
    private Coroutine animationCoroutine;
    private Coroutine petalAnimationCoroutine;

    private float originalCameraX;
    public bool IsPlayingUpgradeAnim;
    public GameObject UpperBar;

    public float CurrentExp
    {
        get { return currentExp; }
        set
        {
            if (CurrentGradeIndex >= maxGrade)
            {
                currentExp = ExpRequirementTable[CurrentGradeIndex];
                if (UIManager.Instance.ExpBarFill.material != null)
                    UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", 1f);
                UIManager.Instance.ExpText.text = "MAX";
                return;
            }

            currentExp = value;
            float expRequirement = ExpRequirementTable[CurrentGradeIndex];
            float expRatio = currentExp / expRequirement;

            if (UIManager.Instance.ExpBarFill.material != null)
                UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", expRatio);

            UIManager.Instance.ExpText.text = $"{(int)currentExp} / {expRequirement}";

            UpdatePetals(expRatio);

            if (currentExp >= expRequirement)
            {
                currentExp -= expRequirement;
                CurrentGradeIndex++;

                isUpgradePending = true;
                GradeUpgradeAlertText.SetActive(true);

                if (CurrentGradeIndex >= maxGrade)
                {
                    currentExp = ExpRequirementTable[CurrentGradeIndex];
                    if (UIManager.Instance.ExpBarFill.material != null)
                        UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", 1f);

                    UIManager.Instance.NextGradeText.text = MaxGradeText.GetLocalizedString();
                    UIManager.Instance.GradeAbilityText.text = "";
                    UIManager.Instance.ExpText.text = "";
                }
                else
                {
                    float nextExpRequirement = ExpRequirementTable[CurrentGradeIndex];
                    if (UIManager.Instance.ExpBarFill.material != null)
                        UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", currentExp / nextExpRequirement);

                    UIManager.Instance.NextGradeText.text = CurrentGradeIndex < LocalizedGrades.Length - 1 ? LocalizedGrades[CurrentGradeIndex + 1].GetLocalizedString() : "Max Grade";
                    UIManager.Instance.GradeAbilityText.text = CurrentGradeIndex < LocalizedGrades.Length - 1 ? LocalizedGradeAbilityDescriptions[CurrentGradeIndex + 1].GetLocalizedString() : "";
                    UIManager.Instance.ExpText.text = $"{(int)currentExp} / {nextExpRequirement}";
                }
            }
        }
    }

    public void InitializeGradeAbility()
    {
        if (PeachTree.Instance != null)
            PeachTree.Instance.SetInitialStage(CurrentGradeIndex);

        float initialRatio = currentExp / ExpRequirementTable[CurrentGradeIndex];
        UpdatePetals(initialRatio);

        if (CurrentGradeIndex > 0) Apply_Ability_SecondClass();
        if (CurrentGradeIndex > 1) Apply_Ability_FirstClass();
        if (CurrentGradeIndex > 2) Apply_Ability_Peak();
        if (CurrentGradeIndex > 3) Apply_Ability_SuperPeak();
        if (CurrentGradeIndex > 4) Apply_Ability_TransformationRealm();
        if (CurrentGradeIndex > 5) Apply_Ability_ProfoundRealm();

        if (CurrentGradeIndex >= maxGrade)
        {
            if (UIManager.Instance.ExpBarFill.material != null)
                UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", 1f);

            UIManager.Instance.ExpText.text = "";
            UIManager.Instance.NextGradeText.text = MaxGradeText.GetLocalizedString();
            UIManager.Instance.GradeAbilityText.text = "";
        }
        else
        {
            if (UIManager.Instance.ExpBarFill.material != null)
                UIManager.Instance.ExpBarFill.material.SetFloat("_FillAmount", initialRatio);

            UIManager.Instance.ExpText.text = $"{(int)currentExp} / {ExpRequirementTable[CurrentGradeIndex]}";
            UIManager.Instance.NextGradeText.text = CurrentGradeIndex < LocalizedGrades.Length - 1 ? LocalizedGrades[CurrentGradeIndex + 1].GetLocalizedString() : "Max Grade";
            UIManager.Instance.GradeAbilityText.text = CurrentGradeIndex < LocalizedGrades.Length - 1 ? LocalizedGradeAbilityDescriptions[CurrentGradeIndex + 1].GetLocalizedString() : "";
        }
    }

    private void Awake() => Instance = this;

    private void Start()
    {
        originalBgColor = backgroundImage.color;

        if (blurVolume != null) blurVolume.weight = 1f;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) originalCameraX = mainCamera.transform.position.x;
        StartFlowerPulse();
        gradePanel.DisableUI(0f);
        ExpGainLog = new float[6];

        SetUIInteraction(true);
    }

    // ==============================================================
    // 💡 투명한 전체 화면 방어막(Blocker)을 생성하여 클릭을 원천 차단하는 로직
    // (인벤토리나 다른 UI 코드와 절대 충돌하지 않음)
    // ==============================================================
    private GameObject globalUIBlocker;

    private void SetUIInteraction(bool enable)
    {
        bool isBlocked = !enable; // enable이 false면 차단(Block) 상태
        if (isBlocked)
        {
            if (globalUIBlocker == null)
            {
                globalUIBlocker = new GameObject("Global_UI_InputBlocker");
                Canvas canvas = globalUIBlocker.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767; // 모든 UI(인벤토리, 상단바 등)보다 최상위에 덮음

                globalUIBlocker.AddComponent<GraphicRaycaster>(); // 핵심: 여기서 레이캐스트(클릭)를 삼켜버림

                Image img = globalUIBlocker.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0); // 눈에 보이지 않도록 완전 투명
            }
            globalUIBlocker.SetActive(true); // 차단 활성화
        }
        else
        {
            if (globalUIBlocker != null)
            {
                globalUIBlocker.SetActive(false); // 차단 해제 (정상 조작 가능)
            }
        }
    }

    private void OnDestroy()
    {
        if (globalUIBlocker != null) Destroy(globalUIBlocker);
    }
    // ==============================================================

    private void UpdatePetals(float ratio)
    {
        if (petalImages == null || petalImages.Length == 0) return;
        int activeCount = Mathf.FloorToInt(ratio / 0.2f);

        for (int i = 0; i < petalImages.Length; i++)
        {
            if (petalImages[i] != null)
            {
                bool shouldBeActive = i < activeCount;
                petalImages[i].SetActive(shouldBeActive);
                Image img = petalImages[i].GetComponent<Image>();
                if (img != null) img.fillAmount = 1f;
            }
        }
    }

    public void OpenGradePanel()
    {
        StartCoroutine(ManualOpenAnimationProcess());
    }

    public void CloseGradePanel()
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        if (petalAnimationCoroutine != null) StopCoroutine(petalAnimationCoroutine);

        if (panelAnimCoroutine != null) StopCoroutine(panelAnimCoroutine);
        panelAnimCoroutine = StartCoroutine(ClosePanelAnimationProcess());
    }

    public void OnFlowerUIOpened()
    {
        if (isUpgradePending)
        {
            if (petalAnimationCoroutine != null) StopCoroutine(petalAnimationCoroutine);
            StartCoroutine(SequenceUpgradeProcess());
        }
        else
        {
            float expRatio = currentExp / ExpRequirementTable[CurrentGradeIndex];
            if (petalAnimationCoroutine != null) StopCoroutine(petalAnimationCoroutine);
            petalAnimationCoroutine = StartCoroutine(AnimatePetalsFillAndEnable(expRatio));
        }
    }

    private IEnumerator AnimatePetalsFillAndEnable(float targetRatio)
    {
        SetUIInteraction(false);

        yield return StartCoroutine(AnimatePetalsFill(targetRatio));
        SetUIInteraction(true);

    }

    private IEnumerator ManualOpenAnimationProcess()
    {
        SetUIInteraction(false);

        StopFlowerPulse();
        flowerUIObject.SetActive(true);
        flowerUIObject.transform.localScale = Vector3.zero;

        float duration = 0.5f;
        float time = 0f;

        float startAlpha = 185 / 255f;
        float startCamX = mainCamera != null ? mainCamera.transform.position.x : originalCameraX;
        float targetCamX = originalCameraX + cameraForwardXOffset;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easeOutT = EaseOut(t);

            Color c = backgroundImage.color;
            c.a = Mathf.Lerp(startAlpha, 0f, easeOutT);
            backgroundImage.color = c;

            if (blurVolume != null) blurVolume.weight = Mathf.Lerp(1f, 0f, easeOutT * 1.5f);

            if (mainCamera != null)
            {
                Vector3 camPos = mainCamera.transform.position;
                camPos.x = Mathf.Lerp(startCamX, targetCamX, easeOutT);
                mainCamera.transform.position = camPos;
            }
            yield return null;
        }

        if (backgroundImage != null) backgroundImage.color = new Color(originalBgColor.r, originalBgColor.g, originalBgColor.b, 0f);
        if (blurVolume != null) blurVolume.weight = 0f;

        gradePanel.EnableUI(0.2f);
        SetUIInteraction(true);
    }

    private IEnumerator ClosePanelAnimationProcess()
    {
        StopFlowerPulse();
        flowerUIObject.SetActive(true);
        flowerUIObject.transform.localScale = Vector3.one;
        flowerRect.localScale = Vector3.zero;

        float durationFast = 0.5f;
        float durationScale = 0.5f;
        float time = 0f;

        float startAlpha = backgroundImage.color.a;
        float startWeight = blurVolume != null ? blurVolume.weight : 0f;
        float startCamX = mainCamera != null ? mainCamera.transform.position.x : originalCameraX + cameraForwardXOffset;

        gradePanel.DisableUI(0.5f);

        while (time < durationScale)
        {
            time += Time.unscaledDeltaTime;
            float tFast = Mathf.Clamp01(time / durationFast);
            float easeOutTFast = EaseOut(tFast);

            Color c = backgroundImage.color;
            c.a = Mathf.Lerp(startAlpha, originalBgColor.a, easeOutTFast);
            backgroundImage.color = c;

            if (blurVolume != null) blurVolume.weight = Mathf.Lerp(startWeight, 1f, easeOutTFast);

            float tScale = Mathf.Clamp01(time / durationScale);
            float easeOutTScale = EaseOut(tScale);
            flowerRect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.8f, easeOutTScale);

            if (mainCamera != null)
            {
                Vector3 camPos = mainCamera.transform.position;
                camPos.x = Mathf.Lerp(startCamX, originalCameraX, easeOutTScale);
                mainCamera.transform.position = camPos;
            }
            yield return null;
        }

        if (backgroundImage != null) backgroundImage.color = originalBgColor;
        if (blurVolume != null) blurVolume.weight = 1f;
        flowerRect.localScale = Vector3.one * 1.8f;

        StartFlowerPulse();
    }

    private void StartFlowerPulse()
    {
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(FlowerPulseProcess());
    }

    private void StopFlowerPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }

    private IEnumerator FlowerPulseProcess()
    {
        float pulseSpeed = 3f;
        float minScale = 1.8f;
        float maxScale = 1.85f;
        float phaseOffset = -Mathf.PI / 2f;
        float startTime = Time.unscaledTime;

        while (true)
        {
            float t = Time.unscaledTime - startTime;
            float lerpParam = (Mathf.Sin(t * pulseSpeed + phaseOffset) + 1f) / 2f;
            float scale = Mathf.Lerp(minScale, maxScale, lerpParam);
            flowerRect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }

    private IEnumerator SequenceUpgradeProcess()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUISound(10);
        IsPlayingUpgradeAnim = true;

        SetUIInteraction(false); // 💡 업그레이드 전체 연출 중 차단막 활성화

        yield return StartCoroutine(AnimatePetalsFill(1.0f));

        yield return new WaitForSecondsRealtime(0.2f);
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(UpgradeAnimationProcess());
    }

    private IEnumerator AnimatePetalsFill(float targetRatio)
    {
        if (petalImages == null || petalImages.Length == 0) yield break;
        float duration = 0.5f;
        float time = 0f;
        int activeCount = Mathf.FloorToInt(targetRatio / 0.2f);

        for (int i = 0; i < petalImages.Length; i++)
        {
            if (petalImages[i] != null)
            {
                Image img = petalImages[i].GetComponent<Image>();
                bool shouldBeActive = i < activeCount;
                petalImages[i].SetActive(shouldBeActive);
                if (img != null && shouldBeActive) img.fillAmount = 0f;
            }
        }

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easeOutT = EaseOut(t);

            for (int i = 0; i < activeCount; i++)
            {
                if (petalImages[i] != null)
                {
                    Image img = petalImages[i].GetComponent<Image>();
                    if (img != null) img.fillAmount = easeOutT;
                }
            }
            yield return null;
        }

        for (int i = 0; i < activeCount; i++)
        {
            if (petalImages[i] != null)
            {
                Image img = petalImages[i].GetComponent<Image>();
                if (img != null) img.fillAmount = 1f;
            }
        }
    }

    private IEnumerator UpgradeAnimationProcess()
    {
        isUpgradePending = false;
        GradeUpgradeAlertText.SetActive(false);

        float duration = 2.0f;
        float time = 0f;
        StopFlowerPulse();

        Vector2 startPos = flowerRect.anchoredPosition;
        Vector3 startScale = flowerRect.localScale;
        float startAlpha = backgroundImage.color.a;
        float startWeight = blurVolume != null ? blurVolume.weight : 0f;

        float startCamX = mainCamera != null ? mainCamera.transform.position.x : originalCameraX;
        float targetCamX = originalCameraX + cameraForwardXOffset;

        bool isRightSide = Random.value > 0.5f;
        float angleDeg = isRightSide ? Random.Range(-60f, 60f) : Random.Range(120f, 240f);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        Vector2 sideStep = new Vector2(-direction.y, direction.x) * 200f;

        Vector2 p0 = startPos;
        Vector2 p1 = startPos + (direction * 400f) + sideStep;
        Vector2 p2 = startPos + (direction * 400f) - sideStep;
        Vector2 p3 = startPos + new Vector2(0f, flowerUpTargetY);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easeOutT = EaseOut(t);

            flowerUIObject.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easeOutT);
            flowerRect.anchoredPosition = CalculateBezier(p0, p1, p2, p3, easeOutT);

            Color c = backgroundImage.color;
            c.a = Mathf.Lerp(startAlpha, 0f, easeOutT);
            backgroundImage.color = c;

            if (blurVolume != null) blurVolume.weight = Mathf.Lerp(startWeight, 0f, easeOutT * 1.5f);

            if (mainCamera != null)
            {
                Vector3 camPos = mainCamera.transform.position;
                camPos.x = Mathf.Lerp(startCamX, targetCamX, easeOutT);
                mainCamera.transform.position = camPos;
            }
            yield return new WaitForSecondsRealtime(0.016f);
        }

        flowerUIObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.color = new Color(originalBgColor.r, originalBgColor.g, originalBgColor.b, 0f);
        if (blurVolume != null) blurVolume.weight = 0f;

        if (PeachTree.Instance != null)
            PeachTree.Instance.GrowToStage(CurrentGradeIndex);

        flowerRect.anchoredPosition = startPos;
        UpdatePetals(0f);
        ApplyGradeAbilities(CurrentGradeIndex);

        gradePanel.EnableUI(0.5f);
        IsPlayingUpgradeAnim = false;

        SetUIInteraction(true); // 💡 메인 연출이 끝났으므로 클릭 차단막 파괴(해제)

        yield return new WaitForSecondsRealtime(5f);
    }

    private void ApplyGradeAbilities(int gradeIndex)
    {
        switch (gradeIndex)
        {
            case 1: Apply_Ability_SecondClass(); break;
            case 2: Apply_Ability_FirstClass(); break;
            case 3: Apply_Ability_Peak(); break;
            case 4: Apply_Ability_SuperPeak(); break;
            case 5: Apply_Ability_TransformationRealm(); break;
            case 6: Apply_Ability_ProfoundRealm(); break;
        }
    }

    private Vector2 CalculateBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    private float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

    int[] ExpRequirementTable = { 100, 300, 500, 1000, 3000, 5000, 99999 };

    public enum ExpGainBehavior
    {
        UseSKill, UseAdvancedSkill, KillEnemy, KillBoss, EarnedInsightPoint, CompletelyAcquiredSkillBook,
    }

    Dictionary<ExpGainBehavior, float> ExpGainTable = new Dictionary<ExpGainBehavior, float>
    {
        { ExpGainBehavior.UseSKill, 0.1f },
        { ExpGainBehavior.UseAdvancedSkill, 0.3f },
        { ExpGainBehavior.KillEnemy, 3 },
        { ExpGainBehavior.KillBoss, 50 },
        { ExpGainBehavior.EarnedInsightPoint, 0.5f },
        { ExpGainBehavior.CompletelyAcquiredSkillBook, 30 },
    };

    public void GetExp(ExpGainBehavior behavior)
    {
        CurrentExp += ExpGainTable[behavior];
        ExpGainLog[(int)behavior] += ExpGainTable[behavior];
    }

    public float[] ExpGainLog = new float[6];

    public void Apply_Ability_SecondClass()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 5f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 10f);
    }

    public void Apply_Ability_FirstClass()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 8f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 15f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.MpConsumptionReduction, 0.05f);
    }

    public void Apply_Ability_Peak()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 15f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 30f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.MpConsumptionReduction, 0.05f);
    }

    public void Apply_Ability_SuperPeak()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 30f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 50f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.MpConsumptionReduction, 0.05f);
    }

    public void Apply_Ability_TransformationRealm()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 50f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 80f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.MpConsumptionReduction, 0.05f);
    }

    public void Apply_Ability_ProfoundRealm()
    {
        StatManager.Instance.ApplySingleStat(StatManager.StatType.ExternalArt, 80f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.InnerArt, 100f);
        StatManager.Instance.ApplySingleStat(StatManager.StatType.MpConsumptionReduction, 0.05f);
    }
}
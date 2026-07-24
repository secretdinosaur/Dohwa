using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

[Serializable]
public class BossSkillProfile
{
    public SkillBase skill;
    public float minDistance = 0f;
    public float maxDistance = 10f;
    public float baseWeight = 10f;
    public float weightIncreaseAmount = 2f;
    public float nextAttackDelay = 2f;
    public float cooldown = 5f;

    [Header("Phase Setting")]
    public int minPhase = 1;
    public int maxPhase = 99;

    [Header("Combo Setting")]
    public SkillBase[] comboSkills;
    public bool isInstantCombo = false;
    
    [HideInInspector] public float lastUsedTime = -9999f;
    [HideInInspector] public float currentWeight;
}

[RequireComponent(typeof(EnemyCore))]
public class BossSkillSelector : MonoBehaviour
{
    public List<BossSkillProfile> skillProfiles = new List<BossSkillProfile>();

    private BossSkillProfile lastUsedProfile;
    private SkillBase pendingComboSkill;
    private EnemyCore core;

    [HideInInspector] public bool isNextSkillCombo = false;
    [HideInInspector] public bool isInstantComboTriggered = false;

    [HideInInspector] public int currentPhase = 1; // AI에서 업데이트
    public bool HasPendingCombo => pendingComboSkill != null;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        foreach (var profile in skillProfiles)
        {
            profile.currentWeight = profile.baseWeight;
        }
    }

    private void OnEnable()
    {
        if (core != null) core.OnPlayerHit += NotifySkillHit;
    }

    private void OnDisable()
    {
        if (core != null) core.OnPlayerHit -= NotifySkillHit;
    }

    public void SetPhase(int phase)
    {
        currentPhase = phase;
    }

    public SkillBase GetNextSkill(float distanceToPlayer)
    {
        if (pendingComboSkill != null)
        {
            SkillBase combo = pendingComboSkill;
            pendingComboSkill = null;
            isNextSkillCombo = true;

            BossSkillProfile comboProfile = skillProfiles.Find(p => p.skill == combo);
            if (comboProfile != null)
            {
                lastUsedProfile = comboProfile;
                comboProfile.lastUsedTime = Time.time;
            }

            return combo;
        }

        isNextSkillCombo = false;
        List<BossSkillProfile> validProfiles = new List<BossSkillProfile>();
        float totalWeight = 0f;

        foreach (var profile in skillProfiles)
        {
            if (currentPhase < profile.minPhase || currentPhase > profile.maxPhase) continue;
            if (profile.baseWeight <= 0f && profile.weightIncreaseAmount <= 0f) continue;
            if (Time.time - profile.lastUsedTime < profile.cooldown) continue;
            if (profile == lastUsedProfile) continue;
            if (distanceToPlayer < profile.minDistance || distanceToPlayer > profile.maxDistance) continue;

            validProfiles.Add(profile);
            totalWeight += profile.currentWeight;
        }

        if (validProfiles.Count == 0) return null;

        float randomVal = UnityEngine.Random.Range(0f, totalWeight);
        float currentSum = 0f;
        BossSkillProfile selectedProfile = validProfiles[0];

        foreach (var profile in validProfiles)
        {
            currentSum += profile.currentWeight;
            if (randomVal <= currentSum)
            {
                selectedProfile = profile;
                break;
            }
        }

        foreach (var profile in skillProfiles)
        {
            if (profile.baseWeight <= 0f && profile.weightIncreaseAmount <= 0f) continue;

            if (profile == selectedProfile)
            {
                profile.currentWeight = profile.baseWeight;
                profile.lastUsedTime = Time.time;
            }
            else
            {
                profile.currentWeight += profile.weightIncreaseAmount;
            }
        }

        lastUsedProfile = selectedProfile;
        return selectedProfile.skill;
    }

    private void NotifySkillHit(SkillBase hitSkill)
    {
        var profile = skillProfiles.Find(p => p.skill == hitSkill);

        if (profile != null && profile.comboSkills != null && profile.comboSkills.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, profile.comboSkills.Length);
            SkillBase selectedComboSkill = profile.comboSkills[randomIndex];

            if (selectedComboSkill != null)
            {
                pendingComboSkill = selectedComboSkill;

                if (profile.isInstantCombo)
                {
                    StartCoroutine(InstantComboTriggerOn());
                }
            }
        }
    }

    IEnumerator InstantComboTriggerOn()
    {
        yield return null;
        isInstantComboTriggered = true;
    }

    public float GetLastSkillDelay()
    {
        return lastUsedProfile != null ? lastUsedProfile.nextAttackDelay : 1f;
    }

    public float GetOptimalDistance(SkillBase skill)
    {
        if (skill == null) return 3f;
        var profile = skillProfiles.Find(p => p.skill == skill);
        if (profile != null)
        {
            return UnityEngine.Random.Range(profile.minDistance, profile.maxDistance);
        }
        return 3f;
    }
}
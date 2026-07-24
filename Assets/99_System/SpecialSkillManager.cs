using System.Collections.Generic;
using UnityEngine;

public class SpecialSkillManager : MonoBehaviour
{
    public static SpecialSkillManager Instance { get; private set; }

    // 기존 long 타입 키 대신 (EntityId, EntityId) 튜플을 키로 사용
    private Dictionary<(EntityId, EntityId), float> lastTriggeredTime = new Dictionary<(EntityId, EntityId), float>();
    private Dictionary<(EntityId, EntityId), int> usedCounts = new Dictionary<(EntityId, EntityId), int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Trigger(Transform caster, SkillBase.TriggerType triggerType, float triggerValue = 0f)
    {
        if (caster == null) return;

        List<SkillBase> skills = new List<SkillBase>();

        if (caster.TryGetComponent<Player>(out var player))
        {
            if (player.PlayerAttack != null && player.PlayerAttack.acquiredSkills != null)
                skills.AddRange(player.PlayerAttack.acquiredSkills);
        }
        if (caster.TryGetComponent<EnemyCore>(out var enemy))
        {
            /*if (enemy is MeleeEnemy me && me.skillSet != null)
                skills.AddRange(me.skillSet);
            else if (enemy.GetType().GetField("skillSet") != null)
            {
                var fi = enemy.GetType().GetField("skillSet");
                if (fi != null)
                {
                    var arr = fi.GetValue(enemy) as SkillBase[];
                    if (arr != null) skills.AddRange(arr);
                }
            }*/
        }

        // 반복문 외부에서 Caster의 EntityId를 한 번만 호출
        EntityId casterId = caster.GetEntityId();

        foreach (var skill in skills)
        {
            if (skill == null) continue;
            if (!skill.isSpecialSkill) continue;
            if (skill.triggerType != triggerType) continue;

            if (skill.triggerChance < 1f && Random.value > skill.triggerChance) continue;

            EntityId skillId = skill.GetEntityId();
            var key = (casterId, skillId);

            if (lastTriggeredTime.TryGetValue(key, out float t))
            {
                if (Time.time < t + skill.triggerCooldown) continue;
            }

            if (skill.triggerMaxUsesPerCaster > 0)
            {
                int used = usedCounts.TryGetValue(key, out int c) ? c : 0;
                if (used >= skill.triggerMaxUsesPerCaster) continue;
                usedCounts[key] = used + 1;
            }

            lastTriggeredTime[key] = Time.time;

            Player.Instance.PlayerAttack.Attack(skill);
        }
    }

    public void ResetCasterData(Transform caster)
    {
        if (caster == null) return;

        // GetInstanceID() 대신 GetEntityId() 사용
        EntityId cid = caster.GetEntityId();
        var keysToRemove = new List<(EntityId, EntityId)>();

        foreach (var k in lastTriggeredTime.Keys)
        {
            // 튜플의 첫 번째 값(CasterId)이 대상과 일치하는지 확인
            if (k.Item1.Equals(cid)) keysToRemove.Add(k);
        }

        foreach (var k in keysToRemove)
        {
            lastTriggeredTime.Remove(k);
            usedCounts.Remove(k);
        }
    }
}
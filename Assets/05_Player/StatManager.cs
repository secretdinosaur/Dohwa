using UnityEngine;
using System;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance;

    public enum StatType
    {
        Attack,
        MaxHp,
        MaxMp,
        Defense,
        MoveSpeed,
        HpRecovery,
        MpRecovery,
    }
    public float ParringTime = 0.2f;
    public float dashCooldown = 1f;

    public int earnedStatPoint = 0;
    private int statPoint = 0;
    public int StatPoint
    {
        get { return statPoint; }
        set
        {
            statPoint = Mathf.Max(0, value);
            UIManager.Instance.InsightPointText.text = statPoint.ToString();
        }
    }

    [SerializeField] private float attackPower = 100f;
    public float AttackPower
    {
        get => attackPower;
        set { attackPower = value; UIManager.Instance.AttackText.text = ((int)attackPower).ToString();  }
    }

    [SerializeField] private float maxHp = 100;
    public float MaxHp
    {
        get => maxHp;
        set { maxHp = value;}
    }

    [SerializeField] private float maxMp = 100f;
    public float MaxMp
    {
        get => maxMp;
        set { maxMp = value; }
    }

    [SerializeField] private float armor = 0f;
    public float Armor
    {
        get => armor;
        set { armor = value; UIManager.Instance.ArmorText.text = ((int)armor).ToString(); }
    }

    public float MoveSpeed = 5f;
    public float HpRecoveryAmount = 10f;
    public float MpRecoveryAmount = 10f;
    public float ParryMpRecovery = 20f;
    public float GuardSuccessCostMpMult = 1f;

    private void Awake()
    {
        Instance = this;

        armor = 0f;
        attackPower = 280;
        MaxHp = 100;
        MaxMp = 100;


        Armor = armor;
        AttackPower = attackPower;
        MaxHp = maxHp;
        MaxMp = maxMp;

    }


    private void OnValidate()
    {
        if(UIManager.Instance == null) return;
        Armor = armor;
        AttackPower = attackPower;
        MaxHp = maxHp;
        MaxMp = maxMp;
    }
    public void GetStatPoint(int amount)
    {
        earnedStatPoint += amount;
        StatPoint += amount;
        for(int i = 0; i < amount; i++)
            GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.EarnedInsightPoint);
        StartCoroutine(UIManager.Instance.ShowStatPanel(amount));
    }

    public void ApplySingleStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHp:
                MaxHp += value;
                if (Player.Instance != null) Player.Instance.HP = Mathf.Min(Player.Instance.HP, MaxHp);
                break;
            case StatType.MaxMp:
                MaxMp += value;
                if (Player.Instance != null) Player.Instance.MP = Mathf.Min(Player.Instance.MP, MaxMp);
                break;
            case StatType.Attack:
                AttackPower += value;
                break;
            case StatType.Defense:
                Armor += value;
                break;
            case StatType.HpRecovery:
                HpRecoveryAmount += value;
                break;
            case StatType.MpRecovery:
                MpRecoveryAmount += value;
                break;
            case StatType.MoveSpeed:
                MoveSpeed += value;
                break;
        }
    }
    public void SetStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHp: MaxHp = value; break;
            case StatType.MaxMp: MaxMp = value; break;
            case StatType.Attack: AttackPower = value; break;
            case StatType.Defense: Armor = value; break;
            case StatType.MoveSpeed: MoveSpeed = value; break;
            case StatType.HpRecovery: HpRecoveryAmount = value; break;
            case StatType.MpRecovery: MpRecoveryAmount = value; break;
        }
    }
}
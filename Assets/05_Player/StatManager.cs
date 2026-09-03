using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

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
        ExternalArt,
        InnerArt,
        VitalStrikeRate,
        VitalStrikeDamage,
        PoisonResistance,
        BleedResistance,
        FireResistance,
        LightningResistance,
        Orthodox,
        Unorthodox,
        Demonic,
        MpConsumptionReduction
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
    #region Stat Variables and Texts
    [SerializeField] private float attackPower = 90f;
    public float AttackPower
    {
        get => attackPower;
        set { attackPower = value; UIManager.Instance.AttackText.text = ((int)attackPower).ToString(); }
    }

    [SerializeField] private float maxHp = 100;
    public float MaxHp
    {
        get => maxHp;

        set { maxHp = value; UIManager.Instance.HpText.text = ((int)MaxHp).ToString(); }
    }

    [SerializeField] private float maxMp = 100f;
    public float MaxMp
    {
        get => maxMp;
        set { maxMp = value; UIManager.Instance.MpText.text = ((int)MaxMp).ToString(); }
    }

    [SerializeField] private float armor = 0f;
    public float Armor
    {
        get => armor;
        set { armor = value; UIManager.Instance.ArmorText.text = ((int)armor).ToString(); }
    }

    [SerializeField] private float externalArt = 0f;
    public float ExternalArt
    {
        get => externalArt;
        set { externalArt = value; UIManager.Instance.ExternalArtText.text = ((int)externalArt).ToString(); }
    }

    [SerializeField] private float innerArt = 0f;
    public float InnerArt
    {
        get => innerArt;
        set { innerArt = value; UIManager.Instance.InnerArtText.text = ((int)innerArt).ToString(); }
    }

    [SerializeField] private float vitalStrikeRate = 0f;
    public float VitalStrikeRate
    {
        get => vitalStrikeRate;
        set { vitalStrikeRate = value; UIManager.Instance.VitalStrikeRateText.text = ((int)(vitalStrikeRate * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float vitalStrikeDamage = 1.1f;
    public float VitalStrikeDamage
    {
        get => vitalStrikeDamage;
        set { vitalStrikeDamage = value; UIManager.Instance.VitalStrikeDamageText.text = ((int)(vitalStrikeDamage * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float hpRecoveryAmount = 1f;
    public float HpRecoveryAmount
    {
        get => hpRecoveryAmount;
        set { hpRecoveryAmount = value; UIManager.Instance.HpRecoveryAmountText.text = ((int)(hpRecoveryAmount * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float mpRecoveryAmount = 10f;
    public float MpRecoveryAmount
    {
        get => mpRecoveryAmount;
        set { mpRecoveryAmount = value; UIManager.Instance.MpRecoveryAmountText.text = ((int)mpRecoveryAmount).ToString(); }
    }

    [SerializeField] private float moveSpeed = 5f;
    public float MoveSpeed
    {
        get => moveSpeed;
        set { moveSpeed = value; UIManager.Instance.MoveSpeedText.text = ((int)moveSpeed).ToString(); }
    }

    [SerializeField] private float poisonResistance = 0f;
    public float PoisonResistance
    {
        get => poisonResistance;
        set { poisonResistance = value; UIManager.Instance.PoisonResistanceText.text = ((int)(poisonResistance * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float bleedResistance = 0f;
    public float BleedResistance
    {
        get => bleedResistance;
        set { bleedResistance = value; UIManager.Instance.BleedResistanceText.text = ((int)(bleedResistance * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float fireResistance = 0f;
    public float FireResistance
    {
        get => fireResistance;
        set { fireResistance = value; UIManager.Instance.FireResistanceText.text = ((int)(fireResistance * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float lightningResistance = 0f;
    public float LightningResistance
    {
        get => lightningResistance;
        set { lightningResistance = value; UIManager.Instance.LightningResistanceText.text = ((int)(lightningResistance * 100f)).ToString() + "%"; }
    }

    [SerializeField] private float orthodox = 0f;
    public float Orthodox
    {
        get => orthodox;
        set { orthodox = value; UIManager.Instance.OrthodoxText.text = ((int)orthodox).ToString(); }
    }

    [SerializeField] private float unorthodox = 0f;
    public float Unorthodox
    {
        get => unorthodox;
        set { unorthodox = value; UIManager.Instance.UnorthodoxText.text = ((int)unorthodox).ToString(); }
    }

    [SerializeField] private float demonic = 0f;
    public float Demonic
    {
        get => demonic;
        set { demonic = value; /*UIManager.Instance.DemonicText.text = ((int)demonic).ToString();*/ }
    }

    [SerializeField] private float mpConsumptionReduction = 0f;
    public float MpConsumptionReduction
    {
        get => mpConsumptionReduction;
        set { mpConsumptionReduction = value; UIManager.Instance.MpConsumptionReductionText.text = ((int)(mpConsumptionReduction * 100f)).ToString() + "%"; }
    }

    public float ParryMpRecovery = 30f;
    public float GuardSuccessCostMpMult = 1f;


    // 0 = MaxHp, 1 = Defense, 2 = Attack, 3 = MaxMp
    private float[] ExternalArtRatio;
    private float[] InnerArtRatio;

    #endregion

    private void Awake()
    {
        Instance = this;

        Armor = armor;
        AttackPower = attackPower;
        MaxHp = maxHp;
        MaxMp = maxMp;

        ExternalArt = externalArt;
        InnerArt = innerArt;
        VitalStrikeRate = vitalStrikeRate;
        VitalStrikeDamage = vitalStrikeDamage;
        MoveSpeed = moveSpeed;

        PoisonResistance = poisonResistance;
        BleedResistance = bleedResistance;
        FireResistance = fireResistance;
        LightningResistance = lightningResistance;

        Orthodox = orthodox;
        Unorthodox = unorthodox;
        Demonic = demonic;

        HpRecoveryAmount = hpRecoveryAmount;
        MpRecoveryAmount = mpRecoveryAmount;
        MpConsumptionReduction = mpConsumptionReduction;

        ExternalArtRatio = new float[] { 0.9f, 0.2f, 0.14f, 0.11f };
        InnerArtRatio = new float[] { 0.1f, 0.03f, 0.5f, 0.25f };
    }

    public bool cheatMode = false;
    private void OnValidate()
    {
        if (UIManager.Instance == null) return;

        Armor = armor;
        AttackPower = attackPower;
        MaxHp = maxHp;
        MaxMp = maxMp;

        if (cheatMode)
        {
            SetStat(StatType.ExternalArt, 99999);
            SetStat(StatType.InnerArt, 99999);
            Player.Instance.HP = float.MaxValue;
            SetStat(StatType.MpRecovery, float.MaxValue);
            MoveSpeed = 1000f;
        }

    }
    public void GetStatPoint(int amount)
    {
        earnedStatPoint += amount;
        StatPoint += amount;
        for (int i = 0; i < amount; i++)
            GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.EarnedInsightPoint);
        StartCoroutine(UIManager.Instance.ShowStatPanel(amount));
    }

    public void ApplySingleStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHp:
                MaxHp += value;
                if (Player.Instance != null)
                    Player.Instance.HP = Mathf.Min(Player.Instance.HP, MaxHp);
                break;

            case StatType.MaxMp:
                MaxMp += value;
                if (Player.Instance != null)
                    Player.Instance.MP = Mathf.Min(Player.Instance.MP, MaxMp);
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

            case StatType.ExternalArt:
                ExternalArt += value;
                ApplySingleStat(StatType.MaxHp, value * ExternalArtRatio[0]);
                ApplySingleStat(StatType.Defense, value * ExternalArtRatio[1]);
                ApplySingleStat(StatType.Attack, value * ExternalArtRatio[2]);
                ApplySingleStat(StatType.MaxMp, value * ExternalArtRatio[3]);
                break;

            case StatType.InnerArt:
                InnerArt += value;
                ApplySingleStat(StatType.MaxHp, value * InnerArtRatio[0]);
                ApplySingleStat(StatType.Defense, value * InnerArtRatio[1]);
                ApplySingleStat(StatType.Attack, value * InnerArtRatio[2]);
                ApplySingleStat(StatType.MaxMp, value * InnerArtRatio[3]);
                break;

            case StatType.VitalStrikeRate:
                VitalStrikeRate += value;
                break;

            case StatType.VitalStrikeDamage:
                VitalStrikeDamage += value;
                break;

            case StatType.PoisonResistance:
                PoisonResistance += value;
                break;

            case StatType.BleedResistance:
                BleedResistance += value;
                break;

            case StatType.FireResistance:
                FireResistance += value;
                break;

            case StatType.LightningResistance:
                LightningResistance += value;
                break;

            case StatType.Orthodox:
                Orthodox += value;
                break;

            case StatType.Unorthodox:
                Unorthodox += value;
                break;

            case StatType.Demonic:
                Demonic += value;
                break;

            case StatType.MpConsumptionReduction:
                MpConsumptionReduction += value;
                break;
        }
    }

    public void SetStat(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHp:
                MaxHp = value;
                break;

            case StatType.MaxMp:
                MaxMp = value;
                break;

            case StatType.Attack:
                AttackPower = value;
                break;

            case StatType.Defense:
                Armor = value;
                break;

            case StatType.MoveSpeed:
                MoveSpeed = value;
                break;

            case StatType.HpRecovery:
                HpRecoveryAmount = value;
                break;

            case StatType.MpRecovery:
                MpRecoveryAmount = value;
                break;

            case StatType.ExternalArt:
                ExternalArt = value;
                ApplySingleStat(StatType.MaxHp, ExternalArt * ExternalArtRatio[0]);
                ApplySingleStat(StatType.Defense, ExternalArt * ExternalArtRatio[1]);
                ApplySingleStat(StatType.Attack, ExternalArt * ExternalArtRatio[2]);
                ApplySingleStat(StatType.MaxMp, ExternalArt * ExternalArtRatio[3]);
                break;

            case StatType.InnerArt:
                InnerArt = value;
                ApplySingleStat(StatType.MaxHp, InnerArt * InnerArtRatio[0]);
                ApplySingleStat(StatType.Defense, InnerArt * InnerArtRatio[1]);
                ApplySingleStat(StatType.Attack, InnerArt * InnerArtRatio[2]);
                ApplySingleStat(StatType.MaxMp, InnerArt * InnerArtRatio[3]);
                break;

            case StatType.VitalStrikeRate:
                VitalStrikeRate = value;
                break;

            case StatType.VitalStrikeDamage:
                VitalStrikeDamage = value;
                break;

            case StatType.PoisonResistance:
                PoisonResistance = value;
                break;

            case StatType.BleedResistance:
                BleedResistance = value;
                break;

            case StatType.FireResistance:
                FireResistance = value;
                break;

            case StatType.LightningResistance:
                LightningResistance = value;
                break;

            case StatType.Orthodox:
                Orthodox = value;
                break;

            case StatType.Unorthodox:
                Unorthodox = value;
                break;

            case StatType.Demonic:
                Demonic = value;
                break;

            case StatType.MpConsumptionReduction:
                MpConsumptionReduction = value;
                break;
        }
    }
}
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
        Demonic
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
        set { vitalStrikeRate = value; UIManager.Instance.VitalStrikeRateText.text = (vitalStrikeRate * 100f).ToString("0.#") + "%"; }
    }

    [SerializeField] private float vitalStrikeDamage = 1.1f;
    public float VitalStrikeDamage
    {
        get => vitalStrikeDamage;
        set { vitalStrikeDamage = value; UIManager.Instance.VitalStrikeDamageText.text = (vitalStrikeDamage * 100f).ToString("0.#") + "%"; }
    }

    [SerializeField] private float hpRecoveryAmount = 10f;
    public float HpRecoveryAmount
    {
        get => hpRecoveryAmount;
        set { hpRecoveryAmount = value; UIManager.Instance.HpRecoveryAmountText.text = ((int)hpRecoveryAmount).ToString(); }
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
        set { poisonResistance = value; UIManager.Instance.PoisonResistanceText.text = (poisonResistance * 100f).ToString("0.#") + "%"; }
    }

    [SerializeField] private float bleedResistance = 0f;
    public float BleedResistance
    {
        get => bleedResistance;
        set { bleedResistance = value; UIManager.Instance.BleedResistanceText.text = (bleedResistance * 100f).ToString("0.#") + "%"; }
    }

    [SerializeField] private float fireResistance = 0f;
    public float FireResistance
    {
        get => fireResistance;
        set { fireResistance = value; UIManager.Instance.FireResistanceText.text = (fireResistance * 100f).ToString("0.#") + "%"; }
    }

    [SerializeField] private float lightningResistance = 0f;
    public float LightningResistance
    {
        get => lightningResistance;
        set { lightningResistance = value; UIManager.Instance.LightningResistanceText.text = (lightningResistance * 100f).ToString("0.#") + "%"; }
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
        set { demonic = value; UIManager.Instance.DemonicText.text = ((int)demonic).ToString(); }
    }

    public float ParryMpRecovery = 20f;
    public float GuardSuccessCostMpMult = 1f;

    #endregion

    private void Awake()
    {
        Instance = this;

        armor = 0f;
        attackPower = 280f;
        maxHp = 100f;
        maxMp = 100f;

        externalArt = 0f;
        innerArt = 0f;
        vitalStrikeRate = 0f;
        vitalStrikeDamage = 1.1f;
        moveSpeed = 5f;

        poisonResistance = 0f;
        bleedResistance = 0f;
        fireResistance = 0f;
        lightningResistance = 0f;

        orthodox = 0f;
        unorthodox = 0f;
        demonic = 0f;

        hpRecoveryAmount = 10f;
        mpRecoveryAmount = 10f;


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
                break;

            case StatType.InnerArt:
                InnerArt += value;
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
                ApplySingleStat(StatType.MaxHp, ExternalArt * 1.2f);
                ApplySingleStat(StatType.Defense, ExternalArt * 0.3f);
                ApplySingleStat(StatType.Attack, ExternalArt * 0.5f);
                ApplySingleStat(StatType.MaxMp, ExternalArt * 0.1f);
                break;

            case StatType.InnerArt:
                InnerArt = value;
                ApplySingleStat(StatType.MaxHp, ExternalArt * 0.2f);
                ApplySingleStat(StatType.Defense, ExternalArt * 0.1f);
                ApplySingleStat(StatType.Attack, ExternalArt * 0.5f);
                ApplySingleStat(StatType.MaxMp, ExternalArt * 0.8f);
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
        }
    }
}
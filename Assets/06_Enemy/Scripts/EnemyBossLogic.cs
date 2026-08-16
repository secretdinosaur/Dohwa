using DG.Tweening;
using UnityEngine;
using UnityEngine.Localization;

public class EnemyBossLogic : MonoBehaviour
{
    public LocalizedString BossName;
    public GameObject FightWall;
    public Checkpoint checkpoint;

    [Header("Save Settings")]
    public AudioClip[] BossMusic;
    private bool isdead;
    private EnemyCore core;
    public bool fightStarted;
    private void Awake()
    {
        core = GetComponent<EnemyCore>();
    }

    public virtual void Start()
    {
        string bossSaveKey = "BossCleared_" + gameObject.name;
        fightStarted = false;
        if (DataManager.Instance != null && DataManager.Instance.IsEventCleared(bossSaveKey))
        {
            if (FightWall != null)
            {
                FightWall.SetActive(false);
            }
            EnableCheckPoint();
            Destroy(gameObject);
            return;
        }

        core.OnDeath += EnemyDead;
        core.OnRevived += PlayerDead;
    }

    public void StartBossFight()
    {
        if (FightWall != null)
            FightWall.SetActive(true);
        fightStarted = true;
        PlayBGM();
        ShowBossStatus();
        GetComponent<EnemyAIBase>().isDetectedPlayer = true;
        GetComponent<EnemyAIBase>().isMemorizingPlayer = true;
        core.CmdForceDetectPlayer?.Invoke();
    }

    public void PlayBGM()
    {
        if (BossMusic != null && 0 < BossMusic.Length)
            SoundManager.Instance.PlaySpecialBGM(BossMusic[0]);
    }

    public void StopBGM()
    {
        SoundManager.Instance.StopSpecialBGMWithFade();
    }

    private void OnDestroy()
    {
        if (core != null)
        {
            core.OnDeath -= EnemyDead;
            core.OnRevived -= PlayerDead;
        }
    }

    public void EnemyDead()
    {
        if (DataManager.Instance != null)
        {
            string bossSaveKey = "BossCleared_" + gameObject.name;
            DataManager.Instance.SetEventCleared(bossSaveKey);
        }

        DungeonManager.Instance.BossCleared = true;

        if (FightWall != null)
            FightWall.SetActive(false);

        GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.KillBoss);
        DungeonManager.Instance.OnBossDefeated();
        EnableCheckPoint();
        StopBGM();
        isdead = true;
    }

    private void ShowBossStatus()
    {
        if (isdead) return;
        UIManager.Instance.BossStatus.gameObject.SetActive(true);
        UIManager.Instance.UpdateBossHpUI(1f);
        UIManager.Instance.UpdateBossMpUI(1f);

        if (BossName != null)
            UIManager.Instance.BossNameText.text = BossName.GetLocalizedString();
        else
            UIManager.Instance.BossNameText.text = "Null boss name";
    }

    protected virtual void PlayerDead()
    {
        if (FightWall != null)
            FightWall.SetActive(false);
        fightStarted = false;
        UIManager.Instance.BossStatus.gameObject.SetActive(false);
    }

    private void EnableCheckPoint()
    {
        if (checkpoint == null) return;

        var colliders = checkpoint.GetComponents<Collider>();
        if (colliders.Length > 1)
        {
            colliders[0].enabled = true;
            colliders[1].enabled = true;
        }

        var renderers = checkpoint.GetComponents<MeshRenderer>();
        if (renderers.Length > 0)
        {
            renderers[0].enabled = true;
        }
    }
}
using UnityEngine;

public class Boatman : NPC
{
    public EnemyCore[] OnetimeEnemies;
    private string eventSaveKey = "Boatman_Event_Cleared";

    private Animator anim;

    // 인스펙터에서 EndingSceneManager 오브젝트를 드래그하여 할당하십시오.
    public EndingSceneManager endingSceneManager;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        bool isCleared = DataManager.Instance != null && DataManager.Instance.IsEventCleared(eventSaveKey);
        isInteractable = isCleared;

        if (anim != null)
        {
            anim.SetBool("isClear", isCleared);
        }

        if (isCleared)
        {
            TalkCount = 1;
            RemoveEnemies();
        }
    }

    private void Update()
    {
        if (!isInteractable)
        {
            if (CheckAllEnemiesDead())
            {
                isInteractable = true;
            }
        }
    }

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        if (!isInteractable) return;

        if (DataManager.Instance != null && DataManager.Instance.IsEventCleared(eventSaveKey))
        {
            TalkCount = 1;
            base.Interact();
            return;
        }

        if (DataManager.Instance != null)
        {
            DataManager.Instance.SetEventCleared(eventSaveKey);
            DataManager.Instance.AllSave();
        }

        if (anim != null)
        {
            anim.SetTrigger("firstTimeClear");
            anim.SetBool("isClear", true);
        }

        base.Interact();
    }

    public void OnSelectYesToBoard()
    {
        if (DataManager.Instance != null && DataManager.Instance.IsEventCleared(eventSaveKey))
        {
            if (endingSceneManager != null)
            {
                endingSceneManager.StartCutscene();
            }
        }
    }

    private bool CheckAllEnemiesDead()
    {
        foreach (EnemyCore enemy in OnetimeEnemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead)
            {
                return false;
            }
        }
        return true;
    }

    private void RemoveEnemies()
    {
        foreach (EnemyCore enemy in OnetimeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
    }
}
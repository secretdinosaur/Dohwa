using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Tooltip("체크포인트는 순서대로 배치하세요(인덱스가 높을수록 우선).")]
    public List<Checkpoint> checkpoints = new List<Checkpoint>();
    public Checkpoint CurrentCheckpoint;
    public List<int> unlockedCheckpoints = new List<int>();

    public int currentRespawnIndex = 1;

    private int currentStage;
    private bool isFileLoading = false;

    public int CurrentStage
    {
        get { return currentStage; }
        private set
        {
            currentStage = value;
            DialogueManager.Instance.ResetDialogueIndex();
            StartCoroutine(LoadNextStageAsync());

            if (currentStage > 2)
            {
                string prevSceneName = "Stage_" + (currentStage - 2);
                if (SceneManager.GetSceneByName(prevSceneName).isLoaded)
                {
                    SceneManager.UnloadSceneAsync(prevSceneName);
                }
            }
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CurrentStage = 0;
    }

    private void Start()
    {
        StartCoroutine(InitializePlayerPosition());
    }

    IEnumerator InitializePlayerPosition()
    {

        yield return new WaitForSeconds(0.5f);
        if(Player.Instance.isCinematic) yield break;
        if (currentRespawnIndex != -1)
        {
            Vector3 respawnPoint = GetRespawnTransformByIndex(currentRespawnIndex);
            if (respawnPoint != null && Player.Instance != null)
            {
                var rb = Player.Instance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(respawnPoint);
                }
                else
                {
                    Player.Instance.transform.position = respawnPoint;
                }

            }
        }
    }

    IEnumerator LoadNextStageAsync()
    {
        if (isFileLoading) yield break;
        isFileLoading = true;

        string nextSceneName = "Stage_" + (currentStage + 1);
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            if (!SceneManager.GetSceneByName(nextSceneName).isLoaded)
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
        }

        isFileLoading = false;
    }

    public void TryEnterNextStage()
    {
        if (currentStage == 0)
        {
            CurrentStage++;
        }
        else
        {
        }
    }

    public bool BossCleared;

    public void OnBossDefeated()
    {
        Player.Instance.impulseSource.GenerateImpulse(2f);
        Time.timeScale = 0.2f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        DOVirtual.DelayedCall(3f, () =>
        {
            DOTween.To(
                () => Time.timeScale,
                x =>
                {
                    Time.timeScale = x;
                    Time.fixedDeltaTime = 0.02f * x;
                },
                1f,
                3f
            )
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
        })
        .SetUpdate(true);

        UIManager.Instance.BossStatus.SetActive(false);

        if (currentStage > 0)
        {
            CurrentStage++;
            BossCleared = true;
        }
    }

    public void TeleportToStage(int value)
    {
        if (currentStage + value < 0) return;
        StartCoroutine(TeleportRoutine(currentStage + value));
    }

    IEnumerator TeleportRoutine(int stageIndex)
    {
        string sceneName = "Stage_" + stageIndex;
        if (stageIndex == 0) sceneName = "Dungeon";
        Scene targetScene = SceneManager.GetSceneByName(sceneName);

        if (!targetScene.isLoaded)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
                targetScene = SceneManager.GetSceneByName(sceneName);
            }
            else
            {
                yield break;
            }
        }

        DungeonEntrance targetEntrance = null;
        GameObject[] rootObjects = targetScene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            targetEntrance = obj.GetComponentInChildren<DungeonEntrance>(true);
            if (targetEntrance != null) break;
        }
        if (targetEntrance != null && Player.Instance != null)
        {
            Player.Instance.PlayerMove.enabled = false;
            Player.Instance.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            while (Player.Instance.transform.position != targetEntrance.transform.position)
            {
                Player.Instance.transform.position = targetEntrance.transform.position;
                yield return null;
            }
            Player.Instance.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
            Player.Instance.PlayerMove.enabled = true;
        }
    }

    public GameObject CheckPointPanel;
    public GameObject CheckPointPanelContent;
    public GameObject CheckPointButtonPrefab;

    public void ActivateCheckpoint(Checkpoint cp)
    {
        int idx = checkpoints.IndexOf(cp);
        if (idx < 0) return;
        CurrentCheckpoint = cp;
        if (!unlockedCheckpoints.Contains(idx))
        {
            StatManager.Instance.GetStatPoint(7);
            unlockedCheckpoints.Add(idx);
            unlockedCheckpoints.Sort();
        }

        currentRespawnIndex = idx;

        if (CheckPointPanel != null)
        {
            UIManager.Instance.InteractPanel.GetComponent<CanvasGroup>().alpha = 0f;
            CheckPointPanel.SetActive(true);

            CheckPointPanel.GetComponent<CanvasGroup>().alpha = 0f;
            CheckPointPanel.GetComponent<CanvasGroup>().DOKill();
            Sequence OpenfadeSequence = DOTween.Sequence();
            OpenfadeSequence.Append(CheckPointPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));
        }
        PopulateCheckPointPanel();
    }

    public void CloseCheckpointPanel()
    {
        CheckPointPanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();

        ClosefadeSequence.Append(CheckPointPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            CheckPointPanel.SetActive(false);
            CheckPointPanel.GetComponent<CanvasGroup>().alpha = 1f;
        });
        UIManager.Instance.InteractPanel.GetComponent<CanvasGroup>().alpha = 1f;

        CurrentCheckpoint.Exit();
    }

    public Vector3 GetRespawnTransform()
    {
        if (currentRespawnIndex >= 0 && currentRespawnIndex < checkpoints.Count)
        {
            var cp = checkpoints[currentRespawnIndex];
            return cp.GetRespawnPosition();
        }
        return Vector3.zero;
    }

    public Vector3 GetRespawnTransformByIndex(int idx)
    {
        if (idx >= 0 && idx < checkpoints.Count)
        {
            var cp = checkpoints[idx];
            if(cp != null)
                return cp.GetRespawnPosition();
        }
        return Vector3.zero;
    }

    public void TeleportToCheckpoint(int idx)
    {
        StartCoroutine(Teleport(idx));
    }

    public FadePanel TeleportPanel;

    IEnumerator Teleport(int idx)
    {
        currentRespawnIndex = idx;
        Player.Instance.isInteracting = true;
        TeleportPanel.ShowPanel();
        yield return new WaitForSeconds(1f);
        Player.Instance.HP = StatManager.Instance.MaxHp;
        Player.Instance.MP = StatManager.Instance.MaxMp;
        Vector3 respawnPoint = GetRespawnTransformByIndex(idx);
        if (respawnPoint != null && Player.Instance != null)
        {
            var rb = Player.Instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.MovePosition(respawnPoint);
            }
            else
            {
                Player.Instance.transform.position = respawnPoint + Vector3.up * 3;
            }

        }
        TeleportPanel.HidePanel(3);
        Player.Instance.isInteracting = false;
        Player.Instance.reachedCheckPoint = true;
        CurrentCheckpoint.Exit();

        UIManager.Instance.InteractPanel.GetComponent<CanvasGroup>().alpha = 1f;
        yield return null;

    }

    public void PopulateCheckPointPanel()
    {
        if (CheckPointPanelContent == null || CheckPointButtonPrefab == null) return;

        for (int i = CheckPointPanelContent.transform.childCount - 1; i >= 0; i--)
        {
            var child = CheckPointPanelContent.transform.GetChild(i);
            Destroy(child.gameObject);
        }

        foreach (int idx in unlockedCheckpoints)
        {
            var cp = checkpoints[idx];
            if (cp == null) continue;

            GameObject go = Instantiate(CheckPointButtonPrefab, CheckPointPanelContent.transform);
            go.name = $"CheckBtn_{idx}_{cp.name}";

            var btn = go.GetComponentInChildren<UnityEngine.UI.Button>();
            int captureIdx = idx;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    TeleportToCheckpoint(captureIdx);
                    if (CheckPointPanel != null)
                    {
                        CheckPointPanel.SetActive(false);
                    }
                });
            }

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                string label = cp.GetDisplayName();
                tmp.text = string.IsNullOrEmpty(label) ? $"Checkpoint {idx}" : label;
            }
        }
    }

    public void ReviveAllEnemies()
    {
        List<EnemyCore> enemies = FindAllEnemiesInLoadedScenes();
        foreach (var e in enemies)
        {
            if (e != null)
            {
                e.Revive();
            }
        }
    }

    private List<EnemyCore> FindAllEnemiesInLoadedScenes()
    {
        List<EnemyCore> result = new List<EnemyCore>();
        int sceneCount = SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var enemies = root.GetComponentsInChildren<EnemyCore>(true);
                if (enemies != null && enemies.Length > 0)
                {
                    result.AddRange(enemies);
                }
            }
        }
        return result;
    }

    public void ResetCheckpoints()
    {
        unlockedCheckpoints.Clear();
        currentRespawnIndex = -1;
    }
}
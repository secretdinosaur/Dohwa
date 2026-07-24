using UnityEngine;
using UnityEngine.Playables;
using System.Collections;
using Unity.Cinemachine;

[RequireComponent(typeof(PlayableDirector))]
[RequireComponent(typeof(BoxCollider))]
public class CutsceneManager : MonoBehaviour
{
    public string cutsceneId;
    [Header("Dolly Cart Reference")]
    public Transform startTransform;

    protected PlayableDirector director;
    private bool hasPlayed = false;
    private bool isSkipped = false;

    public GameObject[] CutSceneDisableUIs;
    public float uiFadeDuration = 1f;

    public float panelTurnOnTimeBeforeEnd = 1f;

    public GameObject[] EnableWhenStart;
    public GameObject[] EnableWhenEnd;
    public GameObject[] DisableWhenStart;
    public GameObject[] DisableWhenEnd;

    private Coroutine showPanelCoroutine;

    private void Awake()
    {
        canPlay = false;
        director = GetComponent<PlayableDirector>();
        StartCoroutine(WaitRo());
    }
    bool canPlay;
    IEnumerator WaitRo()
    {

        yield return null;
        hasPlayed = DataManager.Instance.IsCutscenePlayed(cutsceneId);
        yield return new WaitForSeconds(0.25f);
        canPlay = true;
    }
    private void OnTriggerStay(Collider other)
    {
        if (DataManager.Instance != null && hasPlayed)
        {
            // 이미 본 컷씬이라면 컴포넌트 비활성화 또는 오브젝트 제거
            //gameObject.SetActive(false);
            return;
        }

        if (!hasPlayed && other.CompareTag("Player") && canPlay)
        {
            StartCutscene();
        }
    }

    public virtual void StartCutscene()
    {
        if (hasPlayed || !canPlay) return;
        canPlay = false;
        Player.Instance.SetCinematicMode(true, true);

        if (Player.Instance.PlayerMove != null && Player.Instance.PlayerMove._rb != null)
        {
            Player.Instance.PlayerMove._rb.isKinematic = true;
        }
        if (DataManager.Instance != null)
        {
            DataManager.Instance.MarkCutsceneAsPlayed(cutsceneId);
        }

        Player.Instance.transform.position = startTransform.position;
        Player.Instance.transform.rotation = startTransform.rotation;
        for (int i = 0; i < CutSceneDisableUIs.Length; i++)
        {
            if (CutSceneDisableUIs[i] != null)
                CutSceneDisableUIs[i].SetActive(false);
        }

        if (EnableWhenStart != null)
        {
            foreach (var obj in EnableWhenStart) { if (obj != null) obj.SetActive(true); }
        }
        if (DisableWhenStart != null)
        {
            foreach (var obj in DisableWhenStart) { if (obj != null) obj.SetActive(false); }
        }

        director.Play();
        showPanelCoroutine = StartCoroutine(ShowPanelBeforeEnd(0.25f));
    }

    public void SkipCutscene()
    {
        if (hasPlayed) return;
        isSkipped = true;

        if (showPanelCoroutine != null)
        {
            StopCoroutine(showPanelCoroutine);
        }

        // 1프레임 대기 로직을 처리하기 위해 코루틴으로 스킵 실행
        StartCoroutine(SkipRoutine());
    }

    private IEnumerator SkipRoutine()
    {
        director.stopped -= OnCutsceneEnded;

        director.time = director.duration;
        director.Evaluate();

        yield return null;

        director.stopped += OnCutsceneEnded;

        if (director.state == PlayState.Playing)
        {
            director.Stop();
        }
        else
        {
            OnCutsceneEnded(director);
        }
    }

    protected IEnumerator ShowPanelBeforeEnd(float time)
    {
        float waitTime = (float)director.duration - time;

        while (waitTime > 0)
        {
            waitTime -= Time.deltaTime;
            yield return null;
        }
        fadePanel.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (director != null) director.stopped += OnCutsceneEnded;
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= OnCutsceneEnded;
    }
    public FadePanel fadePanel;
    private void OnCutsceneEnded(PlayableDirector obj)
    {
        hasPlayed = true;
        Player.Instance.transform.SetParent(null);

        if (Player.Instance.PlayerMove != null && Player.Instance.PlayerMove._rb != null)
        {
            Player.Instance.PlayerMove._rb.isKinematic = false;
        }

        Player.Instance.SetCinematicMode(false, true);

        foreach (var ui in CutSceneDisableUIs)
        {
            if (ui == null) continue;
            ui.SetActive(true);
            CanvasGroup canvasGroup = ui.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = ui.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeInUI(canvasGroup, uiFadeDuration));
        }

        if (EnableWhenEnd != null)
        {
            foreach (var ob in EnableWhenEnd) { if (ob != null) ob.SetActive(true); }
        }
        if (DisableWhenEnd != null)
        {
            foreach (var ob in DisableWhenEnd) { if (ob != null) ob.SetActive(false); }
        }

        StartCoroutine(DisableCutsceneRoutine(3.5f));
    }

    private IEnumerator FadeInUI(CanvasGroup cg, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, time / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    protected virtual IEnumerator DisableCutsceneRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        fadePanel.HidePanel();
    }
}
using JetBrains.Annotations;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class EndingSceneManager : CutsceneManager
{
    [Header("Ending Scene Settings")]
    public float speed = 1;
    public AudioClip audioClip;

    [Header("Player Positions")]
    public Transform playerStartTransform;
    public Transform playerSecondTransform;

    private Vector3 initialPos;
    private Quaternion initialRot;
    private Coroutine earlyEndRoutine;

    private void Start()
    {
        director = GetComponent<PlayableDirector>();
        if (director != null)
        {
            director.played += OnEndingSceneStarted;
        }
        initialPos = transform.localPosition;
        initialRot = transform.localRotation;
    }

    public float swaySpeed = 2f;
    public float heightAmount = 0.2f;
    public float rollAngle = 3f;

    void Update()
    {
        /*float newY = initialPos.y + Mathf.Sin(Time.time * swaySpeed) * heightAmount;
        transform.localPosition = new Vector3(initialPos.x, newY, initialPos.z);

        float roll = Mathf.Cos(Time.time * swaySpeed * 0.5f) * rollAngle;
        transform.localRotation = initialRot * Quaternion.Euler(0, 0, roll);*/
    }

    public override void StartCutscene()
    {
        StartCoroutine(CutScene());
    }

    IEnumerator CutScene()
    {
        Player.Instance.HP = 9999;
        Player.Instance.SetCinematicMode(true, true);
        SoundManager.Instance.PlayOnetimeBGM(audioClip);

        Player.Instance.SetCinematicMode(true, true);

        if (Player.Instance.PlayerMove != null && Player.Instance.PlayerMove._rb != null)
        {
            Player.Instance.PlayerMove._rb.isKinematic = true;
        }

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

        if (earlyEndRoutine != null) StopCoroutine(earlyEndRoutine);
        earlyEndRoutine = StartCoroutine(EarlyEndRoutine());

        StartCoroutine(ShowPanelBeforeEnd(1));

        yield return new WaitForSeconds(2);
    }

    private IEnumerator EarlyEndRoutine()
    {
        float waitTime = (float)director.duration - 5f;

        if (waitTime > 0)
        {
            yield return new WaitForSeconds(waitTime);
        }

        if (director.state == PlayState.Playing)
        {
        }

        OnEndingSceneEnded(director);

        StartCoroutine(DisableCutsceneRoutine(6f));
    }

    private void OnDestroy()
    {
        if (director != null)
        {
            director.played -= OnEndingSceneStarted;
        }
    }

    private void OnEndingSceneStarted(PlayableDirector obj)
    {
        

        StartCoroutine(MoveToSecondPositionRoutine());
    }

    private IEnumerator MoveToSecondPositionRoutine()
    {
        yield return new WaitForSeconds(2);
        if (playerStartTransform != null)
        {
            Player.Instance.transform.position = playerStartTransform.position;
            Player.Instance.transform.rotation = playerStartTransform.rotation;
        }
        yield return new WaitForSeconds(4.2f);

        if (playerSecondTransform != null)
        {
            Player.Instance.transform.position = playerSecondTransform.position;
            Player.Instance.transform.rotation = playerSecondTransform.rotation;
            Player.Instance.transform.SetParent(playerSecondTransform);
        }
    }

    private void OnEndingSceneEnded(PlayableDirector obj)
    {

    }

    protected override IEnumerator DisableCutsceneRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene("Main");
    }
}
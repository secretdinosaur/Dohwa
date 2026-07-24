using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class EnvironmentSound : MonoBehaviour
{
    [Header("Environment Sound Settings")]
    public AudioClip[] environmentClips;

    [Header("Interval Settings (Seconds)")]
    public float minInterval = 5f;
    public float maxInterval = 15f;

    [Header("Target Setting")]
    public string targetTag = "Player";

    private Coroutine playRoutine;
    private bool isPlayerInside = false;

    // AudioSource 대신 풀링 객체인 SoundObject를 직접 참조합니다.
    private SoundObject currentPlayingSound;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        // 플레이어가 영역 안에 있지만 죽었을 경우 처리
        if (isPlayerInside && Player.Instance != null && !Player.Instance.IsAlive)
        {
            StopEnvironmentSound();
            isPlayerInside = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            // 플레이어가 이미 죽어있다면 진입 시 환경음을 재생하지 않음
            if (Player.Instance != null && !Player.Instance.IsAlive) return;

            isPlayerInside = true;
            if (playRoutine == null)
            {
                playRoutine = StartCoroutine(EnvironmentSoundRoutine());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isPlayerInside = false;
            StopEnvironmentSound();
        }
    }

    private void StopEnvironmentSound()
    {
        // 대기 코루틴 중지
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        // 재생 중인 사운드가 있다면 SoundManager의 기능을 통해 정지 및 풀 반환
        if (currentPlayingSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopSound(currentPlayingSound);
            currentPlayingSound = null;
        }
    }

    private IEnumerator EnvironmentSoundRoutine()
    {
        while (isPlayerInside)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            float timer = 0f;

            // waitTime만큼 대기하되, 매 프레임 플레이어 생존 여부 체크
            while (timer < waitTime)
            {
                if (Player.Instance != null && !Player.Instance.IsAlive)
                {
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            if (isPlayerInside && environmentClips != null && environmentClips.Length > 0)
            {
                AudioClip randomClip = environmentClips[Random.Range(0, environmentClips.Length)];

                if (SoundManager.Instance != null)
                {
                    // 반환되는 SoundObject를 캐싱하여 나중에 강제로 끌 수 있게 둡니다.
                    currentPlayingSound = SoundManager.Instance.PlaySound(randomClip);
                }
            }
        }
        playRoutine = null;
    }
}
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MusicTrigger : MonoBehaviour
{
    [Tooltip("이 구역에 들어왔을 때 랜덤으로 재생할 BGM 리스트")]
    public AudioClip[] zoneBGMClips;

    [Tooltip("구역에 들어오고 음악이 재생되기까지의 대기 시간(초)")]
    public float startDelay = 5f;

    [Tooltip("경계선 밖으로 나갔을 때 음악을 유지하는 유예 시간(초)")]
    public float exitDelay = 60f;

    private Coroutine exitRoutine;
    private Coroutine startRoutine; // 시작 딜레이를 관리할 코루틴
    private bool isInside = false;
    private bool isPlayingZoneBGM = false;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInside = true;

            // 나가는 도중에 다시 들어왔다면 종료 타이머 취소
            if (exitRoutine != null)
            {
                StopCoroutine(exitRoutine);
                exitRoutine = null;
            }

            // 현재 음악이 재생 중이 아니고, 시작 대기 중도 아니라면 시작 타이머 가동
            if (!isPlayingZoneBGM && startRoutine == null)
            {
                startRoutine = StartCoroutine(StartDelayRoutine());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInside = false;

            // 시작 대기 중(5초 이내)에 밖으로 나갔다면 재생 자체를 취소
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
                startRoutine = null;
            }

            if (exitRoutine != null)
            {
                StopCoroutine(exitRoutine);
                exitRoutine = null;
            }

            // 이미 음악이 재생 중일 때만 종료 유예 시간 대기 시작
            if (isPlayingZoneBGM)
            {
                exitRoutine = StartCoroutine(ExitDelayRoutine());
            }
        }
    }

    private IEnumerator StartDelayRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        // 5초가 지난 시점에도 여전히 구역 안에 있고 음악이 안 나오고 있다면 재생
        if (isInside && !isPlayingZoneBGM)
        {
            PlayZoneBGM();
        }

        startRoutine = null;
    }

    private void PlayZoneBGM()
    {
        if (zoneBGMClips != null && zoneBGMClips.Length > 0 && SoundManager.Instance != null)
        {
            AudioClip randomClip = zoneBGMClips[Random.Range(0, zoneBGMClips.Length)];
            SoundManager.Instance.PlaySpecialBGM(randomClip);
            isPlayingZoneBGM = true;
        }
    }

    private IEnumerator ExitDelayRoutine()
    {
        yield return new WaitForSeconds(exitDelay);

        // 유예 시간이 끝날 때까지 다시 들어오지 않았다면 종료 예약 실행
        if (!isInside && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopSpecialBGMSequence();
            isPlayingZoneBGM = false;
        }

        exitRoutine = null;
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic; // List 사용을 위해 추가

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

    // 플레이어가 물리적으로 트리거 안에 있는지 여부만 기록합니다.
    private bool isPlayerInside = false;

    // 💡 수정됨: 재생된 여러 겹의 사운드들을 놓치지 않고 전부 끄기 위해 List로 관리합니다.
    private List<SoundObject> activeSounds = new List<SoundObject>();

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
        // 💡 수정됨: Inventory.activeSelf 대신 확실한 화면 노출 여부를 체크하는 activeInHierarchy 사용
        bool canPlay = isPlayerInside &&
                       Player.Instance != null &&
                       Player.Instance.IsAlive &&
                       UIManager.Instance != null &&
                       !UIManager.Instance.Inventory.activeInHierarchy;

        if (canPlay)
        {
            // 재생 조건이 맞는데 코루틴이 돌고 있지 않다면 시작합니다. (인벤토리를 닫았을 때 재개)
            if (playRoutine == null)
            {
                playRoutine = StartCoroutine(EnvironmentSoundRoutine());
            }
        }
        else
        {
            // 재생 조건이 안 맞는데 사운드 로직이 돌고 있거나 켜진 소리가 있다면 정지합니다.
            if (playRoutine != null || activeSounds.Count > 0)
            {
                StopEnvironmentSound();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isPlayerInside = false;
        }
    }

    private void StopEnvironmentSound()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        // 💡 수정됨: 리스트에 등록된 모든 사운드를 순회하며 확실하게 전부 정지시킵니다.
        if (SoundManager.Instance != null)
        {
            foreach (var sound in activeSounds)
            {
                if (sound != null && sound.audioSource != null && sound.audioSource.isPlaying)
                {
                    SoundManager.Instance.StopSound(sound);
                }
            }
        }

        // 정지 완료 후 리스트 초기화
        activeSounds.Clear();
    }

    private IEnumerator EnvironmentSoundRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (environmentClips != null && environmentClips.Length > 0)
            {
                AudioClip randomClip = environmentClips[Random.Range(0, environmentClips.Length)];

                if (SoundManager.Instance != null)
                {
                    SoundObject newSound = SoundManager.Instance.PlaySound(randomClip);
                    activeSounds.Add(newSound);

                    // 성능 최적화: 이미 재생이 끝났거나 풀로 돌아간 사운드오브젝트는 리스트에서 자동 청소
                    activeSounds.RemoveAll(s => s == null || !s.gameObject.activeInHierarchy || !s.audioSource.isPlaying);
                }
            }
        }
    }
}
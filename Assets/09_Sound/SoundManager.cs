using UnityEngine;
using Redcode.Pools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    PoolManager poolManager;

    public AudioClip[] UIsounds;
    public AudioClip[] MovementSounds;
    public AudioClip[] bgmClips;
    public AudioClip teacherSound;
    public AudioClip CriticalSound;
    public AudioSource bgmSource;
    [Range(0f, 1f)] public float masterVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    private List<SoundObject> activeSFXList = new List<SoundObject>();

    // 💡 적(EnemySound)들의 AudioSource를 추적하기 위한 리스트 추가
    private List<AudioSource> enemyAudioSources = new List<AudioSource>();

    private AudioClip lastPlayedBGM;
    private Coroutine fadeCoroutine;
    private float idleTimer = 0f;
    private float autoPlayInterval = 120f;

    // 특별 BGM 종료 예약을 추적하기 위한 변수
    private bool isStoppingSpecialBGM = false;


    public Button[] NotUseDefaultSoundButton;

    private void Awake()
    {
        Instance = this;
        poolManager = GetComponent<PoolManager>();

        SetNextAutoPlayInterval();
    }

    private void Start()
    {
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (Button btn in allButtons)
        {
            if (NotUseDefaultSoundButton.Contains(btn)) continue;
            btn.onClick.AddListener(() => SoundManager.Instance.PlayUISound(4));
        }
    }

    private void Update()
    {
        // bgm이 꺼져있을 때만 타이머를 돌려 자동 재생
        if (!bgmSource.isPlaying)
        {
            // 루프 해제로 특별 BGM이 자연스럽게 끝났을 때를 대비해 상태 초기화
            if (isStoppingSpecialBGM)
            {
                isStoppingSpecialBGM = false;
                idleTimer = 0f; // BGM이 끝나자마자 다음 자동재생 타이머 시작
            }

            idleTimer += Time.deltaTime;
            if (idleTimer >= autoPlayInterval)
            {
                PlayRandomBGM();
            }
        }
        else
        {
            idleTimer = 0f;
        }
    }

    public void PlayTeacherSound()
    {
        if(teacherSound != null)
            PlaySound(teacherSound);
    }

    public void PlayCriticalSound()
    {
               PlaySound(CriticalSound);
    }
    public void SetMasterVolume(float volume)
    {
        masterVolume = volume;
        UpdateBGMVolume();
        UpdateSFXVolume();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
        UpdateSFXVolume();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = volume;
        UpdateBGMVolume();
    }

    private void UpdateBGMVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = masterVolume * bgmVolume;
        }
    }

    private void UpdateSFXVolume()
    {
        // 1. 기존 사운드 풀 객체들 볼륨 조절
        for (int i = 0; i < activeSFXList.Count; i++)
        {
            if (activeSFXList[i] != null && activeSFXList[i].audioSource != null)
            {
                activeSFXList[i].audioSource.volume = masterVolume * sfxVolume;
            }
        }

        // 2. 💡 맵에 존재하는 모든 Enemy의 AudioSource 볼륨 조절
        for (int i = enemyAudioSources.Count - 1; i >= 0; i--)
        {
            if (enemyAudioSources[i] == null)
            {
                // 적이 파괴되었을 경우 리스트에서 자동 정리
                enemyAudioSources.RemoveAt(i);
            }
            else
            {
                enemyAudioSources[i].volume = masterVolume * sfxVolume;
            }
        }
    }

    // ====== 💡 EnemySound에서 직접 호출할 등록/해제 함수 ======
    public void RegisterEnemyAudioSource(AudioSource source)
    {
        if (source != null && !enemyAudioSources.Contains(source))
        {
            enemyAudioSources.Add(source);
            source.volume = masterVolume * sfxVolume; // 등록되는 즉시 현재 설정된 볼륨 적용
        }
    }

    public void UnregisterEnemyAudioSource(AudioSource source)
    {
        if (enemyAudioSources.Contains(source))
        {
            enemyAudioSources.Remove(source);
        }
    }
    // ==========================================================

    private void SetNextAutoPlayInterval()
    {
        autoPlayInterval = Random.Range(120f, 180f);
    }

    public void PlayRandomBGM()
    {
        if (bgmSource.isPlaying || bgmClips.Length == 0) return;

        AudioClip nextClip = null;
        if (bgmClips.Length == 1)
        {
            nextClip = bgmClips[0];
        }
        else
        {
            do
            {
                nextClip = bgmClips[Random.Range(0, bgmClips.Length)];
            } while (nextClip == lastPlayedBGM);
        }

        lastPlayedBGM = nextClip;
        isStoppingSpecialBGM = false;
        PlayBGMCore(nextClip, false); // 랜덤 BGM은 루프 안 함
        SetNextAutoPlayInterval();
    }

    // 외부에서 특정 클립을 재생시킬 때 사용
    public void PlaySpecialBGM(AudioClip specialClip)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        isStoppingSpecialBGM = false;
        fadeCoroutine = StartCoroutine(FadeOutAndPlay(specialClip));
    }

    // 특별 BGM 종료 예약을 추적하기 위한 변수
    public void StopSpecialBGMSequence()
    {
        if (bgmSource != null && bgmSource.isPlaying && bgmSource.loop)
        {
            // 즉시 끄지 않고 반복만 풀어줌 -> 현재 재생 중인 클립이 끝까지 재생된 후 멈춤
            bgmSource.loop = false;
            isStoppingSpecialBGM = true;
        }
    }

    private IEnumerator FadeOutAndPlay(AudioClip newClip)
    {
        float fadeSpeed = bgmSource.volume / 1f; // 1초 동안 페이드아웃

        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        bgmSource.Stop();
        lastPlayedBGM = null;

        // 특별 BGM은 기본적으로 무한 반복(loop: true)
        PlayBGMCore(newClip, true);
    }

    public void PlayOnetimeBGM(AudioClip clip)
    {
        isStoppingSpecialBGM = false;
        PlayBGMCore(clip, false);
    }

    private void PlayBGMCore(AudioClip clip, bool loop)
    {
        if (bgmSource == null) return;
        bgmSource.clip = clip;
        UpdateBGMVolume();
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public IEnumerator ReturnRoutine(SoundObject sound)
    {
        yield return new WaitForSeconds(sound.audioSource.clip.length + 3);
        ReturnSoundObject(sound);
    }

    public SoundObject PlaySound(AudioClip clip, float pitch = 1)
    {
        SoundObject sound = poolManager.GetFromPool<SoundObject>();
        sound.audioSource.pitch = pitch;

        sound.audioSource.volume = masterVolume * sfxVolume;

        sound.audioSource.clip = clip;
        sound.audioSource.Play();

        if (!activeSFXList.Contains(sound))
        {
            activeSFXList.Add(sound);
        }

        StartCoroutine(ReturnRoutine(sound));

        return sound;
    }

    public void PlayMovementSound(int index)
    {
        switch (index)
        {
            case 0:
                PlaySound(MovementSounds[Random.Range(0, 7)]);
                break;
            case 1:
                SoundObject sound = PlaySound(MovementSounds[Random.Range(0, 7)]);
                sound.audioSource.pitch = 1.2f;
                break;
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
                PlaySound(MovementSounds[index + 5]);
                break;
        }
    }

    public void StopSpecialBGMWithFade()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            isStoppingSpecialBGM = false; // 자연 종료 예약을 취소하고 강제 종료로 전환
            fadeCoroutine = StartCoroutine(FadeOutAndStopRoutine());
        }
    }

    public void StopSound(SoundObject soundObject)
    {
        if (soundObject != null && soundObject.audioSource != null && soundObject.audioSource.isPlaying)
        {
            soundObject.audioSource.Stop();
            ReturnSoundObject(soundObject);
        }
    }

    private IEnumerator FadeOutAndStopRoutine()
    {
        float fadeSpeed = bgmSource.volume / 1f; // 1초 동안 페이드아웃

        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        bgmSource.Stop();
        lastPlayedBGM = null;

        // 다음번에 재생될 곡(자동 랜덤 BGM 등)이 소리가 0으로 나오지 않도록 볼륨을 원상 복구해 둡니다.
        UpdateBGMVolume();
    }

    public IEnumerator FadeOutAndStopSpecificRoutine(SoundObject obj)
    {
        float fadeSpeed = obj.audioSource.volume / 3f; // 1초 동안 페이드아웃

        while (obj.audioSource.volume > 0)
        {
            obj.audioSource.volume -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        obj.audioSource.Stop();
    }

    public void ReturnSoundObject(SoundObject soundObject)
    {
        if (activeSFXList.Contains(soundObject))
        {
            activeSFXList.Remove(soundObject);
        }
        if (soundObject.gameObject.activeSelf)
            poolManager.TakeToPool<SoundObject>(soundObject);
    }

    public void PlayUISound(int index)
    {
        if (index >= 0 && index < UIsounds.Length)
        {
            PlaySound(UIsounds[index]);
        }
    }
}
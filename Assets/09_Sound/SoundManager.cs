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
    private List<AudioSource> enemyAudioSources = new List<AudioSource>();

    private AudioClip lastPlayedBGM;
    private Coroutine fadeCoroutine;
    private float idleTimer = 0f;
    private float autoPlayInterval = 120f;

    private bool isStoppingSpecialBGM = false;

    // 사망 시 BGM 먹먹해짐(희미해짐) 처리를 위한 변수
    private bool isDeadMuffled = false;
    private Coroutine muffleCoroutine;

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
        if (!bgmSource.isPlaying)
        {
            if (isStoppingSpecialBGM)
            {
                isStoppingSpecialBGM = false;
                idleTimer = 0f;
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
        if (teacherSound != null)
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
            // 💡 사망 상태일 경우 BGM 볼륨을 20%로 강제 축소
            float targetVol = masterVolume * bgmVolume;
            if (isDeadMuffled) targetVol *= 0.2f;
            bgmSource.volume = targetVol;
        }
    }

    private void UpdateSFXVolume()
    {
        for (int i = 0; i < activeSFXList.Count; i++)
        {
            if (activeSFXList[i] != null && activeSFXList[i].audioSource != null)
            {
                activeSFXList[i].audioSource.volume = masterVolume * sfxVolume;
            }
        }

        for (int i = enemyAudioSources.Count - 1; i >= 0; i--)
        {
            if (enemyAudioSources[i] == null)
            {
                enemyAudioSources.RemoveAt(i);
            }
            else
            {
                enemyAudioSources[i].volume = masterVolume * sfxVolume;
            }
        }
    }

    public void RegisterEnemyAudioSource(AudioSource source)
    {
        if (source != null && !enemyAudioSources.Contains(source))
        {
            enemyAudioSources.Add(source);
            source.volume = masterVolume * sfxVolume;
        }
    }

    public void UnregisterEnemyAudioSource(AudioSource source)
    {
        if (enemyAudioSources.Contains(source))
        {
            enemyAudioSources.Remove(source);
        }
    }

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
        PlayBGMCore(nextClip, false);
        SetNextAutoPlayInterval();
    }

    public void PlaySpecialBGM(AudioClip specialClip)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        isStoppingSpecialBGM = false;
        fadeCoroutine = StartCoroutine(FadeOutAndPlay(specialClip));
    }

    public void StopSpecialBGMSequence()
    {
        if (bgmSource != null && bgmSource.isPlaying && bgmSource.loop)
        {
            bgmSource.loop = false;
            isStoppingSpecialBGM = true;
        }
    }

    private IEnumerator FadeOutAndPlay(AudioClip newClip)
    {
        float fadeSpeed = bgmSource.volume / 1f;

        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        bgmSource.Stop();
        lastPlayedBGM = null;

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

        // 피치 저하 삭제, 1.0 고정
        bgmSource.pitch = 1.0f;
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

            isStoppingSpecialBGM = false;
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
        float fadeSpeed = bgmSource.volume / 1f;

        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= fadeSpeed * Time.deltaTime;
            yield return null;
        }

        bgmSource.Stop();
        lastPlayedBGM = null;

        bgmSource.pitch = 1.0f; // 초기화
        UpdateBGMVolume();
    }

    public IEnumerator FadeOutAndStopSpecificRoutine(SoundObject obj)
    {
        float fadeSpeed = obj.audioSource.volume / 3f;

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

    public void MuffleBGMOnDeath()
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        if (muffleCoroutine != null) StopCoroutine(muffleCoroutine);
        muffleCoroutine = StartCoroutine(MuffleBGMRoutine(true));
    }

    public void RestoreBGMOnRespawn()
    {
        if (bgmSource == null) return;
        if (muffleCoroutine != null) StopCoroutine(muffleCoroutine);
        muffleCoroutine = StartCoroutine(MuffleBGMRoutine(false));
    }

    private IEnumerator MuffleBGMRoutine(bool isMuffling)
    {
        isDeadMuffled = isMuffling;

        float startVolume = bgmSource.volume;
        float targetVolume = isMuffling ? (masterVolume * bgmVolume * 0.2f) : (masterVolume * bgmVolume);

        float elapsed = 0f;
        float duration = 1.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 볼륨만 조절
            bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        bgmSource.volume = targetVolume;
    }
}
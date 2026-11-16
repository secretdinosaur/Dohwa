using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class EnvironmentSound : MonoBehaviour
{
    public AudioClip[] environmentClips;
    public float minInterval = 5f;
    public float maxInterval = 15f;
    public string targetTag = "Player";

    private Coroutine playRoutine;
    private bool isPlayerInside = false;
    private int playerColliderCount = 0;
    private List<SoundObject> activeSounds = new List<SoundObject>();

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        StopEnvironmentSound();
        playerColliderCount = 0;
        isPlayerInside = false;
    }

    private void Update()
    {
        bool canPlay = isPlayerInside &&
                       Player.Instance != null &&
                       Player.Instance.IsAlive &&
                       UIManager.Instance != null &&
                       !UIManager.Instance.Inventory.activeInHierarchy;

        if (canPlay)
        {
            if (playRoutine == null)
            {
                playRoutine = StartCoroutine(EnvironmentSoundRoutine());
            }
        }
        else
        {
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
            playerColliderCount++;
            isPlayerInside = playerColliderCount > 0;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            playerColliderCount--;
            isPlayerInside = playerColliderCount > 0;
        }
    }

    private void StopEnvironmentSound()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (SoundManager.Instance != null)
        {
            foreach (var sound in activeSounds)
            {
                if (sound != null && sound.audioSource != null && sound.audioSource.isPlaying)
                {
                    SoundManager.Instance.FadeOutAndStopSpecificRoutine(sound);
                }
            }
        }

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

                    activeSounds.RemoveAll(s => s == null || !s.gameObject.activeInHierarchy || !s.audioSource.isPlaying);

                    if (randomClip != null)
                    {
                        yield return new WaitForSeconds(randomClip.length);
                    }
                }
            }
        }
    }
}
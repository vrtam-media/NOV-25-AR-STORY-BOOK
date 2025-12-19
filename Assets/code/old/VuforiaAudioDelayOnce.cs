using UnityEngine;
using System.Collections;
using Vuforia;

[RequireComponent(typeof(ObserverBehaviour))]
public class VuforiaAudioDelayOnce : MonoBehaviour
{
    public AudioSource audioSource;
    public float delaySeconds = 4f;

    private ObserverBehaviour observer;
    private Coroutine playRoutine;

    private bool isTracked = false;
    private bool hasPlayedThisTrack = false;

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError("[VuforiaAudioDelayOnce] Assign an AudioSource.");
            enabled = false;
            return;
        }

        observer.OnTargetStatusChanged += OnTargetStatusChanged;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.Stop();
        audioSource.time = 0f;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        // IMPORTANT: only true when marker is ACTUALLY visible
        bool trackedNow = status.Status == Status.TRACKED;

        // FOUND (first time after being lost)
        if (trackedNow && !isTracked)
        {
            isTracked = true;
            hasPlayedThisTrack = false;   // allow play for this new tracking session
            StartDelayThenPlay();
        }

        // LOST
        if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopAndReset();
        }
    }

    private void StartDelayThenPlay()
    {
        StopDelay();  // cancel any old coroutine
        playRoutine = StartCoroutine(DelayThenPlay());
    }

    private IEnumerator DelayThenPlay()
    {
        // wait only while still tracked
        float t = 0f;
        while (t < delaySeconds)
        {
            if (!isTracked) yield break;  // marker lost during wait
            t += Time.deltaTime;
            yield return null;
        }

        // play only once per tracking session
        if (isTracked && !hasPlayedThisTrack)
        {
            audioSource.time = 0f;
            audioSource.Play();
            hasPlayedThisTrack = true;
        }

        playRoutine = null;
    }

    private void StopAndReset()
    {
        StopDelay();

        if (audioSource.isPlaying)
            audioSource.Stop();

        audioSource.time = 0f;
        hasPlayedThisTrack = false; // next found should play again
    }

    private void StopDelay()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }
}

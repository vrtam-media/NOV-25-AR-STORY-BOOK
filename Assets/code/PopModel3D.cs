using UnityEngine;
using System.Collections;

public class PopModel3D : MonoBehaviour
{
    [Header("Delay (handled by Vuforia script)")]
    public float startDelay = 0f;

    [Header("Pop Settings")]
    public float duration = 0.5f;
    public Vector3 startScale = Vector3.one * 0.001f;
    public float overshootMultiplier = 1.1f;
    public float overshootPoint = 0.7f;

    [Header("Smoothness")]
    public AnimationCurve easeUp = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeDown = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional Pop SFX")]
    public AudioClip popSfx;
    [Range(0f, 1f)] public float popSfxVolume = 1f;

    [Header("Optional VoiceOver")]
    public AudioSource voiceOverSource;   // drag your AudioSource here (optional)
    public AudioClip voiceOverClip;       // optional
    public float voiceOverDelay = 0f;     // delay AFTER model turns ON

    Vector3 originalScale;
    Coroutine popRoutine;
    Coroutine voiceRoutine;
    AudioSource sfxSource;

    void Awake()
    {
        originalScale = transform.localScale;

        // create SFX audio source only if needed
        if (popSfx != null)
        {
            sfxSource = gameObject.GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    public void PlayPopWithAudio()
    {
        // stop old routines
        if (popRoutine != null) StopCoroutine(popRoutine);
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);

        // play pop sfx immediately if assigned
        if (popSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(popSfx, popSfxVolume);

        // start pop animation
        popRoutine = StartCoroutine(PopRoutine());

        // start voiceover if assigned
        if (voiceOverSource != null && voiceOverClip != null)
            voiceRoutine = StartCoroutine(VoiceRoutine());
    }

    IEnumerator PopRoutine()
    {
        Vector3 endScale = originalScale;
        Vector3 overshootScale = endScale * overshootMultiplier;

        transform.localScale = startScale;

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            if (t < overshootPoint)
            {
                float p1 = t / overshootPoint;
                float e1 = easeUp.Evaluate(p1);
                transform.localScale = Vector3.LerpUnclamped(startScale, overshootScale, e1);
            }
            else
            {
                float p2 = (t - overshootPoint) / (1f - overshootPoint);
                float e2 = easeDown.Evaluate(p2);
                transform.localScale = Vector3.LerpUnclamped(overshootScale, endScale, e2);
            }

            yield return null;
        }

        transform.localScale = endScale;
    }

    IEnumerator VoiceRoutine()
    {
        if (voiceOverDelay > 0f)
            yield return new WaitForSeconds(voiceOverDelay);

        voiceOverSource.clip = voiceOverClip;
        voiceOverSource.Play();
    }

    // called by Vuforia script when marker lost
    public void StopAllAudioNow()
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        if (voiceOverSource != null) voiceOverSource.Stop();
        if (sfxSource != null) sfxSource.Stop();
    }

    void OnDisable()
    {
        StopAllAudioNow();
    }
}

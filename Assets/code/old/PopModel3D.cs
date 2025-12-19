using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    // ------------------ MULTI VOICE ------------------

    [System.Serializable]
    public class VoiceClipEntry
    {
        public AudioClip clip;
        [Tooltip("Delay BEFORE this clip plays (seconds)")]
        public float delayBefore = 0f;
    }

    [Header("Optional VoiceOver (Multiple Clips)")]
    public AudioSource voiceOverSource;
    public List<VoiceClipEntry> voiceClips = new();

    // ------------------ NEW: ANIM DELAY ------------------

    [Header("Optional Character Animation Delay")]
    [Tooltip("Animator of this character (optional). If empty, auto-finds on this object.")]
    public Animator characterAnimator;

    [Tooltip("Delay BEFORE animator starts (seconds). Can be different per character.")]
    public float animationDelay = 0f;

    [Tooltip("If ON, animator restarts from beginning every time PlayPopWithAudio is called.")]
    public bool restartAnimatorOnPlay = true;

    // ------------------------------------------------------

    private Vector3 originalScale;
    private Coroutine popRoutine;
    private Coroutine voiceRoutine;
    private Coroutine animRoutine;
    private AudioSource sfxSource;

    void Awake()
    {
        originalScale = transform.localScale;

        // auto-find animator if not assigned
        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

        // create SFX audio source only if needed
        if (popSfx != null)
        {
            sfxSource = gameObject.GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    // called by Vuforia when env/character should pop
    public void PlayPopWithAudio()
    {
        // stop old routines
        if (popRoutine != null) StopCoroutine(popRoutine);
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        if (animRoutine != null) StopCoroutine(animRoutine);

        // restart / pause animator immediately
        if (characterAnimator != null)
        {
            characterAnimator.enabled = false;  // keep frozen until delay is done
        }

        // play pop sfx immediately if assigned
        if (popSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(popSfx, popSfxVolume);

        // start pop animation
        popRoutine = StartCoroutine(PopRoutine());

        // start voiceover sequence if assigned
        if (voiceOverSource != null && voiceClips != null && voiceClips.Count > 0)
            voiceRoutine = StartCoroutine(VoiceSequenceRoutine());

        // start animator after delay (optional)
        if (characterAnimator != null)
            animRoutine = StartCoroutine(AnimationDelayRoutine());
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

    IEnumerator VoiceSequenceRoutine()
    {
        foreach (var entry in voiceClips)
        {
            if (entry == null || entry.clip == null)
                continue;

            if (entry.delayBefore > 0f)
                yield return new WaitForSeconds(entry.delayBefore);

            if (!gameObject.activeInHierarchy) yield break;

            voiceOverSource.clip = entry.clip;
            voiceOverSource.Play();

            while (voiceOverSource.isPlaying)
            {
                if (!gameObject.activeInHierarchy) yield break;
                yield return null;
            }
        }
    }

    // ✅ NEW: delay animation start
    IEnumerator AnimationDelayRoutine()
    {
        if (animationDelay > 0f)
            yield return new WaitForSeconds(animationDelay);

        if (!gameObject.activeInHierarchy) yield break;

        characterAnimator.enabled = true;

        if (restartAnimatorOnPlay)
        {
            // restart current state from beginning
            characterAnimator.Play(0, 0, 0f);
        }
    }

    // called by Vuforia script when marker lost
    public void StopAllAudioNow()
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        if (animRoutine != null) StopCoroutine(animRoutine);

        if (voiceOverSource != null) voiceOverSource.Stop();
        if (sfxSource != null) sfxSource.Stop();

        if (characterAnimator != null)
        {
            characterAnimator.enabled = false;
            if (restartAnimatorOnPlay)
                characterAnimator.Play(0, 0, 0f); // reset pose
        }
    }

    void OnDisable()
    {
        StopAllAudioNow();
    }
}

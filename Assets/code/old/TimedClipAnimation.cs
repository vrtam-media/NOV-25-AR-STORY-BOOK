using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Vuforia;

public class TimedClipAnimation : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Header("Clip")]
        public AnimationClip clip;

        [Tooltip("If 0, uses clip.length")]
        public float durationOverride = 0f;

        [Tooltip("If ON, clip will loop to fill Duration Override")]
        public bool loopDuringDuration = true;

        [Header("Transition (optional)")]
        [Tooltip("Seconds to fade in from previous step")]
        public float fadeIn = 0f;

        [Tooltip("Seconds to fade out before next step")]
        public float fadeOut = 0f;

        [Header("Pause After (Freeze Pose)")]
        [Tooltip("Hold last pose for this many seconds before next step")]
        public float pauseAfter = 0f;

        [Header("Optional Rotation Override (Step Only)")]
        public bool overrideRotation = false;
        public Vector3 targetLocalEuler = Vector3.zero;

        [Tooltip("0 = instant snap")]
        public float rotateDuration = 0f;

        [Tooltip("If ON, pivot returns to previous rotation after this step finishes")]
        public bool revertAfterStep = true;

        [Tooltip("0 = instant snap back")]
        public float revertDuration = 0.25f;

        [Header("Optional Step Audio")]
        public AudioClip stepAudio;

        [Tooltip("Loop audio while this step runs")]
        public bool loopAudioDuringStep = true;

        [Tooltip("Gap between loops (seconds). 0 = no gap.")]
        public float audioLoopGap = 0f;

        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Tooltip("Stop audio at step end (recommended ON). If OFF, audio keeps going.")]
        public bool stopAudioOnStepEnd = true;
    }

    [Header("Animator")]
    public Animator animator;

    [Header("Steps in order (drag clips here)")]
    public List<Step> steps = new List<Step>();

    [Header("Rotation Target (Pivot Parent)")]
    [Tooltip("Assign empty parent pivot here. Animator is on child.")]
    public Transform rotationTarget;

    [Header("Restart behavior")]
    public bool restartOnEnable = true;

    [Header("Optional Tracking (ImageTarget)")]
    public ObserverBehaviour observer;

    [Header("Audio Source (auto-created if empty)")]
    public AudioSource stepAudioSource;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationPlayableOutput output;

    private AnimationClipPlayable currentPlayable;
    private Coroutine sequenceRoutine;

    private Coroutine stepAudioRoutine;

    private bool isTracked = true; // default true if no observer
    private Quaternion originalRot;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rotationTarget == null) rotationTarget = transform;

        originalRot = rotationTarget.localRotation;

        // audio source setup (separate from VO, won’t interrupt others)
        if (stepAudioSource == null)
        {
            stepAudioSource = gameObject.GetComponent<AudioSource>();
            if (stepAudioSource == null) stepAudioSource = gameObject.AddComponent<AudioSource>();
        }
        stepAudioSource.playOnAwake = false;
        stepAudioSource.loop = false;

        SetupGraph();

        if (observer == null)
            observer = GetComponentInParent<ObserverBehaviour>();

        if (observer != null)
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;

        DestroyGraph();
    }

    void OnEnable()
    {
        if (restartOnEnable)
            RestartSequence();
    }

    void OnDisable()
    {
        PauseAndReset();
    }

    // ---------------- VUFORIA TRACKING ----------------
    private void OnTargetStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        if (trackedNow && !isTracked)
        {
            isTracked = true;
            RestartSequence();
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;
            PauseAndReset();
        }
    }

    // ---------------- PUBLIC API ----------------
    public void RestartSequence()
    {
        if (!gameObject.activeInHierarchy) return;

        SetupGraph();
        StopSequenceRoutine();
        ResetState();
        sequenceRoutine = StartCoroutine(SequenceLoop());
    }

    public void PauseAndReset()
    {
        StopSequenceRoutine();
        ResetState();
        StopStepAudioImmediate();

        if (graph.IsValid())
            mixer.SetInputWeight(0, 0f);
    }

    // ---------------- CORE SEQUENCE ----------------
    private IEnumerator SequenceLoop()
    {
        if (steps == null || steps.Count == 0)
            yield break;

        for (int i = 0; i < steps.Count; i++)
        {
            if (!isTracked) yield break;

            Step s = steps[i];
            if (s.clip == null) continue;

            Quaternion stepStartRot = rotationTarget.localRotation;

            if (s.overrideRotation)
                yield return RotateTarget(Quaternion.Euler(s.targetLocalEuler), s.rotateDuration);

            currentPlayable = AnimationClipPlayable.Create(graph, s.clip);
            currentPlayable.SetApplyFootIK(false);
            currentPlayable.SetApplyPlayableIK(false);

            mixer.DisconnectInput(0);
            mixer.ConnectInput(0, currentPlayable, 0);

            if (s.fadeIn > 0f)
                yield return FadeWeight(0f, 1f, s.fadeIn);
            else
                mixer.SetInputWeight(0, 1f);

            // start step audio (if any)
            StartStepAudio(s);

            float dur = (s.durationOverride > 0f) ? s.durationOverride : s.clip.length;
            float t = 0f;

            currentPlayable.SetTime(0);
            currentPlayable.SetSpeed(1f);

            while (t < dur)
            {
                if (!isTracked) yield break;

                t += Time.deltaTime;

                if (s.loopDuringDuration)
                {
                    double clipLen = s.clip.length;
                    double timeNow = currentPlayable.GetTime();
                    if (timeNow >= clipLen)
                        currentPlayable.SetTime(timeNow % clipLen);
                }

                yield return null;
            }

            if (s.fadeOut > 0f)
                yield return FadeWeight(1f, 0f, s.fadeOut);
            else
                mixer.SetInputWeight(0, 0f);

            if (s.stopAudioOnStepEnd)
                StopStepAudioImmediate();

            if (s.pauseAfter > 0f)
            {
                currentPlayable.SetSpeed(0f);
                float p = 0f;
                while (p < s.pauseAfter)
                {
                    if (!isTracked) yield break;
                    p += Time.deltaTime;
                    yield return null;
                }
            }

            if (s.overrideRotation && s.revertAfterStep)
                yield return RotateTarget(stepStartRot, s.revertDuration);
        }
    }

    // ---------------- STEP AUDIO ----------------
    private void StartStepAudio(Step s)
    {
        StopStepAudioImmediate();

        if (stepAudioSource == null || s.stepAudio == null) return;

        // no looping -> play once
        if (!s.loopAudioDuringStep)
        {
            stepAudioSource.PlayOneShot(s.stepAudio, s.audioVolume);
            return;
        }

        // looping with optional gap
        stepAudioRoutine = StartCoroutine(StepAudioLoopRoutine(s));
    }

    private IEnumerator StepAudioLoopRoutine(Step s)
    {
        float gap = Mathf.Max(0f, s.audioLoopGap);

        while (isTracked && gameObject.activeInHierarchy)
        {
            stepAudioSource.PlayOneShot(s.stepAudio, s.audioVolume);

            // wait clip length + gap
            float wait = s.stepAudio.length + gap;
            float t = 0f;
            while (t < wait)
            {
                if (!isTracked) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void StopStepAudioImmediate()
    {
        if (stepAudioRoutine != null)
        {
            StopCoroutine(stepAudioRoutine);
            stepAudioRoutine = null;
        }

        if (stepAudioSource == null) return;

        // Stop only this source (other audio keeps playing)
        stepAudioSource.Stop();
        stepAudioSource.clip = null;
        stepAudioSource.loop = false;
    }

    // ---------------- HELPERS ----------------
    private IEnumerator FadeWeight(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            if (!isTracked) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / time);
            mixer.SetInputWeight(0, Mathf.Lerp(from, to, a));
            yield return null;
        }
        mixer.SetInputWeight(0, to);
    }

    private IEnumerator RotateTarget(Quaternion targetRot, float dur)
    {
        if (rotationTarget == null) yield break;

        Quaternion start = rotationTarget.localRotation;

        if (dur <= 0f)
        {
            rotationTarget.localRotation = targetRot;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            if (!isTracked) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            rotationTarget.localRotation = Quaternion.Slerp(start, targetRot, a);
            yield return null;
        }

        rotationTarget.localRotation = targetRot;
    }

    private void ResetState()
    {
        if (currentPlayable.IsValid())
            currentPlayable.Destroy();

        if (rotationTarget != null)
            rotationTarget.localRotation = originalRot;
    }

    private void StopSequenceRoutine()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }
    }

    // ---------------- PLAYABLE GRAPH ----------------
    private void SetupGraph()
    {
        if (graph.IsValid()) return;

        graph = PlayableGraph.Create($"{name}_TimedClipSequence");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 1);
        output = AnimationPlayableOutput.Create(graph, "AnimOut", animator);
        output.SetSourcePlayable(mixer);

        graph.Play();
    }

    private void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Vuforia;

public class OffOnTimer : MonoBehaviour
{
    [Header("Off Duration (seconds)")]
    public float offDuration = 2f;

    [Header("What to turn OFF")]
    [Tooltip("If empty, THIS object will be turned off (visual+anim+collider), without disabling the script.")]
    public GameObject target;

    [Header("Restart Animator After ON")]
    public bool restartAnimatorOnEnable = true;

    [Header("Optional Tracking Reset (ImageTarget)")]
    public ObserverBehaviour observer;

    // cached components
    private List<Renderer> renderers = new List<Renderer>();
    private List<Collider> colliders = new List<Collider>();
    private List<Animator> animators = new List<Animator>();
    private List<AudioSource> audioSources = new List<AudioSource>();

    private Coroutine routine;
    private bool isTracked = false;

    void Awake()
    {
        if (target == null) target = gameObject;

        CacheTargetComponents();

        if (observer == null)
            observer = GetComponentInParent<ObserverBehaviour>();

        if (observer != null)
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        else
        {
            // no observer = always run once at start
            isTracked = true;
            StartSequence();
        }
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void CacheTargetComponents()
    {
        renderers.Clear();
        colliders.Clear();
        animators.Clear();
        audioSources.Clear();

        renderers.AddRange(target.GetComponentsInChildren<Renderer>(true));
        colliders.AddRange(target.GetComponentsInChildren<Collider>(true));
        animators.AddRange(target.GetComponentsInChildren<Animator>(true));
        audioSources.AddRange(target.GetComponentsInChildren<AudioSource>(true));
    }

    private void OnTargetStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        if (trackedNow && !isTracked)
        {
            isTracked = true;
            StartSequence();      // ✅ restart fresh every time found
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopSequence();
            ForceOff();           // ✅ stay OFF while lost
        }
    }

    private void StartSequence()
    {
        StopSequence();
        routine = StartCoroutine(OffThenOnRoutine());
    }

    private void StopSequence()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator OffThenOnRoutine()
    {
        // Step 1: OFF immediately (but keep script alive)
        ForceOff();

        // Step 2: wait
        float t = 0f;
        while (t < offDuration)
        {
            if (!isTracked) yield break; // stop if lost
            t += Time.deltaTime;
            yield return null;
        }

        if (!isTracked) yield break;

        // Step 3: ON again
        ForceOn();
    }

    private void ForceOff()
    {
        // disable visuals
        foreach (var r in renderers) if (r) r.enabled = false;

        // disable collisions
        foreach (var c in colliders) if (c) c.enabled = false;

        // stop animators + freeze
        foreach (var a in animators)
        {
            if (!a) continue;
            a.enabled = false;
        }

        // stop audio (optional)
        foreach (var s in audioSources)
        {
            if (s) s.Stop();
        }
    }

    private void ForceOn()
    {
        // enable visuals
        foreach (var r in renderers) if (r) r.enabled = true;

        // enable collisions
        foreach (var c in colliders) if (c) c.enabled = true;

        // enable animators + restart
        foreach (var a in animators)
        {
            if (!a) continue;
            a.enabled = true;

            if (restartAnimatorOnEnable)
            {
                a.Rebind();
                a.Update(0f);
                a.Play(0, 0, 0f);
            }
        }
    }
}

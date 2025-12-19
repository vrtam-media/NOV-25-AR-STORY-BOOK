using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Vuforia;

[RequireComponent(typeof(ObserverBehaviour))]
public class VuforiaTimedEnvController : MonoBehaviour
{
    [System.Serializable]
    public class EnvSegment
    {
        public string name;
        public GameObject envRoot;

        [Header("Timeline (seconds)")]
        public float startTime;
        public float endTime;

        [Header("Optional Delays")]
        public float startDelay = 0f; // wait after startTime before ON
        public float endDelay = 0f;   // keep ON after endTime
    }

    [Header("Segments in order")]
    public EnvSegment[] segments;

    [Header("Restart logic")]
    public float lostGraceTime = 0.25f;
    public bool loopTimeline = true;

    private ObserverBehaviour observer;

    private Coroutine timelineRoutine;
    private Coroutine lostRoutine;

    private bool isTracked = false;

    private float globalTime = 0f;
    private int currentIndex = -1;
    private int resumeIndex = 0;

    // ✅ keep pop coroutines so we can stop them on env switch
    private List<Coroutine> popCoroutines = new List<Coroutine>();

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        HideAllEnvs();
        observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        if (trackedNow && !isTracked)
        {
            isTracked = true;

            if (lostRoutine != null)
            {
                StopCoroutine(lostRoutine);
                lostRoutine = null;
            }

            StartFromResumeEnv();
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;

            if (lostRoutine == null)
                lostRoutine = StartCoroutine(DelayedLost());
        }
    }

    private IEnumerator DelayedLost()
    {
        yield return new WaitForSeconds(lostGraceTime);

        if (!isTracked)
        {
            resumeIndex = Mathf.Max(currentIndex, 0);
            StopTimelineAndHideAll();
        }

        lostRoutine = null;
    }

    private void StartFromResumeEnv()
    {
        StopTimelineAndHideAll();

        if (segments == null || segments.Length == 0)
            return;

        globalTime = segments[resumeIndex].startTime;
        currentIndex = -1;
        timelineRoutine = StartCoroutine(TimelineLoop());
    }

    private IEnumerator TimelineLoop()
    {
        while (isTracked)
        {
            int newIndex = GetSegmentIndexWithDelays(globalTime);

            if (newIndex != currentIndex)
            {
                currentIndex = newIndex;
                SwitchToEnv(currentIndex);
            }

            globalTime += Time.deltaTime;

            float timelineEnd = GetTimelineEnd();

            if (globalTime >= timelineEnd)
            {
                if (loopTimeline)
                {
                    globalTime = segments[0].startTime;
                    currentIndex = -1;
                }
                else yield break;
            }

            yield return null;
        }
    }

    private int GetSegmentIndexWithDelays(float t)
    {
        for (int i = 0; i < segments.Length; i++)
        {
            float onTime = segments[i].startTime + segments[i].startDelay;
            float offTime = segments[i].endTime + segments[i].endDelay;

            if (t >= onTime && t < offTime)
                return i;
        }

        return 0;
    }

    private float GetTimelineEnd()
    {
        float maxEnd = 0f;
        foreach (var s in segments)
        {
            float offTime = s.endTime + s.endDelay;
            if (offTime > maxEnd) maxEnd = offTime;
        }
        return maxEnd;
    }

    // ✅ MAIN FIX: env switch triggers pop with each character's startDelay
    private void SwitchToEnv(int index)
    {
        // stop old pop waits
        StopPopCoroutines();

        HideAllEnvs();

        var seg = segments[index];
        if (seg.envRoot == null) return;

        seg.envRoot.SetActive(true);

        // find all pop models in this env
        PopModel3D[] popModels = seg.envRoot.GetComponentsInChildren<PopModel3D>(true);

        foreach (var pop in popModels)
        {
            if (pop == null) continue;

            // start hidden first, wait its delay, then pop
            pop.gameObject.SetActive(false);

            var c = StartCoroutine(DelayedPop(pop, seg.envRoot));
            popCoroutines.Add(c);
        }
    }

    private IEnumerator DelayedPop(PopModel3D pop, GameObject envRoot)
    {
        float d = Mathf.Max(0f, pop.startDelay);

        float t = 0f;
        while (t < d)
        {
            // stop if tracking lost or env switched off
            if (!isTracked || envRoot == null || !envRoot.activeInHierarchy)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        if (!isTracked || envRoot == null || !envRoot.activeInHierarchy)
            yield break;

        pop.gameObject.SetActive(true);
        pop.PlayPopWithAudio();
    }

    private void StopPopCoroutines()
    {
        foreach (var c in popCoroutines)
            if (c != null) StopCoroutine(c);

        popCoroutines.Clear();
    }

    private void HideAllEnvs()
    {
        if (segments == null) return;
        foreach (var s in segments)
        {
            if (s.envRoot != null)
                s.envRoot.SetActive(false);
        }
    }

    private void StopTimelineAndHideAll()
    {
        if (timelineRoutine != null)
        {
            StopCoroutine(timelineRoutine);
            timelineRoutine = null;
        }

        StopPopCoroutines();

        // stop audio + hide envs
        if (segments != null)
        {
            foreach (var s in segments)
            {
                if (s.envRoot == null) continue;

                PopModel3D[] pops = s.envRoot.GetComponentsInChildren<PopModel3D>(true);
                foreach (var p in pops)
                    p.StopAllAudioNow();

                s.envRoot.SetActive(false);
            }
        }

        currentIndex = -1;
    }
}

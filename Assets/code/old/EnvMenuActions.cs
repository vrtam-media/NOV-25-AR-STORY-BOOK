using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Vuforia;

public class EnvMenuActions : MonoBehaviour
{
    [System.Serializable]
    public class EnvEntry
    {
        public string name;
        public GameObject envRoot;
    }

    [Header("Tracking Source (ImageTarget)")]
    public ObserverBehaviour observer; // Page1_Target

    [Header("Environments in order")]
    public List<EnvEntry> envs = new List<EnvEntry>();

    [Header("Clamp At Ends")]
    public bool clampAtEnds = true;

    private int currentIndex = 0;
    private bool isTracked = true;

    // pop coroutines so we can stop when switching env
    private List<Coroutine> popCoroutines = new List<Coroutine>();

    void Awake()
    {
        if (observer == null)
            observer = GetComponentInParent<ObserverBehaviour>();

        if (observer != null)
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    void OnEnable()
    {
        ApplyEnv(currentIndex, replay: true);
    }

    // -------- VUFORIA TRACKING --------
    private void OnTargetStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        if (trackedNow && !isTracked)
        {
            isTracked = true;
            ApplyEnv(currentIndex, replay: true);
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopAllPopCoroutines();
            DisableAllEnvs();
        }
    }

    // -------- PUBLIC API FOR UI --------
    public void RestartFromBeginning()
    {
        SetIndex(0, replay: true);
    }

    public void ReplayCurrentEnv()
    {
        ApplyEnv(currentIndex, replay: true);
    }

    public void GoNextEnv()
    {
        int next = currentIndex + 1;
        if (next >= envs.Count)
        {
            if (clampAtEnds) return;
            next = envs.Count - 1;
        }
        SetIndex(next, replay: true);
    }

    public void GoPrevEnv()
    {
        int prev = currentIndex - 1;
        if (prev < 0)
        {
            if (clampAtEnds) return;
            prev = 0;
        }
        SetIndex(prev, replay: true);
    }

    // -------- CORE --------
    private void SetIndex(int index, bool replay)
    {
        currentIndex = Mathf.Clamp(index, 0, envs.Count - 1);
        ApplyEnv(currentIndex, replay);
    }

    private void ApplyEnv(int index, bool replay)
    {
        if (!isTracked) return;
        if (envs == null || envs.Count == 0) return;

        StopAllPopCoroutines();
        DisableAllEnvs();

        var entry = envs[index];
        if (entry.envRoot == null) return;

        entry.envRoot.SetActive(true);

        // ✅ make menu switching behave like your timed controller
        if (replay)
            StartCoroutine(ReplayEnvRoutine(entry.envRoot));
    }

    private void DisableAllEnvs()
    {
        foreach (var e in envs)
        {
            if (e.envRoot != null)
                e.envRoot.SetActive(false);
        }
    }

    // -------- REPLAY ENV (POP + AUDIO FIX) --------
    private IEnumerator ReplayEnvRoutine(GameObject envRoot)
    {
        // wait one frame so hierarchy is active
        yield return null;

        // 1) Restart any TimedClipAnimation
        var timedClips = envRoot.GetComponentsInChildren<TimedClipAnimation>(true);
        foreach (var t in timedClips)
            t.RestartSequence();

        // 2) POP LOGIC (same as VuforiaTimedEnvController)
        var pops = envRoot.GetComponentsInChildren<PopModel3D>(true);

        foreach (var pop in pops)
        {
            if (pop == null) continue;

            // stop any old audio
            pop.StopAllAudioNow();

            // hide character first
            pop.gameObject.SetActive(false);

            // delay then pop
            var c = StartCoroutine(DelayedPop(pop, envRoot));
            popCoroutines.Add(c);
        }
    }

    private IEnumerator DelayedPop(PopModel3D pop, GameObject envRoot)
    {
        float d = Mathf.Max(0f, pop.startDelay);

        float t = 0f;
        while (t < d)
        {
            if (!isTracked) yield break;
            if (envRoot == null || !envRoot.activeInHierarchy) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        if (!isTracked) yield break;
        if (envRoot == null || !envRoot.activeInHierarchy) yield break;

        pop.gameObject.SetActive(true);
        pop.PlayPopWithAudio();
    }

    private void StopAllPopCoroutines()
    {
        foreach (var c in popCoroutines)
            if (c != null) StopCoroutine(c);

        popCoroutines.Clear();
    }
}

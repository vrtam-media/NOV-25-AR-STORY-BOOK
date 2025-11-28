using UnityEngine;
using System.Collections;
using Vuforia;

[RequireComponent(typeof(ObserverBehaviour))]
public class VuforiaPopTrigger : MonoBehaviour
{
    [Header("Options")]
    public bool popOnlyOnce = false;

    private ObserverBehaviour observer;
    private bool isTracked = false;
    private bool hasPoppedThisSession = false;

    private Coroutine sessionRoutine;

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        observer.OnTargetStatusChanged += OnStatusChanged;

        HideAllPopModels();
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
            hasPoppedThisSession = false;
            StartSession();
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopSessionAndReset();
        }
    }

    private void StartSession()
    {
        if (sessionRoutine != null)
            StopCoroutine(sessionRoutine);

        sessionRoutine = StartCoroutine(SessionRoutine());
    }

    private IEnumerator SessionRoutine()
    {
        if (popOnlyOnce && hasPoppedThisSession)
            yield break;

        var targets = GetComponentsInChildren<PopModel3D>(true);

        // hide everything first (important for clean restart)
        foreach (var t in targets)
        {
            if (t == null) continue;
            t.StopAllAudioNow();
            t.gameObject.SetActive(false);
        }

        // schedule each model by its own delay
        foreach (var t in targets)
        {
            if (t == null) continue;
            StartCoroutine(DelayedShowAndPop(t));
        }

        hasPoppedThisSession = true;
        sessionRoutine = null;
    }

    private IEnumerator DelayedShowAndPop(PopModel3D t)
    {
        float d = Mathf.Max(0f, t.startDelay);

        float elapsed = 0f;
        while (elapsed < d)
        {
            if (!isTracked) yield break;   // marker lost during delay
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isTracked) yield break;

        t.gameObject.SetActive(true);
        t.PlayPopWithAudio();
    }

    private void StopSessionAndReset()
    {
        if (sessionRoutine != null)
        {
            StopCoroutine(sessionRoutine);
            sessionRoutine = null;
        }

        HideAllPopModels();
        hasPoppedThisSession = false;
    }

    private void HideAllPopModels()
    {
        var targets = GetComponentsInChildren<PopModel3D>(true);

        foreach (var t in targets)
        {
            if (t == null) continue;
            t.StopAllAudioNow();
            t.gameObject.SetActive(false);
        }
    }

    // ✅ Call this when env changes while tracking is still ON
    public void RetriggerForCurrentEnv()
    {
        if (!isTracked) return;
        StartSession();
    }
}

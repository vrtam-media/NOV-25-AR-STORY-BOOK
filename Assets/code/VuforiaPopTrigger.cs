using UnityEngine;
using Vuforia;
using System.Collections;
using System.Collections.Generic;

public class VuforiaPopTrigger : MonoBehaviour
{
    public bool popOnlyOnce = true;

    ObserverBehaviour observer;
    bool hasPopped = false;

    PopModel3D[] models;
    Dictionary<PopModel3D, Coroutine> running = new Dictionary<PopModel3D, Coroutine>();

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        models = GetComponentsInChildren<PopModel3D>(true);

        // Start: keep ALL models OFF
        foreach (var m in models)
            m.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (observer != null)
            observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDisable()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow =
            status.Status == Status.TRACKED ||
            status.Status == Status.EXTENDED_TRACKED;

        if (trackedNow)
        {
            if (popOnlyOnce && hasPopped) return;
            hasPopped = true;

            foreach (var m in models)
            {
                if (running.TryGetValue(m, out var c) && c != null)
                    StopCoroutine(c);

                running[m] = StartCoroutine(DelayThenPop(m));
            }
        }
        else
        {
            foreach (var kv in running)
                if (kv.Value != null) StopCoroutine(kv.Value);

            running.Clear();

            foreach (var m in models)
            {
                m.StopAllAudioNow();
                m.gameObject.SetActive(false);
            }
        }
    }

    IEnumerator DelayThenPop(PopModel3D m)
    {
        // OFF during delay (your requirement)
        m.gameObject.SetActive(false);

        if (m.startDelay > 0f)
            yield return new WaitForSeconds(m.startDelay);

        // ON after delay
        m.gameObject.SetActive(true);

        // pop + optional audios
        m.PlayPopWithAudio();
    }
}

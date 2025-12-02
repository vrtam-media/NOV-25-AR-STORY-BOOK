using UnityEngine;
using System.Collections;
using Vuforia;

public class DieAfterTime : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("Seconds after enable/found to die/off")]
    public float lifeTime = 5f;

    [Header("Optional Die SFX (AudioClip only)")]
    public AudioClip dieSfx;
    [Range(0f, 1f)] public float dieSfxVolume = 1f;

    [Header("Disable vs Destroy")]
    [Tooltip("If ON -> SetActive(false). If OFF -> Destroy(gameObject).")]
    public bool disableInsteadOfDestroy = true;

    [Tooltip("Extra delay after SFX start before disabling/destroying.\nIf 0, object may disable immediately but SFX still plays (detached).")]
    public float offDelayAfterSfx = 0f;

    [Header("Optional Tracking Reset (ImageTarget)")]
    [Tooltip("Drag your ImageTarget's ObserverBehaviour here if you want restart on lost/found.")]
    public ObserverBehaviour observer;

    private Coroutine routine;
    private bool isTracked = true; // default true if no observer

    void Awake()
    {
        if (observer == null)
            observer = GetComponentInParent<ObserverBehaviour>();

        if (observer != null)
            observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnEnable()
    {
        RestartTimer();
    }

    void OnDisable()
    {
        StopRoutine();
        // no need to stop sfx now because it's detached
    }

    private void OnStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        // LOST -> cancel timer
        if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopRoutine();
        }

        // FOUND -> restart timer
        if (trackedNow && !isTracked)
        {
            isTracked = true;
            RestartTimer();
        }
    }

    public void RestartTimer()
    {
        StopRoutine();
        routine = StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        // wait lifetime while tracking stays true
        float t = 0f;
        while (t < lifeTime)
        {
            if (!isTracked) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        // ✅ play SFX on a detached temp object so it overlaps & survives disable
        if (dieSfx != null)
            PlayDetachedSfx(dieSfx, dieSfxVolume);

        // optional hold before turning off
        float hold = Mathf.Max(0f, offDelayAfterSfx);
        if (hold > 0f)
        {
            float h = 0f;
            while (h < hold)
            {
                if (!isTracked) yield break;
                h += Time.deltaTime;
                yield return null;
            }
        }

        routine = null;
        if (!isTracked) yield break;

        if (disableInsteadOfDestroy)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);
    }

    private void PlayDetachedSfx(AudioClip clip, float volume)
    {
        GameObject sfxObj = new GameObject($"DieSFX_{clip.name}");
        sfxObj.transform.position = transform.position;

        AudioSource a = sfxObj.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f; // 2D sound (set to 1 if you want 3D)
        a.volume = volume;
        a.clip = clip;
        a.Play();

        Destroy(sfxObj, clip.length + 0.1f);
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}

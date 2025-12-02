using UnityEngine;
using System.Collections;
using Vuforia;

[ExecuteAlways]
public class Env2DirectionalLightFX : MonoBehaviour
{
    [Header("Tracking Source (ImageTarget)")]
    public ObserverBehaviour observer;   // drag Page1_Target here

    [Header("Light Reference")]
    public Light targetLight;            // drag Directional Light here

    [Header("Color Sequence (Start -> Target -> Back)")]
    public bool enableColorFX = true;

    public bool overrideStartColor = false;
    public Color startColor = Color.white;

    public float colorStartDelay = 0f;
    public float fadeToTargetDuration = 1f;
    public Color targetColor = Color.yellow;

    public float colorHoldTime = 1f;
    public float fadeBackDuration = 1f;

    public bool overrideBackColor = false;
    public Color backColor = Color.white;

    [Header("Transform Sequence (Optional)")]
    public bool enableTransformFX = false;
    public float transformStartDelay = 0f;
    public float moveToTargetDuration = 1f;
    public Vector3 targetLocalPosition;
    public Vector3 targetLocalEuler;
    public float transformHoldTime = 1f;
    public float moveBackDuration = 1f;

    [Header("Editor Preview")]
    public bool previewInEditor = true;
    public enum PreviewMode { StartColor, TargetColor, BackColor }
    public PreviewMode previewMode = PreviewMode.TargetColor;

    // internal state
    private Color originalColor;
    private Vector3 originalPos;
    private Quaternion originalRot;

    private Coroutine fxRoutine;
    private bool isTracked = true; // if no observer, assume tracked

    void Awake()
    {
        CacheOriginals();

        // DO NOT auto-find in play unless you truly want that.
        // Better: you drag the correct observer in inspector.
        if (observer == null)
        {
            observer = GetComponentInParent<ObserverBehaviour>(true);
        }

        if (Application.isPlaying && observer != null)
            observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    private void CacheOriginals()
    {
        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>(true);

        if (targetLight != null)
        {
            originalColor = targetLight.color;
            originalPos = targetLight.transform.localPosition;
            originalRot = targetLight.transform.localRotation;
        }
    }

    // ✅ KEY FIX: FX only starts when Env2 becomes active
    void OnEnable()
    {
        CacheOriginals();
        ResetToStartState();

        // If tracking is already true, start now
        if (Application.isPlaying && isTracked)
            RestartFX();
    }

    // ✅ KEY FIX: When Env2 is disabled (env switch), stop & reset
    void OnDisable()
    {
        if (!Application.isPlaying) return;

        StopFX();
        ResetToStartState();  // so Env0 never inherits Env2 color
    }

    private void OnStatusChanged(ObserverBehaviour obs, TargetStatus status)
    {
        bool trackedNow = status.Status == Status.TRACKED;

        if (trackedNow && !isTracked)
        {
            isTracked = true;

            // ✅ Only run FX if Env2 is currently active
            if (gameObject.activeInHierarchy)
                RestartFX();
        }
        else if (!trackedNow && isTracked)
        {
            isTracked = false;
            StopFX();
            ResetToStartState();
        }
    }

    private void RestartFX()
    {
        StopFX();
        fxRoutine = StartCoroutine(FXSequence());
    }

    private IEnumerator FXSequence()
    {
        if (targetLight == null) yield break;

        Color realStart = overrideStartColor ? startColor : originalColor;
        Color realBack = overrideBackColor ? backColor : originalColor;

        // reset baseline at sequence start
        targetLight.color = realStart;
        targetLight.transform.localPosition = originalPos;
        targetLight.transform.localRotation = originalRot;

        // ---------------- COLOR FX ----------------
        if (enableColorFX)
        {
            yield return WaitTracked(colorStartDelay);
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return FadeColor(realStart, targetColor, fadeToTargetDuration);
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return WaitTracked(colorHoldTime);
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return FadeColor(targetColor, realBack, fadeBackDuration);
        }

        // ---------------- TRANSFORM FX ----------------
        if (enableTransformFX)
        {
            yield return WaitTracked(transformStartDelay);
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return MoveTransform(
                originalPos, originalRot,
                targetLocalPosition, Quaternion.Euler(targetLocalEuler),
                moveToTargetDuration
            );
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return WaitTracked(transformHoldTime);
            if (!isTracked || !gameObject.activeInHierarchy) yield break;

            yield return MoveTransform(
                targetLocalPosition, Quaternion.Euler(targetLocalEuler),
                originalPos, originalRot,
                moveBackDuration
            );
        }

        fxRoutine = null;
    }

    private IEnumerator WaitTracked(float seconds)
    {
        if (seconds <= 0f) yield break;
        float t = 0f;
        while (t < seconds)
        {
            if (!isTracked || !gameObject.activeInHierarchy) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeColor(Color from, Color to, float dur)
    {
        if (dur <= 0f)
        {
            targetLight.color = to;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            if (!isTracked || !gameObject.activeInHierarchy) yield break;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            targetLight.color = Color.Lerp(from, to, p);
            yield return null;
        }
        targetLight.color = to;
    }

    private IEnumerator MoveTransform(
        Vector3 fromPos, Quaternion fromRot,
        Vector3 toPos, Quaternion toRot,
        float dur)
    {
        if (dur <= 0f)
        {
            targetLight.transform.localPosition = toPos;
            targetLight.transform.localRotation = toRot;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            if (!isTracked || !gameObject.activeInHierarchy) yield break;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            targetLight.transform.localPosition = Vector3.Lerp(fromPos, toPos, p);
            targetLight.transform.localRotation = Quaternion.Slerp(fromRot, toRot, p);
            yield return null;
        }

        targetLight.transform.localPosition = toPos;
        targetLight.transform.localRotation = toRot;
    }

    private void ResetToStartState()
    {
        if (targetLight == null) return;

        Color realStart = overrideStartColor ? startColor : originalColor;

        targetLight.color = realStart;
        targetLight.transform.localPosition = originalPos;
        targetLight.transform.localRotation = originalRot;
    }

    private void StopFX()
    {
        if (fxRoutine != null)
        {
            StopCoroutine(fxRoutine);
            fxRoutine = null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        CacheOriginals();

        if (!previewInEditor || Application.isPlaying) return;
        if (targetLight == null) return;

        switch (previewMode)
        {
            case PreviewMode.StartColor:
                targetLight.color = overrideStartColor ? startColor : originalColor;
                break;
            case PreviewMode.TargetColor:
                targetLight.color = targetColor;
                break;
            case PreviewMode.BackColor:
                targetLight.color = overrideBackColor ? backColor : originalColor;
                break;
        }

        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
}

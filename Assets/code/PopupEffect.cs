using UnityEngine;
using System.Collections;

public class PopupController : MonoBehaviour
{
    [Header("Reference")]
    public GameObject visualRoot;   // drag VisualRoot here

    [Header("Timing")]
    public float startDelay = 0f;   // 0 = no delay
    public float duration = 0.5f;
    public float loopDelay = 0.6f;
    public bool loopPop = false;    // true only for testing

    [Header("Scale Pop")]
    public Vector3 startScale = Vector3.one * 0.001f;
    public float overshootMultiplier = 1.1f;
    public float overshootPoint = 0.7f;

    [Header("Smoothness")]
    public AnimationCurve easeUp = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeDown = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional SFX")]
    public AudioClip popSfx;
    public float sfxVolume = 1f;

    AudioSource audioSource;

    void Start()
    {
        if (visualRoot == null)
        {
            Debug.LogError("PopupController: VisualRoot not assigned!");
            return;
        }

        if (popSfx != null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = sfxVolume;
        }

        StartCoroutine(PopLoop());
    }

    IEnumerator PopLoop()
    {
        while (true)
        {
            // ✅ OFF during delay
            visualRoot.SetActive(false);

            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            // ✅ ON after delay
            visualRoot.SetActive(true);

            yield return StartCoroutine(PopBounceSmooth());

            if (!loopPop) break;

            yield return new WaitForSeconds(loopDelay);
        }
    }

    IEnumerator PopBounceSmooth()
    {
        Transform t = visualRoot.transform;

        Vector3 endScale = t.localScale;
        Vector3 overshootScale = endScale * overshootMultiplier;

        t.localScale = startScale;

        if (popSfx != null && audioSource != null)
            audioSource.PlayOneShot(popSfx);

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float x = Mathf.Clamp01(time / duration);

            if (x < overshootPoint)
            {
                float p1 = x / overshootPoint;
                float e1 = easeUp.Evaluate(p1);
                t.localScale = Vector3.LerpUnclamped(startScale, overshootScale, e1);
            }
            else
            {
                float p2 = (x - overshootPoint) / (1f - overshootPoint);
                float e2 = easeDown.Evaluate(p2);
                t.localScale = Vector3.LerpUnclamped(overshootScale, endScale, e2);
            }

            yield return null;
        }

        t.localScale = endScale;
    }
}

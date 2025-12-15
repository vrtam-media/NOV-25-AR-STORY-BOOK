using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXOnOffTimer : MonoBehaviour
{
    [Header("VFX Targets (drag ParticleSystem roots here)")]
    public List<ParticleSystem> vfxSystems = new List<ParticleSystem>();

    [Header("Timing")]
    [Tooltip("Wait this long AFTER demon becomes active, before first ON.")]
    public float startDelay = 0f;

    [Tooltip("How long VFX stays ON each cycle.")]
    public float onDuration = 2f;

    [Tooltip("How long VFX stays OFF between cycles.")]
    public float offDuration = 2f;

    [Header("Cycle")]
    [Tooltip("If ON, repeats On/Off cycle forever while demon is active.")]
    public bool loop = true;

    [Tooltip("If loop = false, how many ON cycles to play.")]
    public int playCount = 1;

    [Header("Performance / Cleanup")]
    [Tooltip("Stop + Clear particles when OFF. Keeps memory low.")]
    public bool clearOnStop = true;

    [Tooltip("Also disable the VFX GameObject when OFF.")]
    public bool disableGameObjectOnStop = false;

    [Header("Optional VFX Audio (loops only while ON)")]
    public AudioClip vfxLoopClip;
    [Range(0f, 1f)] public float vfxVolume = 1f;

    [Tooltip("If empty, script auto-creates a hidden AudioSource on this object.")]
    public AudioSource audioSource;

    private Coroutine routine;

    void OnEnable()
    {
        // auto collect VFX if empty
        if (vfxSystems == null || vfxSystems.Count == 0)
            vfxSystems = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));

        // auto setup audio source if needed
        SetupAudioSource();

        // start clean
        ForceOff();

        routine = StartCoroutine(RunCycle());
    }

    void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ForceOff();
    }

    private IEnumerator RunCycle()
    {
        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);

        int cyclesDone = 0;

        while (true)
        {
            // ON
            ForceOn();
            if (onDuration > 0)
                yield return new WaitForSeconds(onDuration);

            // OFF
            ForceOff();
            if (offDuration > 0)
                yield return new WaitForSeconds(offDuration);

            cyclesDone++;

            if (!loop && cyclesDone >= Mathf.Max(1, playCount))
                break;
        }
    }

    private void ForceOn()
    {
        // VFX ON
        foreach (var ps in vfxSystems)
        {
            if (ps == null) continue;

            if (disableGameObjectOnStop)
                ps.gameObject.SetActive(true);

            ps.Play(true);
        }

        // Audio ON (loop)
        if (audioSource != null && vfxLoopClip != null)
        {
            if (audioSource.clip != vfxLoopClip)
                audioSource.clip = vfxLoopClip;

            audioSource.volume = vfxVolume;
            audioSource.loop = true;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }

    private void ForceOff()
    {
        // VFX OFF
        foreach (var ps in vfxSystems)
        {
            if (ps == null) continue;

            ps.Stop(true, clearOnStop
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);

            if (disableGameObjectOnStop)
                ps.gameObject.SetActive(false);
        }

        // Audio OFF
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void SetupAudioSource()
    {
        if (audioSource == null && vfxLoopClip != null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D by default, good for AR scene
            audioSource.loop = true;
        }
    }
}

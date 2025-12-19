using System.Collections;
using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class AndroidVideoResumeFix : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Renderer targetRenderer;   // Plane MeshRenderer
    [SerializeField] private RenderTexture targetRT;

    [Header("Material")]
    [SerializeField] private string materialTextureProperty = "_MainTex";

    [Header("Android Recovery")]
    [Tooltip("Frames to wait after resume before rebuilding video")]
    [SerializeField] private int resumeDelayFrames = 6;

    private Coroutine recoveryCoroutine;

    void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        targetRenderer = GetComponent<Renderer>();
    }

    void Awake()
    {
        // Hard safety for Android
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = false;

        ForceInitialBind();
        StartRecovery();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            StartRecovery();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            StartRecovery();
    }

    private void StartRecovery()
    {
        if (recoveryCoroutine != null)
            StopCoroutine(recoveryCoroutine);

        recoveryCoroutine = StartCoroutine(RecoverVideo());
    }

    private IEnumerator RecoverVideo()
    {
        // Wait for Android / Vuforia surface to be ready
        for (int i = 0; i < resumeDelayFrames; i++)
            yield return null;

        // STOP everything
        videoPlayer.Pause();
        videoPlayer.Stop();

        // VERY IMPORTANT: clear broken surface
        videoPlayer.targetTexture = null;

        // Rebuild RenderTexture
        if (targetRT.IsCreated())
            targetRT.Release();

        targetRT.Create();

        // Rebind RT to VideoPlayer
        videoPlayer.targetTexture = targetRT;

        // Rebind RT to Material (shader)
        if (targetRenderer && targetRenderer.material)
            targetRenderer.material.SetTexture(materialTextureProperty, targetRT);

        // Prepare again
        videoPlayer.Prepare();

        float timeout = 3f;
        float t = 0f;

        while (!videoPlayer.isPrepared && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (videoPlayer.isPrepared)
        {
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("[AndroidVideoResumeFix] Video failed to prepare after resume.");
        }

        recoveryCoroutine = null;
    }

    private void ForceInitialBind()
    {
        if (!targetRT.IsCreated())
            targetRT.Create();

        videoPlayer.targetTexture = targetRT;

        if (targetRenderer && targetRenderer.material)
            targetRenderer.material.SetTexture(materialTextureProperty, targetRT);
    }
}

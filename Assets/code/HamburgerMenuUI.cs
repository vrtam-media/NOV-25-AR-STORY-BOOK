using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class HamburgerMenuUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hamburgerButton;
    public RectTransform panel;          // MenuPanel (child)
    public CanvasGroup panelGroup;       // optional, auto-added if null

    public Button restartButton;
    public Button replayButton;
    public Button nextButton;
    public Button prevButton;

    [Header("Env Actions (drag EnvMenuActions here)")]
    public EnvMenuActions envActions;

    [Header("Slide Settings (Responsive)")]
    [Tooltip("How long the panel animates in/out.")]
    public float slideDuration = 0.25f;

    [Tooltip("Panel starts open?")]
    public bool startOpen = false;

    private bool isOpen;
    private Coroutine slideRoutine;

    void Awake()
    {
        if (panelGroup == null && panel != null)
        {
            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null)
                panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        if (hamburgerButton != null)
            hamburgerButton.onClick.AddListener(ToggleMenu);

        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);
        if (replayButton != null)
            replayButton.onClick.AddListener(Replay);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextEnv);
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevEnv);
    }

    void OnEnable()
    {
        isOpen = startOpen;
        ApplyMenuState(isOpen, instant: true);
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;
        ApplyMenuState(isOpen, instant: false);
    }

    private void ApplyMenuState(bool open, bool instant)
    {
        if (panel == null || panelGroup == null) return;

        if (slideRoutine != null)
            StopCoroutine(slideRoutine);

        if (instant)
        {
            panelGroup.alpha = open ? 1f : 0f;
            panelGroup.blocksRaycasts = open;
            panelGroup.interactable = open;
            panel.gameObject.SetActive(open);
            return;
        }

        slideRoutine = StartCoroutine(FadePanel(open));
    }

    private IEnumerator FadePanel(bool open)
    {
        panel.gameObject.SetActive(true);

        float startA = panelGroup.alpha;
        float endA = open ? 1f : 0f;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / slideDuration);
            panelGroup.alpha = Mathf.Lerp(startA, endA, a);
            yield return null;
        }

        panelGroup.alpha = endA;
        panelGroup.blocksRaycasts = open;
        panelGroup.interactable = open;

        if (!open)
            panel.gameObject.SetActive(false);

        slideRoutine = null;
    }

    // ---------- BUTTON ACTIONS ----------
    private void Restart()
    {
        if (envActions != null)
            envActions.RestartFromBeginning();

        CloseAfterClick();
    }

    private void Replay()
    {
        if (envActions != null)
            envActions.ReplayCurrentEnv();

        CloseAfterClick();
    }

    private void NextEnv()
    {
        if (envActions != null)
            envActions.GoNextEnv();

        CloseAfterClick();
    }

    private void PrevEnv()
    {
        if (envActions != null)
            envActions.GoPrevEnv();

        CloseAfterClick();
    }

    private void CloseAfterClick()
    {
        isOpen = false;
        ApplyMenuState(false, instant: false);
    }
}

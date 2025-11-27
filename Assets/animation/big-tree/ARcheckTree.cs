using UnityEngine;
using UnityEngine.InputSystem;

public class ARcheckTree : MonoBehaviour
{
    private Animator mAnimator;
    private const string TriggerName = "ATrigger";

    void Start()
    {
        mAnimator = GetComponentInChildren<Animator>();
        if (mAnimator == null)
            Debug.LogError("ARcheck: Animator not found on this object or its children.");
    }

    void Update()
    {
#if UNITY_EDITOR
        // Editor-only keyboard test (works with New Input System)
        if (Keyboard.current?.oKey.wasPressedThisFrame == true)
            ArriveScale();
#endif
    }

    public void ArriveScale()
    {
        if (mAnimator != null)
            mAnimator.SetTrigger(TriggerName);
    }
}

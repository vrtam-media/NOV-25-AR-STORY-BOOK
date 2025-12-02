using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

// Works across different Splines versions by using reflection.
// Put this on the SAME GameObject that has "Spline Animate".
[DisallowMultipleComponent]
public class SplineAnimateRestartOnEnable : MonoBehaviour
{
    private Component splineAnimate;

    void Awake()
    {
        // "SplineAnimate" is the class name in Unity Splines package
        splineAnimate = GetComponent("SplineAnimate");
    }

    void OnEnable()
    {
        if (splineAnimate == null)
            splineAnimate = GetComponent("SplineAnimate");

        if (splineAnimate == null)
            return;

        ResetAndPlay();
    }

    public void ResetAndPlay()
    {
        Type t = splineAnimate.GetType();

        // Try to reset known time properties if they exist
        SetIfExists(t, splineAnimate, "NormalizedTime", 0f);
        SetIfExists(t, splineAnimate, "ElapsedTime", 0f);
        SetIfExists(t, splineAnimate, "Time", 0f);

        // Try to call Restart / Restart(bool) / Play depending on version
        CallIfExists(t, splineAnimate, "Restart");
        CallIfExists(t, splineAnimate, "Restart", true);
        CallIfExists(t, splineAnimate, "Play");
    }

    private void SetIfExists(Type t, object obj, string propName, float value)
    {
        var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float))
        {
            prop.SetValue(obj, value);
        }
    }

    private void CallIfExists(Type t, object obj, string methodName, params object[] args)
    {
        Type[] argTypes = args.Select(a => a.GetType()).ToArray();

        var method = t.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Instance,
            null,
            argTypes,
            null);

        if (method != null)
        {
            method.Invoke(obj, args);
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public class ParallaxUV_Pro : MonoBehaviour
{
    public enum ParallaxMode { TranslationOnly, RotationOnly, Both }
    public enum AxisMode { XOnly, XY }

    [Header("References")]
    public Transform arCamera;     // ARCamera transform
    public Transform targetRoot;   // ImageTarget transform

    [Header("Mode")]
    public ParallaxMode mode = ParallaxMode.Both;
    public AxisMode axis = AxisMode.XY;
    public bool invert;

    [Header("Strength (tune per layer)")]
    [Tooltip("Translation-based UV parallax strength. BG: 0.03–0.08, FG: 0.08–0.18")]
    [Range(0f, 0.25f)] public float translationStrength = 0.08f;

    [Tooltip("Rotation-based UV parallax strength. Start small: 0.01–0.06")]
    [Range(0f, 0.15f)] public float rotationStrength = 0.03f;

    [Header("Stability / Feel")]
    [Tooltip("Ignore tiny motion (AR jitter).")]
    [Range(0f, 0.02f)] public float deadZone = 0.004f;

    [Tooltip("Higher = snappier, lower = smoother.")]
    [Range(1f, 40f)] public float smooth = 18f;

    [Tooltip("Maximum UV offset magnitude (prevents ugly sliding).")]
    [Range(0.01f, 0.5f)] public float maxOffset = 0.16f;

    [Tooltip("Extra boost for handheld movement. 1 = normal. 1.5–2.5 = stronger.")]
    [Range(0.5f, 3.0f)] public float handheldBoost = 1.6f;

    [Header("Texture Property")]
    public string textureProperty = "_MainTex";

    private Renderer _r;
    private MaterialPropertyBlock _mpb;

    private Vector3 _camLocalStartPos;
    private Quaternion _camLocalStartRot;

    private Vector2 _current;
    private Vector2 _velocity; // for SmoothDamp-like feel

    void Awake()
    {
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        if (!arCamera) arCamera = Camera.main ? Camera.main.transform : null;
        if (!targetRoot) targetRoot = transform.root;

        CacheStartPose();
    }

    void OnEnable()
    {
        // In case tracking was lost and re-acquired, start fresh.
        CacheStartPose();
        _current = Vector2.zero;
        _velocity = Vector2.zero;
        ApplyOffset(_current);
    }

    void CacheStartPose()
    {
        if (!arCamera || !targetRoot) return;

        // Pose of camera in ImageTarget local space
        _camLocalStartPos = targetRoot.InverseTransformPoint(arCamera.position);
        _camLocalStartRot = Quaternion.Inverse(targetRoot.rotation) * arCamera.rotation;
    }

    void LateUpdate()
    {
        if (!_r || !arCamera || !targetRoot) return;

        // Camera pose in target-local space (robust for AR)
        Vector3 camLocalPos = targetRoot.InverseTransformPoint(arCamera.position);
        Quaternion camLocalRot = Quaternion.Inverse(targetRoot.rotation) * arCamera.rotation;

        Vector3 dPos = camLocalPos - _camLocalStartPos;

        // Deadzone to kill AR jitter (translation)
        if (dPos.magnitude < deadZone) dPos = Vector3.zero;

        // Build target UV offset
        Vector2 target = Vector2.zero;

        float dir = invert ? -1f : 1f;

        // 1) Translation parallax (strong when you move phone)
        if (mode == ParallaxMode.TranslationOnly || mode == ParallaxMode.Both)
        {
            // Use X/Y in target local space as “pan”
            float tx = dPos.x;
            float ty = dPos.y;

            if (axis == AxisMode.XOnly) ty = 0f;

            Vector2 t = new Vector2(tx, ty) * (translationStrength * handheldBoost * dir);
            target += t;
        }

        // 2) Rotation parallax (helps when user mostly rotates phone)
        if (mode == ParallaxMode.RotationOnly || mode == ParallaxMode.Both)
        {
            Quaternion dRot = camLocalRot * Quaternion.Inverse(_camLocalStartRot);

            // Convert small rotations to “pan” feel:
            // yaw -> x shift, pitch -> y shift
            Vector3 euler = dRot.eulerAngles;
            float yaw = NormalizeAngle(euler.y);
            float pitch = NormalizeAngle(euler.x);

            // clamp rotation contribution to avoid wild jumps
            yaw = Mathf.Clamp(yaw, -12f, 12f);
            pitch = Mathf.Clamp(pitch, -12f, 12f);

            float rx = yaw / 12f;
            float ry = -pitch / 12f;

            if (axis == AxisMode.XOnly) ry = 0f;

            Vector2 r = new Vector2(rx, ry) * (rotationStrength * dir);
            target += r;
        }

        // Clamp final offset so it never looks detached
        target = Vector2.ClampMagnitude(target, maxOffset);

        // Smooth (exponential) — stable but responsive
        float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        _current = Vector2.Lerp(_current, target, k);

        ApplyOffset(_current);
    }

    void ApplyOffset(Vector2 offset)
    {
        _r.GetPropertyBlock(_mpb);

        // Preserve tiling if you use it; default is (1,1)
        // We set _MainTex_ST: (scaleX, scaleY, offsetX, offsetY)
        _mpb.SetVector(textureProperty + "_ST", new Vector4(1f, 1f, offset.x, offset.y));

        _r.SetPropertyBlock(_mpb);
    }

    static float NormalizeAngle(float a)
    {
        // Convert 0..360 to -180..180
        if (a > 180f) a -= 360f;
        return a;
    }
}

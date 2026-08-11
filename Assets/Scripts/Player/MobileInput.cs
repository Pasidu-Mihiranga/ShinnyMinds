using UnityEngine;
using ShinyMinds.Core;

/// <summary>
/// The one bridge between the on-screen touch controls and the gameplay scripts.
///
/// Runs before PlayerController and CameraController (which sit at the default order 0)
/// so the values they read were sampled this frame. The previous split — a JoystickInput
/// component copying the stick into MobileInput from its own Update — raced the reader
/// and produced a one-frame lag whenever Unity happened to order them the other way.
/// This reads the stick directly instead.
///
/// Put this on the controls Canvas, and point <see cref="controlsRoot"/> at a CHILD of
/// it. This component has to stay active to hear the unlock event, so it must never be
/// the thing that gets switched off.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class MobileInput : MonoBehaviour
{
    public static MobileInput Instance { get; private set; }

    [Header("Controls")]
    [SerializeField] TouchJoystick joystick;

    [Tooltip("Child object holding the stick and buttons. Switched off while a cutscene " +
             "or dialogue owns input. Must NOT be this same object.")]
    [SerializeField] GameObject controlsRoot;

    [Header("Visibility")]
    [Tooltip("Off means the controls also show on desktop, which is how you test them " +
             "in the Game view. On means touch devices only.")]
    [SerializeField] bool touchDevicesOnly = false;

    [Tooltip("Play the desktop build as if it were a phone: prompts read \"Touch\" instead " +
             "of \"Press E\", and a mouse click advances dialogue. Testing only — leave it " +
             "off for a keyboard build, or the prompts will lie about the controls.")]
    [SerializeField] bool simulateTouchOnDesktop = false;

    /// <summary>-1 (turn left) .. +1 (turn right).</summary>
    public float Horizontal { get; private set; }

    /// <summary>-1 (backward) .. +1 (forward).</summary>
    public float Vertical { get; private set; }

    /// <summary>True while the Run button is held.</summary>
    public bool Run { get; private set; }

    /// <summary>True for exactly one frame after a Jump tap. Never latches.</summary>
    public bool JumpPressed { get; private set; }

    bool runHeld;
    bool jumpQueued;
    bool visible = true;

    // Null-safe accessors so gameplay scripts do not each repeat the same guard.
    public static float AxisH => Instance != null ? Instance.Horizontal : 0f;
    public static float AxisV => Instance != null ? Instance.Vertical : 0f;
    public static bool RunHeld => Instance != null && Instance.Run;
    public static bool JumpTapped => Instance != null && Instance.JumpPressed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MobileInput: a second instance exists. Disabling this one.", this);
            enabled = false;
            return;
        }

        Instance = this;

        // Before anything can draw a prompt, so the first line of the first cutscene
        // already reads the right way round.
        TouchInput.SetSimulated(simulateTouchOnDesktop);

        if (joystick == null)
            joystick = GetComponentInChildren<TouchJoystick>(true);

        if (joystick == null)
            Debug.LogWarning("MobileInput: no TouchJoystick found. Movement will be keyboard only.", this);

        ResolveControlsRoot();
    }

    void ResolveControlsRoot()
    {
        // Switching off our own GameObject would stop Update and unsubscribe us from
        // GameplayLockChanged, so the controls could never come back after the first
        // cutscene. Refuse that wiring rather than deadlock silently.
        if (controlsRoot == gameObject)
        {
            Debug.LogError("MobileInput: controlsRoot must be a CHILD, not this object. " +
                           "Falling back to the first child.", this);
            controlsRoot = null;
        }

        if (controlsRoot == null && transform.childCount > 0)
            controlsRoot = transform.GetChild(0).gameObject;

        if (controlsRoot == null)
            Debug.LogWarning("MobileInput: no controlsRoot. The touch controls will stay " +
                             "visible during cutscenes.", this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnEnable()
    {
        PlayerInputLock.GameplayLockChanged += OnLockChanged;
        ApplyVisibility(PlayerInputLock.IsLocked);
    }

    void OnDisable()
    {
        PlayerInputLock.GameplayLockChanged -= OnLockChanged;
        Clear();
    }

    void Update()
    {
        if (visible && joystick != null)
        {
            Horizontal = joystick.Horizontal;
            Vertical = joystick.Vertical;
        }
        else
        {
            Horizontal = 0f;
            Vertical = 0f;
        }

        Run = visible && runHeld;

        // Consume the queued tap. Queueing rather than latching a bool is what stops the
        // old bug where a tap made in mid-air stayed true forever and re-fired the jump
        // on every landing, because only a successful grounded jump ever cleared it.
        JumpPressed = visible && jumpQueued;
        jumpQueued = false;
    }

    // ------------------------------------------------------------------ button hooks

    /// <summary>Called by RunButton on press and release.</summary>
    public void SetRun(bool value) => runHeld = value;

    /// <summary>Called by JumpButton on press. Consumed on the next Update.</summary>
    public void QueueJump() => jumpQueued = true;

    // ------------------------------------------------------------------ visibility

    void OnLockChanged(bool locked) => ApplyVisibility(locked);

    void ApplyVisibility(bool locked)
    {
        visible = !locked && (!touchDevicesOnly || IsTouchDevice);

        // Switching the root off disables the stick, and TouchJoystick.OnDisable releases it,
        // so a cutscene that starts mid-drag cannot leave the tilt latched.
        if (controlsRoot != null && controlsRoot.activeSelf != visible)
            controlsRoot.SetActive(visible);

        if (!visible)
            Clear();
    }

    static bool IsTouchDevice => TouchInput.Active;

    void Clear()
    {
        Horizontal = 0f;
        Vertical = 0f;
        Run = false;
        runHeld = false;
        JumpPressed = false;
        jumpQueued = false;
    }

    // "Enter Play Mode Options -> Disable Domain Reload" keeps statics between sessions,
    // so a stale Instance from the previous run would point at a destroyed object.
    // Same guard PlayerInputLock uses.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
}

using UnityEngine;

[DisallowMultipleComponent]
public class InputBuffer : MonoBehaviour
{
    /* ───────────────────────────── Inspector ───────────────────────── */
    [Header("Control Profile")]
    public PlayerControlsProfile profile;    // must be unique per fighter

    /* ───────────────────────────── Frame Snapshot ───────────────────── */
    public struct Frame
    {
        /* world-relative directions */
        public bool Left, Right, Up, Down;

        /* local directions (toward / away) */
        public bool Forward, Back;

        /* held buttons */
        public bool Run, Block;

        /* per-frame edge triggers */
        public bool PressedHighPunch, PressedHighKick;
        public bool PressedLowPunch,  PressedLowKick;
        public bool PressedBlock,     PressedRun;
        public bool PressedUp;
        public bool PressedForward,   PressedBack;
    }

    public Frame State { get; private set; }
    Frame _prev;                    // previous-frame snapshot for edge tests

    /* double-tap back detection */
    float _lastBackTapTime;
    bool  _waitingSecondTap;

    /* ───────────────────────────── API ─────────────────────────────── */

    /// <summary>Erase the previous-frame snapshot (used at round reset).</summary>
    public void ClearPrev() => _prev = default;

    /// <summary>
    /// Capture hardware input. Call exactly once per *Update*.
    /// </summary>
    /// <param name="facingRight">Current facing; true = right.</param>
    public void Capture(bool facingRight)
    {
        Frame f = new Frame();

        /* 1 ─ raw axes (profile supplies axis names) */
        float h = 0f, v = 0f;

        if (!string.IsNullOrEmpty(profile.horizontalAxis))
            h = Input.GetAxisRaw(profile.horizontalAxis);

        if (!string.IsNullOrEmpty(profile.verticalAxis))
            v = Input.GetAxisRaw(profile.verticalAxis);

        /* fallback to dedicated keys if axis is idle */
        if (Mathf.Approximately(h, 0f))
        {
            if (profile.leftKey  != KeyCode.None && Input.GetKey(profile.leftKey))
                h = -1f;
            if (profile.rightKey != KeyCode.None && Input.GetKey(profile.rightKey))
                h =  1f;
        }
        if (Mathf.Approximately(v, 0f))
        {
            if (profile.upKey   != KeyCode.None && Input.GetKey(profile.upKey))
                v =  1f;
            if (profile.downKey != KeyCode.None && Input.GetKey(profile.downKey))
                v = -1f;
        }

        /* 2 ─ world-direction booleans */
        f.Left  = h < 0f;
        f.Right = h > 0f;
        f.Down  = v < 0f;
        f.Up    = v > 0f;

        /* 3 ─ local directions (relative to facing) */
        f.Forward =  facingRight ? f.Right : f.Left;
        f.Back    =  facingRight ? f.Left  : f.Right;

        /* 4 ─ held buttons */
        f.Run   = Input.GetKey(profile.runKey);
        f.Block = Input.GetKey(profile.blockKey);

        /* 5 ─ edge triggers (KeyDown OR axis edge) */
        f.PressedHighPunch = Input.GetKeyDown(profile.highPunchKey);
        f.PressedHighKick  = Input.GetKeyDown(profile.highKickKey);
        f.PressedLowPunch  = Input.GetKeyDown(profile.lowPunchKey);
        f.PressedLowKick   = Input.GetKeyDown(profile.lowKickKey);
        f.PressedBlock     = Input.GetKeyDown(profile.blockKey);
        f.PressedRun       = Input.GetKeyDown(profile.runKey);

        bool upKeyDown = profile.upKey != KeyCode.None &&
                         Input.GetKeyDown(profile.upKey);
        f.PressedUp    = upKeyDown || (f.Up && !_prev.Up);

        f.PressedForward = f.Forward && !_prev.Forward;
        f.PressedBack    = f.Back    && !_prev.Back;

        /* 6 ─ commit snapshot */
        _prev = f;
        State = f;
    }

    /// <summary>True if BACK tapped twice within 0.25 s (local back).</summary>
    public bool DoubleTappedBack()
    {
        if (State.PressedBack)
        {
            float now = Time.time;
            if (_waitingSecondTap && now - _lastBackTapTime < 0.25f)
            {
                _waitingSecondTap = false;
                return true;
            }
            _waitingSecondTap = true;
            _lastBackTapTime  = now;
        }
        return false;
    }
}

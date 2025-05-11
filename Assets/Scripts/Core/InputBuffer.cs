using UnityEngine;

[DisallowMultipleComponent]
public class InputBuffer : MonoBehaviour
{
    [Header("Control Profile")]
    public PlayerControlsProfile profile;

    // Snapshot of one frame’s inputs (world + local)
    public struct Frame
    {
        // world-relative
        public bool Left, Right, Up, Down;

        // local (toward / away)
        public bool Forward, Back;

        // held buttons
        public bool Run, Block;

        // edge triggers this frame
        public bool PressedHighPunch, PressedHighKick;
        public bool PressedLowPunch,  PressedLowKick;
        public bool PressedBlock,     PressedRun,     PressedUp;
        public bool PressedForward,   PressedBack;
    }

    public Frame State { get; private set; }
    Frame _prev;                         // previous frame for edge detection

    // double-tap back detection
    float _lastBackTapTime;
    bool  _waitingSecondTap;

    /// <summary>
    /// Sample current hardware input. Must be called once per Update before gameplay logic.
    /// Provide the fighter’s current facing (true = facing right).
    /// </summary>
    public void Capture(bool facingRight)
    {
        Frame f = new Frame();

        // 1) raw axis
        float h = Input.GetAxisRaw(profile.HorizontalAxis);
        float v = Input.GetAxisRaw(profile.VerticalAxis);

        f.Left  = h < 0f;
        f.Right = h > 0f;
        f.Down  = v < 0f;
        f.Up    = v > 0f;

        // 2) local mapping
        f.Forward =  facingRight ? f.Right : f.Left;
        f.Back    =  facingRight ? f.Left  : f.Right;

        // 3) held buttons
        f.Run   = Input.GetKey(profile.runKey);
        f.Block = Input.GetKey(profile.blockKey);

        // 4) edge (Down events)
        f.PressedHighPunch = Input.GetKeyDown(profile.highPunchKey);
        f.PressedHighKick  = Input.GetKeyDown(profile.highKickKey);
        f.PressedLowPunch  = Input.GetKeyDown(profile.lowPunchKey);
        f.PressedLowKick   = Input.GetKeyDown(profile.lowKickKey);
        f.PressedBlock     = Input.GetKeyDown(profile.blockKey);
        f.PressedRun       = Input.GetKeyDown(profile.runKey);
        f.PressedUp        = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);

        f.PressedForward = f.Forward && !_prev.Forward;
        f.PressedBack    = f.Back    && !_prev.Back;

        // store
        _prev  = f;
        State  = f;
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

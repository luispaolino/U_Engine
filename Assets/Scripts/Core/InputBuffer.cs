using UnityEngine;

[DisallowMultipleComponent]
public class InputBuffer : MonoBehaviour
{
    [Header("Control Profile")]
    public PlayerControlsProfile profile;

    public struct Frame
    {
        public bool Left, Right, Up, Down;
        public bool Run, Block;
        public bool PressedHighPunch, PressedHighKick;
        public bool PressedLowPunch,  PressedLowKick;
    }

    /// <summary>
    /// The most-recent sample of all inputs this frame.
    /// </summary>
    public Frame State { get; private set; }

    // For double-tap‐back detection
    float _lastBackTapTime;
    bool  _waitingForSecondTap;

    void Update()
    {
        var f = new Frame();

        // 1) Directional axes
        float h = Input.GetAxisRaw(profile.HorizontalAxis);
        float v = Input.GetAxisRaw(profile.VerticalAxis);
        f.Left  = h < 0;  f.Right = h > 0;
        f.Down  = v < 0;  f.Up    = v > 0;

        // 2) Hold-keys
        f.Run   = Input.GetKey(profile.runKey);
        f.Block = Input.GetKey(profile.blockKey);

        // 3) Tap-keys
        f.PressedHighPunch = Input.GetKeyDown(profile.highPunchKey);
        f.PressedHighKick  = Input.GetKeyDown(profile.highKickKey);
        f.PressedLowPunch  = Input.GetKeyDown(profile.lowPunchKey);
        f.PressedLowKick   = Input.GetKeyDown(profile.lowKickKey);

        State = f;
    }

    /// <summary>
    /// Returns true if “back” was tapped twice within 0.25 s.
    /// </summary>
    public bool DoubleTappedBack(bool facingRight)
    {
        bool back = facingRight ? State.Left : State.Right;
        if (back)
        {
            float now = Time.time;
            if (_waitingForSecondTap && now - _lastBackTapTime < 0.25f)
            {
                _waitingForSecondTap = false;
                return true;
            }
            _waitingForSecondTap = true;
            _lastBackTapTime     = now;
        }
        return false;
    }
}

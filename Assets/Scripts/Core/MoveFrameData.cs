using UnityEngine;

public enum ReactionType
{
    None,
    HitHigh,
    HitLow,
    SweepKD,
    Popup,
    Knockback
}

[System.Serializable]
public struct MoveFrameData
{
    public string      tag;          // e.g. "HighPunch", "SweepKick"
    public int         startUp;      // frames before first active
    public int         active;       // number of active frames
    public int         recovery;     // frames after last active
    public int         damage;       // pixel damage or %×10
    public ReactionType reaction;    // how victim reacts
    public float       pushX;        // horizontal velocity applied
    public float       pushY;        // vertical velocity applied
    public bool        knockDown;    // does it sweep/knockdown?
    public bool        unblockable;  // cannot be blocked?
}

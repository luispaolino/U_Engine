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

/// <summary>
/// Frame data for one normal / special / throw.
/// Stored inside a larger MoveTableSO as before.
/// </summary>
[System.Serializable]
public struct MoveFrameData
{
    public string       tag;           // "HighPunch", "SweepKick", …
    public int          startUp;       // frames before hit can connect
    public int          active;        // active frames
    public int          recovery;      // post-swing frames
    public int          damage;        // life to subtract (or 10×%)
    public ReactionType reaction;      // victim reaction type
    public float        pushX;         // horizontal knock-back
    public float        pushY;         // vertical knock-back
    public bool         knockDown;     // true = sweeps / KD
    public bool         unblockable;   // true = ignores guard

    // ── Added for PSX-style block system ─────────────────────
    public bool         noChip;        // true = does 0 chip on block
    public AttackPower  power;         // Light / Medium / Heavy / Special
}

/// <summary>
/// Same four levels the PSX tables use; indices 0-3.
/// </summary>
/*public enum AttackPower
{
    Light   = 0,
    Medium  = 1,
    Heavy   = 2,
    Special = 3
}*/

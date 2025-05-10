using UnityEngine;

[System.Serializable]
public struct ReactionFrameData
{
    public ReactionType reaction;     // Which reaction
    public int          hitStun;      // frames of hit‑stun
    public int          blockStun;    // frames of block‑stun
    public int          airStun;      // frames airborne (popup/juggle)
    public int          knockdownDelay; // frames lying before get‑up
}

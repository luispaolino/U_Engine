using UnityEngine;

[System.Serializable]
public struct ThrowData
{
    public string tag;            // "Throw_Fwd" or "Throw_Rev"
    public int    startUp;        // frames before grab connects
    public int    execute;        // frames holding victim
    public int    recovery;       // frames after release
    public int    damageFull;     // damage when not escaped
    public int    damageSoft;     // damage if tech‑escape
    public int    knockdownDelay; // frames lying after slam
    public int    escapeWindow;   // frames to input tech (2f arcade)
    public Vector2 tossVelocity;  // X,Y push when released
}

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FightingGame/Collider Clip 2D", fileName = "New Collider Clip 2D")]
public class ColliderClip2D : ScriptableObject
{
    public AnimationClip clip;               // the animation this data belongs to
    public float frameRate = 60f;            // editable if you authored the clip at a custom FPS
    public List<Frame> frames = new();       // one entry PER visual frame

    [Serializable] public class Box
    {
        public enum Kind { Hurt, Hit, Push }
        public Kind kind = Kind.Hurt;
        public Vector2 size  = Vector2.one;
        public Vector2 offset = Vector2.zero; // local space (xy of the character’s root)
    }

    [Serializable] public class Frame { public List<Box> boxes = new(); }

    // Utility – get the frame that matches a time value (secs) -----------------
    public Frame GetFrame(float time)
    {
        if (!clip || frames.Count == 0) return null;
        int i = Mathf.Clamp(Mathf.FloorToInt(time * frameRate), 0, frames.Count - 1);
        return frames[i];
    }
}

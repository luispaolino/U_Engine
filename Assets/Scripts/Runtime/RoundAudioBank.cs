using UnityEngine;

public enum GameEvent
{
    RoundOne, RoundTwo, RoundThree,
    Fight, FinishHim, FinishHer,
    Flawless, Wins,
    LastHitWarning, Last10SecTick,
    Name_Scorpion
}

[CreateAssetMenu(menuName = "UMK3/Audio/Round Audio Bank")]
public class RoundAudioBank : ScriptableObject
{
    [System.Serializable]
    public struct ClipRef
    {
        public GameEvent evt;
        public string    resourcesPath; // e.g. "Arcade/ShaoKahn/mk3-09020"
    }

    public AudioClip hitClip;
    public AudioClip blockClip;
    public ClipRef[] clips;

    AudioClip _Find(GameEvent e)
    {
        foreach (var c in clips)
            if (c.evt == e)
                return Resources.Load<AudioClip>(c.resourcesPath);
        Debug.LogWarning($"No clip for {e}");
        return null;
    }

    public void Play(AudioSource src, GameEvent e, float vol = 1f)
    {
        var clip = _Find(e);
        if (clip != null)
            src.PlayOneShot(clip, vol);
    }
}

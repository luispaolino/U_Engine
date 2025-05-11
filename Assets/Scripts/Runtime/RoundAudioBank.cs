using UnityEngine;

[CreateAssetMenu(fileName = "RoundAudioBank", menuName = "UMK3/Round Audio Bank")]
public class RoundAudioBank : ScriptableObject
{
    /* ----------------------------------------------------------
     *  Generic combat S-FX (landed hit & blocked hit)
     * ---------------------------------------------------------- */
    public AudioClip hitClip;
    public AudioClip blockClip;

    /* ----------------------------------------------------------
     *  Announcer VO table — kept under the original name
     *  so DefaultUMK3DataGenerator.cs continues to compile.
     * ---------------------------------------------------------- */
    [System.Serializable]
    public struct ClipRef          // <-- original name expected by generator
    {
        public GameEvent evt;
        public string    resourcesPath;   // optional Resources.Load fallback
        public AudioClip clip;            // direct reference
        [Range(0f,1f)] public float volume;
    }

    public ClipRef[] clips;               // <-- original array name

    /* ----------------------------------------------------------
     *  Runtime play helper
     * ---------------------------------------------------------- */
    public void Play(AudioSource src, GameEvent e, float overrideVol = 1f)
    {
        foreach (var c in clips)
        {
            if (c.evt != e) continue;

            AudioClip clipToPlay = c.clip;
            if (!clipToPlay && !string.IsNullOrEmpty(c.resourcesPath))
                clipToPlay = Resources.Load<AudioClip>(c.resourcesPath);

            if (clipToPlay)
            {
                src.PlayOneShot(clipToPlay, c.volume * overrideVol);
            }
            else
            {
                Debug.LogWarning($"RoundAudioBank: no clip for {e}");
            }
            return;
        }
        Debug.LogWarning($"RoundAudioBank: event {e} not found");
    }
}

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioRoundManager : MonoBehaviour
{
    public static AudioRoundManager I { get; private set; }
    public RoundAudioBank bank;
    AudioSource src;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
    }

    public static void Play(GameEvent e, float vol = 1f)
    {
        if (I != null)
            //Debug.Log($"Play({e})"); 
            I.bank.Play(I.src, e, vol);
    }
}

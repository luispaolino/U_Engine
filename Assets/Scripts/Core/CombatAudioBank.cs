// Assets/Scripts/Data/CombatAudioBank.cs
using UnityEngine;

[CreateAssetMenu(fileName = "CombatAudioBank", menuName = "UMK3/Combat Audio Bank")]
public class CombatAudioBank : ScriptableObject
{
    public AudioClip hitClip;
    public AudioClip blockClip;
}

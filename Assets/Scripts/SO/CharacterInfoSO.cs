// CharacterInfoSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "UMK3/Character Information", fileName = "NewCharacterInfo")]
public class CharacterInfoSO : ScriptableObject
{
    public string characterName = "Fighter";
    public AudioClip nameAudioClip; // Announcer saying the character's name
    public Gender gender = Gender.Male; // If you use this for "Finish Him/Her"

    // You can add more here later:
    // public Sprite portrait;
    // public GameObject fullCharacterModelPrefabForSelectionScreen;
    // public string bio;
}

// You already have this enum, ensure it's accessible (e.g. in a shared Enums.cs or UMK3 namespace)
// public enum Gender { Male, Female, Other }
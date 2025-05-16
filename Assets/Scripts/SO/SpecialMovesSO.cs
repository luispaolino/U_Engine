// SpecialMovesSO.cs
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Make sure AttackPower enum and MoveFrameData struct are accessible here
// (e.g., defined globally, in UMK3 namespace, or in this file)
// public enum AttackPower { Light, Medium, Heavy, Special } defined elsewhere (e.g. FighterCharacterCore)
// public struct MoveFrameData { ... } defined elsewhere (e.g. with BasicMovesSO or globally)


[CreateAssetMenu(menuName = "UMK3/Character Special Moves List", fileName = "NewSpecialMovesList")]
public class SpecialMovesSO : ScriptableObject
{
    [Tooltip("List of actions and special moves with their frame data.")]
    public MoveFrameData[] moves;
    
    public bool TryGetMoveData(string tag, out MoveFrameData data)
    {
        if (moves == null || moves.Length == 0)
        {
            data = default;
            return false;
        }
        foreach (var m in moves)
        {
            if (m.tag == tag)
            {
                data = m;
                return true;
            }
        }
        data = default;
        return false;
    }
}

#if UNITY_EDITOR
public static class SpecialMovesSOMenu
{
    [MenuItem("Assets/Create/UMK3/Character Special Moves List")]
    static void CreateSpecialMovesAsset()
    {
        SpecialMovesSO asset = ScriptableObject.CreateInstance<SpecialMovesSO>();
        string path = "Assets/Data/CharacterMoves";
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path + "/NewSpecialMovesList.asset"));
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
}
#endif
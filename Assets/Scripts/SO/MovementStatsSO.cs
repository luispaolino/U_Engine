// MovementStatsSO.cs (Renamed and refactored from BasicMovesSO)
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "UMK3/Character Movement Stats", fileName = "NewMovementStats")]
public class MovementStatsSO : ScriptableObject
{
    [Header("Walking")]
    public float walkForwardVelocity = 3.5f;
    public float walkBackVelocity = 2.5f;

    [Header("Running")]
    public float runVelocity = 6.0f;

    [Header("Jumping")]
    public float jumpUpVelocity = 7.0f;
    public float jumpForwardHorizontalVelocity = 6.0f;
    public float jumpBackHorizontalVelocity = 5.0f;
    public int jumpStartupFrames = 7;
    
    [Header("Physics")]
    public float gravityPerSecond = 25.0f;
}

#if UNITY_EDITOR
public static class MovementStatsSOMenu
{
    [MenuItem("Assets/Create/UMK3/Character Movement Stats")]
    static void CreateMovementStatsAsset()
    {
        MovementStatsSO asset = ScriptableObject.CreateInstance<MovementStatsSO>();
        // Simplified path creation
        string path = "Assets/Data/CharacterStats";
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path + "/NewMovementStats.asset"));
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
}
#endif
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "UMK3/Move Table", fileName = "BasicMoves")]
public class MoveTableSO : ScriptableObject
{
    public MoveFrameData[] moves;

    public bool TryGet(string tag, out MoveFrameData data)
    {
        foreach (var m in moves)
            if (m.tag == tag)
            {
                data = m;
                return true;
            }
        data = default;
        return false;
    }
}

#if UNITY_EDITOR
public static class MoveTableMenu
{
    [MenuItem("Assets/UMK3/Create Blank Move Table")]
    static void CreateBlank()
    {
        var asset = ScriptableObject.CreateInstance<MoveTableSO>();
        AssetDatabase.CreateAsset(asset, "Assets/Data/BasicMoves.asset");
        AssetDatabase.SaveAssets();
    }
}
#endif

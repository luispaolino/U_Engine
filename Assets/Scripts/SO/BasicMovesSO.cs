using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "UMK3/Move Table", fileName = "BasicMoves")]
public class BasicMovesSO : ScriptableObject
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

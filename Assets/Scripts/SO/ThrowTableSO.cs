using UnityEngine;

[CreateAssetMenu(menuName="UMK3/Throw Table", fileName="ThrowTable")]
public class ThrowTableSO : ScriptableObject
{
    public ThrowData[] throws;

    public bool TryGet(string tag, out ThrowData td)
    {
        foreach (var t in throws)
            if (t.tag == tag)
            {
                td = t;
                return true;
            }
        td = default;
        return false;
    }
}

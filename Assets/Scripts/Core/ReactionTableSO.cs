using UnityEngine;

[CreateAssetMenu(menuName = "UMK3/Reaction Table", fileName = "ReactionTable")]
public class ReactionTableSO : ScriptableObject
{
    public ReactionFrameData[] reactions;

    public bool TryGet(ReactionType t, out ReactionFrameData data)
    {
        foreach (var r in reactions)
            if (r.reaction == t)
            {
                data = r;
                return true;
            }
        data = default;
        return false;
    }
}

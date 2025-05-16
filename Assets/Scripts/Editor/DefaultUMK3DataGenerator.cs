// File: Assets/Scripts/Editor/DefaultUMK3DataGenerator.cs
// Place this script under an "Editor" folder. Use the menu to generate default UMK3 data assets.

using UnityEngine;
using UnityEditor;

public static class DefaultUMK3DataGenerator
{
    [MenuItem("UMK3/Generate Default Data SOs")]
    public static void GenerateAll()
    {
        CreateMoveTable();
        CreateReactionTable();
        CreateThrowTable();
        CreateRoundAudioBank();
        AssetDatabase.SaveAssets();
        Debug.Log("UMK3: Default data ScriptableObjects generated in Assets/Data/");
    }

    static void CreateMoveTable()
    {
        var path = "Assets/Data/BasicMoves.asset";
        var asset = AssetDatabase.LoadAssetAtPath<BasicMovesSO>(path) ??
                    ScriptableObject.CreateInstance<BasicMovesSO>();

        asset.moves = new MoveFrameData[]
        {
            new MoveFrameData { tag="HighPunch",   startUp=5,  active=3, recovery=9,  damage=5,  reaction=ReactionType.HitHigh, pushX=1f, pushY=0f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="LowPunch",    startUp=3,  active=3, recovery=8,  damage=4,  reaction=ReactionType.HitLow,  pushX=1f, pushY=0f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="HighKick",    startUp=7,  active=3, recovery=12, damage=10, reaction=ReactionType.HitHigh, pushX=1.5f, pushY=0f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="LowKick",     startUp=5,  active=3, recovery=10, damage=8,  reaction=ReactionType.HitLow,  pushX=1f, pushY=0f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="SweepKick",   startUp=9,  active=4, recovery=18, damage=12, reaction=ReactionType.SweepKD, pushX=0f, pushY=0f, knockDown=true, unblockable=false },
            new MoveFrameData { tag="Uppercut",    startUp=8,  active=4, recovery=22, damage=15, reaction=ReactionType.Popup,   pushX=0f, pushY=5f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="Roundhouse",  startUp=10, active=5, recovery=24, damage=18, reaction=ReactionType.Knockback, pushX=0f, pushY=0f, knockDown=false, unblockable=false },
            new MoveFrameData { tag="BackDash",    startUp=0,  active=4, recovery=8,  damage=0,  reaction=ReactionType.None,    pushX=-3f, pushY=0f, knockDown=false, unblockable=true  },
            new MoveFrameData { tag="WakeupRoll",  startUp=4,  active=6, recovery=10, damage=0,  reaction=ReactionType.None,    pushX=2f, pushY=0f, knockDown=false, unblockable=true  },
            new MoveFrameData { tag="JumpStart",   startUp=4,  active=0, recovery=0,  damage=0,  reaction=ReactionType.None,    pushX=0f, pushY=0f, knockDown=false, unblockable=true  }
        };

        AssetDatabase.CreateAsset(asset, path);
    }

    static void CreateReactionTable()
    {
        var path = "Assets/Data/ReactionTable.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ReactionTableSO>(path) ??
                    ScriptableObject.CreateInstance<ReactionTableSO>();

        asset.reactions = new ReactionFrameData[]
        {
            new ReactionFrameData { reaction=ReactionType.None,      hitStun=0,  blockStun=0,  airStun=0,  knockdownDelay=0 },
            new ReactionFrameData { reaction=ReactionType.HitHigh,   hitStun=7,  blockStun=4,  airStun=0,  knockdownDelay=0 },
            new ReactionFrameData { reaction=ReactionType.HitLow,    hitStun=6,  blockStun=4,  airStun=0,  knockdownDelay=0 },
            new ReactionFrameData { reaction=ReactionType.SweepKD,   hitStun=10, blockStun=0,  airStun=0,  knockdownDelay=24 },
            new ReactionFrameData { reaction=ReactionType.Popup,     hitStun=16, blockStun=0,  airStun=20, knockdownDelay=0 },
            new ReactionFrameData { reaction=ReactionType.Knockback,hitStun=19, blockStun=0,  airStun=0,  knockdownDelay=0 }
        };

        AssetDatabase.CreateAsset(asset, path);
    }

    static void CreateThrowTable()
    {
        var path = "Assets/Data/ThrowTable.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ThrowTableSO>(path) ??
                    ScriptableObject.CreateInstance<ThrowTableSO>();

        asset.throws = new ThrowData[]
        {
            new ThrowData { tag="Throw_Fwd",    startUp=4, execute=14, recovery=16, damageFull=18, damageSoft=6, knockdownDelay=24, escapeWindow=2, tossVelocity=new Vector2(3f,0f) },
            new ThrowData { tag="Throw_Rev",    startUp=4, execute=14, recovery=16, damageFull=18, damageSoft=6, knockdownDelay=24, escapeWindow=2, tossVelocity=new Vector2(-3f,0f) }
        };

        AssetDatabase.CreateAsset(asset, path);
    }

    static void CreateRoundAudioBank()
    {
        var path = "Assets/Data/RoundAudioBank.asset";
        var asset = AssetDatabase.LoadAssetAtPath<RoundAudioBank>(path) ??
                    ScriptableObject.CreateInstance<RoundAudioBank>();

        asset.clips = new RoundAudioBank.ClipRef[]
        {
            new RoundAudioBank.ClipRef { evt=GameEvent.RoundOne,      resourcesPath="Arcade/ShaoKahn/mk3-09025" },
            new RoundAudioBank.ClipRef { evt=GameEvent.RoundTwo,      resourcesPath="Arcade/ShaoKahn/mk3-09030" },
            new RoundAudioBank.ClipRef { evt=GameEvent.RoundThree,    resourcesPath="Arcade/ShaoKahn/mk3-09035" },
            new RoundAudioBank.ClipRef { evt=GameEvent.Fight,         resourcesPath="Arcade/ShaoKahn/mk3-09020" },
            new RoundAudioBank.ClipRef { evt=GameEvent.FinishHim,     resourcesPath="Arcade/ShaoKahn/mk3-09000" },
            new RoundAudioBank.ClipRef { evt=GameEvent.FinishHer,     resourcesPath="Arcade/ShaoKahn/mk3-09005" },
            new RoundAudioBank.ClipRef { evt=GameEvent.Flawless,      resourcesPath="Arcade/ShaoKahn/mk3-09015" },
            new RoundAudioBank.ClipRef { evt=GameEvent.Wins,          resourcesPath="Arcade/ShaoKahn/mk3-09145" },
            new RoundAudioBank.ClipRef { evt=GameEvent.LastHitWarning,resourcesPath="Arcade/UI/mk3-01030" },
            new RoundAudioBank.ClipRef { evt=GameEvent.Last10SecTick, resourcesPath="Arcade/UI/mk3-01060" },
            new RoundAudioBank.ClipRef { evt=GameEvent.Name_Scorpion, resourcesPath="Arcade/Names/mk3-21120" }
        };

        AssetDatabase.CreateAsset(asset, path);
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

/// <summary>
/// Attach this to a GameObject (e.g. your Fighter prefab).  
/// In the Inspector you’ll see slots for all clips + the target controller.
/// Click the gear menu and choose “Rebuild Animator” to auto-generate
/// the full state machine—including locomotion, attacks, throws, reactions,
/// knockdown and get-up—wired to parameters and triggers.
/// </summary>
[DisallowMultipleComponent]
public class AnimatorAutoBuilder : MonoBehaviour
{
    [Header("Controller & Clips")]
    public AnimatorController targetController;

    [Header("Locomotion Clips")]
    public AnimationClip idle;
    public AnimationClip walkFwd;
    public AnimationClip runFwd;
    public AnimationClip crouchIdle;
    public AnimationClip jumpStart;
    public AnimationClip jumpLoop;
    public AnimationClip jumpLand;

    [Header("Dash & Roll")]
    public AnimationClip backDash;
    public AnimationClip wakeRoll;

    [Header("Normal Attacks")]
    public AnimationClip highPunch;
    public AnimationClip lowPunch;
    public AnimationClip highKick;
    public AnimationClip lowKick;
    public AnimationClip sweepKick;
    public AnimationClip uppercut;
    public AnimationClip roundhouse;

    [Header("Throws")]
    public AnimationClip throwStart;
    public AnimationClip throwDo;
    public AnimationClip thrownStart;
    public AnimationClip thrownSlam;
    public AnimationClip thrownEscape;

    [Header("Reactions & Other")]
    public AnimationClip hitReactStand;
    public AnimationClip hitReactAir;
    public AnimationClip blockHigh;
    public AnimationClip blockLow;
    public AnimationClip victoryPose;
    public AnimationClip defeatedIdle;
    public AnimationClip dizzyLoop;

    [Header("Knockdown/GetUp")]
    public AnimationClip knockdownClip;
    public AnimationClip getUpClip;

#if UNITY_EDITOR
    [ContextMenu("Rebuild Animator (Editor Only)")]
    void Build()
    {
        if (targetController == null)
        {
            Debug.LogError("Assign a target AnimatorController before rebuilding.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(targetController, "Rebuild Animator");

        // 1) Clear existing layers & parameters
        targetController.layers     = new AnimatorControllerLayer[0];
        targetController.parameters = new AnimatorControllerParameter[0];

        // 2) Add parameters
        AddBool("FacingRight");
        AddBool("IsWalking");  AddBool("IsRunning");
        AddBool("IsCrouching");AddBool("IsJumping");
        AddBool("IsBlocking"); AddBool("IsDizzy");
        AddFloat("MoveSpeed");

        // Attack & state triggers
        string[] triggers = {
            "Attack_HighPunch","Attack_LowPunch","Attack_HighKick","Attack_LowKick",
            "Attack_SweepKick","Attack_Uppercut","Attack_Roundhouse",
            "JumpPunch_U","JumpPunch_F","JumpPunch_B",
            "JumpKick_U","JumpKick_F","JumpKick_B",
            "Throw_Start","Throw_Do","Thrown_Start","Thrown_Slam","Thrown_Escape",
            "HitReact","Knockdown","GetUp","Victory","Defeated","BackDash","WakeRoll"
        };
        foreach (var t in triggers) AddTrigger(t);

        // 3) Create base layer
        var layer = new AnimatorControllerLayer {
            name         = "Base",
            stateMachine = new AnimatorStateMachine(),
            defaultWeight= 1
        };
        targetController.AddLayer(layer);
        AssetDatabase.AddObjectToAsset(layer.stateMachine, targetController);
        var sm = layer.stateMachine;

        // 4) Locomotion blend-tree
        var bt = new BlendTree {
            name           = "Locomotion",
            blendParameter = "MoveSpeed"
        };
        bt.AddChild(idle,    0f);
        bt.AddChild(walkFwd, 0.5f);
        bt.AddChild(runFwd,  3f);
        AssetDatabase.AddObjectToAsset(bt, targetController);
        var locom = sm.AddState("Locomotion");
        locom.motion = bt;

        // 5) Looping states
        sm.AddState("Crouch_Idle").motion = crouchIdle;
        sm.AddState("Jump_Loop").motion   = jumpLoop;
        sm.AddState("Lying").motion       = dizzyLoop;
        sm.AddState("Dizzy").motion       = dizzyLoop;
        sm.AddState("BlockHigh").motion   = blockHigh;
        sm.AddState("BlockLow").motion    = blockLow;
        sm.AddState("Victory").motion     = victoryPose;
        sm.AddState("Defeated").motion    = defeatedIdle;

        // 6) One-shot attacks
        AddOneShot(sm, "HighPunch",  highPunch);
        AddOneShot(sm, "LowPunch",   lowPunch);
        AddOneShot(sm, "HighKick",   highKick);
        AddOneShot(sm, "LowKick",    lowKick);
        AddOneShot(sm, "SweepKick",  sweepKick);
        AddOneShot(sm, "Uppercut",   uppercut);
        AddOneShot(sm, "Roundhouse", roundhouse);

        // Dash & roll
        AddOneShot(sm, "BackDash", backDash, exitToLocomotion:false);
        AddOneShot(sm, "WakeRoll", wakeRoll, exitToLocomotion:false);

        // Hit react
        sm.AddState("HitReactAir").motion = hitReactAir;
        AddOneShot(sm, "HitReact", hitReactStand, exitToLocomotion:false);

        // 7) Throws
        var ts = AddOneShot(sm, "Throw_Start", throwStart, exitToLocomotion:false);
        var td = AddOneShot(sm, "Throw_Do",    throwDo,    exitToLocomotion:false);
        ts.AddTransition(td).hasExitTime = true;
        AddOneShot(sm, "Thrown_Start",  thrownStart, exitToLocomotion:false);
        AddOneShot(sm, "Thrown_Slam",   thrownSlam,  exitToLocomotion:false);
        AddOneShot(sm, "Thrown_Escape", thrownEscape,exitToLocomotion:false);

        // 8) Knockdown & GetUp
        AddOneShot(sm, "Knockdown", knockdownClip, exitToLocomotion:false);
        AddOneShot(sm, "GetUp",     getUpClip,     exitToLocomotion:true);

        // 9) AnyState → triggers
        foreach (var state in sm.states)
        {
            var name = state.state.name;
            if (name.StartsWith("Attack_") ||
                name=="BackDash" || name=="WakeRoll" ||
                name.StartsWith("Throw_") ||
                name=="HitReact"|| name=="Knockdown"|| name=="GetUp")
            {
                var trans = sm.AddAnyStateTransition(state.state);
                trans.AddCondition(AnimatorConditionMode.If, 0, name);
                trans.hasExitTime = false;
                trans.duration    = 0.05f;
            }
        }

        EditorUtility.SetDirty(targetController);
        AssetDatabase.SaveAssets();
        Debug.Log("AnimatorAutoBuilder: rebuild complete!");
    }

    // Helpers to add parameters
    void AddBool(string name)    => targetController.AddParameter(name,    AnimatorControllerParameterType.Bool);
    void AddFloat(string name)   => targetController.AddParameter(name,    AnimatorControllerParameterType.Float);
    void AddTrigger(string name) => targetController.AddParameter(name,    AnimatorControllerParameterType.Trigger);

    // Helper to create one-shot states
    AnimatorState AddOneShot(
        AnimatorStateMachine sm,
        string               stateName,
        AnimationClip        clip,
        bool                 exitToLocomotion = true)
    {
        var s = sm.AddState(stateName);
        s.motion = clip;
        s.writeDefaultValues = true;

        if (exitToLocomotion)
        {
            var exit = s.AddExitTransition();
            exit.hasExitTime = true;
            exit.exitTime    = 1f;
            exit.duration    = 0f;
        }
        return s;
    }
#endif
}

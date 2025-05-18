using UnityEngine;
// Make sure ReactionType and AttackPower enums are accessible here.
// If they are in a namespace (e.g., UMK3), add:
// using UMK3; 
// Or if they are global (like in your provided MoveFrameData code snippet), no 'using' is needed for them.

public class DebugDamageP2 : MonoBehaviour
{
    [Tooltip("Reference to the RoundSystem to find Players.")]
    public RoundSystem roundSystem;

    [Tooltip("Amount of damage to apply with each key press.")]
    public int damageToApply = 50;

    [Tooltip("Key to press to apply damage to Player 2.")]
    public KeyCode damageKey = KeyCode.D;

    [Tooltip("The reaction type to use for this debug hit.")]
    public ReactionType debugHitReaction = ReactionType.HitHigh; // CHANGED: Default to an actual enum value

    [Tooltip("The attack power to simulate for this debug hit.")]
    public AttackPower debugHitPower = AttackPower.Medium; // ADDED: For completeness

    void Update()
    {
        if (Input.GetKeyDown(damageKey))
        {
            ApplyDamageToPlayer2();
        }
    }

    void ApplyDamageToPlayer2()
    {
        if (roundSystem == null)
        {
            Debug.LogError("DebugDamageP2: RoundSystem reference not set in the Inspector!");
            return;
        }

        FighterCharacter player2_victim = roundSystem.Player2;
        FighterCharacter player1_attacker = roundSystem.Player1; 

        if (player2_victim == null || player2_victim.core == null)
        {
            Debug.LogWarning("DebugDamageP2: Player 2 (victim) or its core is not available.");
            return;
        }

        if (player2_victim.core.Health <= 0 && 
            player2_victim.core.State != CharacterState.MercyReceiving && 
            player2_victim.core.State != CharacterState.FinishHimVictim)
        {
            Debug.Log("DebugDamageP2: Player 2 is already truly KO'd. No damage applied.");
            return;
        }
        
        Debug.Log($"DebugDamageP2: Applying {damageToApply} debug damage to Player 2 (Health: {player2_victim.core.Health}) with reaction: {debugHitReaction}.");

        // Create dummy MoveFrameData for the debug hit
        MoveFrameData debugHitData = new MoveFrameData
{
    tag          = "DebugHit",
    startUp      = 2,       // e.g. 2 frames of wind-up
    active       = 1,       // 1 frame where the hit actually lands
    recovery     = 8,       // 8 frames to recover
    damage       = damageToApply,

    // knockDown when this blow should sweep them
    reaction     = debugHitReaction,
    knockDown    = (player2_victim.core.Health - damageToApply <= 0)
                  || debugHitReaction == ReactionType.SweepKD
                  || debugHitReaction == ReactionType.Popup
                  || debugHitReaction == ReactionType.Knockback,

    power        = debugHitPower,    // Light/Medium/Heavy/Special
    pushX        = (debugHitReaction == ReactionType.Knockback ||  
                    debugHitReaction == ReactionType.Popup) ? 1f : 0.5f,
    pushY        = (debugHitReaction == ReactionType.Popup) ? 3f
                 : (debugHitReaction == ReactionType.Knockback) ? 1f : 0.2f,

    unblockable  = true,
    noChip       = true,

    // if you added these flags to your struct:
    canMoveDuringStartUp  = false,
    canMoveDuringActive   = false,
};

        player2_victim.ReceiveHit(player1_attacker, debugHitData);


        Debug.Log($"DebugDamageP2: Player 2 health after: {player2_victim.core.Health}, State: {player2_victim.core.State}, Velocity: {player2_victim.core.Velocity}");
    }
}
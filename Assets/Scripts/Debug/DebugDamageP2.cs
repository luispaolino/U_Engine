// In DebugDamageP2.cs

using UnityEngine;

public class DebugDamageP2 : MonoBehaviour
{
    [Tooltip("Reference to the RoundSystem to find Player 2.")]
    public RoundSystem roundSystem; // Assign your RoundSystem GameObject here

    [Tooltip("Amount of damage to apply with each key press.")]
    public int damageToApply = 50;

    [Tooltip("Key to press to apply damage to Player 2.")]
    public KeyCode damageKey = KeyCode.D;

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

        // Use the new public property Player2 from RoundSystem
        FighterCharacter player2 = roundSystem.Player2; // <<<< CHANGE HERE

        if (player2 == null) // <<<< CHANGE HERE
        {
            Debug.LogWarning("DebugDamageP2: Player 2 (via roundSystem.Player2) is not available or not yet instantiated.");
            return;
        }

        if (player2.core == null) // <<<< CHANGE HERE
        {
            Debug.LogWarning("DebugDamageP2: Player 2's core is not initialized.");
            return;
        }

        if (player2.core.Health <= 0) // <<<< CHANGE HERE
        {
            Debug.Log("DebugDamageP2: Player 2 is already KO'd. No damage applied.");
            return;
        }
        
        Debug.Log($"DebugDamageP2: Applying {damageToApply} damage to Player 2 (current health: {player2.core.Health})."); // <<<< CHANGE HERE
        
        player2.core.TakeDamage(damageToApply); // <<<< CHANGE HERE

        Debug.Log($"DebugDamageP2: Player 2 health after damage: {player2.core.Health}"); // <<<< CHANGE HERE
    }
}
using UnityEngine;

public class HitboxDamage : MonoBehaviour
{
    // public int damageAmount; // This field is likely now redundant if damage is driven by MoveFrameData.
                                // You can keep it if you have a use for hitbox-specific damage modifiers,
                                // but the primary damage should come from the MoveFrameData.

    void OnTriggerEnter2D(Collider2D other)
    {
        FighterCharacter victim = other.GetComponentInParent<FighterCharacter>();
        FighterCharacter attacker = GetComponentInParent<FighterCharacter>(); // The owner of this hitbox

        // Basic validation: ensure we have a victim and an attacker, and they are not the same character.
        // Also, ensure the attacker's core and victim's core exist and victim is not paused.
        if (victim == null || attacker == null || victim == attacker ||
            attacker.core == null || victim.core == null || victim.core.IsPaused)
        {
            return;
        }

        // Check if the attacker is in an appropriate state to deal damage
        // and that their current move data is valid.
        if ((attacker.core.State == CharacterState.Attacking || attacker.core.State == CharacterState.BackDash) && // BackDash might also have hitboxes
             attacker.core.Phase == MovePhase.Active &&  // Damage usually only occurs during the active phase
             attacker.core.IsMoveDataValid)             // Ensure CurrentMove is set and valid
        {
            MoveFrameData currentAttackData = attacker.core.CurrentMove;

            // Now call ReceiveHit with both the attacker and their current move's data.
            victim.ReceiveHit(attacker, currentAttackData);

            // Optional: Disable this specific hitbox after it connects to prevent
            // it from hitting multiple times during a single active phase of an attack.
            // This is a common requirement in fighting games.
            // For example:
            // gameObject.SetActive(false);
            // or if you want to reuse it later without re-enabling the GameObject:
            // GetComponent<Collider2D>().enabled = false;
            // You would then re-enable it when the attack starts again (e.g., in an animation event or StartAttack).
        }
    }
}
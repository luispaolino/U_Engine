using UnityEngine;

public class HitboxToggle : MonoBehaviour
{
    Collider2D col;
    // FighterCharacter owner; // 'owner' is not strictly needed if this script only toggles

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("HitboxToggle: No Collider2D found on this GameObject!", this);
            enabled = false; // Disable script if no collider to toggle
            return;
        }
        // owner = GetComponentInParent<FighterCharacter>(); // Only needed if you have other logic requiring the owner
    }

    // Called by animation events to enable the hitbox
    public void Enable()
    {
        if (col != null)
            col.enabled = true;
    }

    // Called by animation events to disable the hitbox
    public void Disable()
    {
        if (col != null)
            col.enabled = false;
    }

    // Remove this method entirely if HitboxDamage.cs handles hit processing:
    /*
    void OnTriggerEnter2D(Collider2D other)
    {
        // This logic should now be handled by HitboxDamage.cs
        // var foe = other.GetComponentInParent<FighterCharacter>();
        // if (foe && foe != owner) // 'owner' would need to be defined if keeping this
        //    foe.ReceiveHit(owner); // THIS IS THE LINE CAUSING THE ERROR
    }
    */
}
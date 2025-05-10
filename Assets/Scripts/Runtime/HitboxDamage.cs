using UnityEngine;

public class HitboxDamage : MonoBehaviour
{
    public int damageAmount;

    void OnTriggerEnter2D(Collider2D other)
    {
        var victim = other.GetComponentInParent<FighterCharacter>();
        if (victim)
            victim.ReceiveHit(GetComponentInParent<FighterCharacter>());
    }
}

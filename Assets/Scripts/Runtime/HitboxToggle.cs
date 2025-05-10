using UnityEngine;

public class HitboxToggle : MonoBehaviour
{
    Collider2D col;
    FighterCharacter owner;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        owner = GetComponentInParent<FighterCharacter>();
    }

    public void Enable()  => col.enabled = true;
    public void Disable() => col.enabled = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        var foe = other.GetComponentInParent<FighterCharacter>();
        if (foe && foe != owner)
            foe.ReceiveHit(owner);
    }
}

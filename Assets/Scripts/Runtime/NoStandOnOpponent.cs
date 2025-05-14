using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NoStandOnOpponent : MonoBehaviour
{
    Collider2D       myCol;
    HashSet<Collider2D> ignored = new HashSet<Collider2D>();

    void Awake()
    {
        myCol = GetComponent<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D col)     => HandleCollision(col, true);
    void OnCollisionStay2D(Collision2D col)      => HandleCollision(col, true);
    void OnCollisionExit2D(Collision2D col)      => HandleCollision(col, false);

    void HandleCollision(Collision2D col, bool entering)
    {
        // only against other push‐boxes (assume same layer)
        if (col.collider.gameObject.layer != gameObject.layer)
            return;

        if (entering)
        {
            foreach (var cp in col.contacts)
            {
                // contact normal points **down** relative to this collider
                // meaning “I’m above them pushing straight down”
                if (cp.normal.y < -0.5f)
                {
                    if (ignored.Add(col.collider))
                        Physics2D.IgnoreCollision(myCol, col.collider, true);
                    break;
                }
            }
        }
        else
        {
            // when they separate, restore collision
            if (ignored.Remove(col.collider))
                Physics2D.IgnoreCollision(myCol, col.collider, false);
        }
    }
}

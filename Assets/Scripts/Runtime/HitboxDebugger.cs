using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[ExecuteAlways]
public class HitboxDebugger : MonoBehaviour
{
    [Tooltip("Color for hurtboxes (where the character can be hit)")]
    public Color hurtboxColor = Color.blue;

    [Tooltip("Color for hitboxes when not actually hitting")]
    public Color hitboxIdleColor = Color.red;

    [Tooltip("Color for hitboxes when overlapping a hurtbox")]
    public Color hitboxActiveColor = Color.green;

    void OnDrawGizmos()
    {
        // 1) Gather all hurtbox colliders by name
        var hurtBoxes = new List<Collider2D>();
        hurtBoxes.AddRange(GetComponentsInChildren<BoxCollider2D>()
            .Where(c => c.gameObject.name.ToLower().Contains("hurtbox")));
        hurtBoxes.AddRange(GetComponentsInChildren<CapsuleCollider2D>()
            .Where(c => c.gameObject.name.ToLower().Contains("hurtbox")));

        // 2) Gather all hitbox colliders by presence of HitboxToggle/HitboxDamage
        var hitBoxes = new List<Collider2D>();
        hitBoxes.AddRange(GetComponentsInChildren<BoxCollider2D>()
            .Where(c => c.GetComponent<HitboxToggle>() != null 
                     || c.GetComponent<HitboxDamage>() != null));
        hitBoxes.AddRange(GetComponentsInChildren<CapsuleCollider2D>()
            .Where(c => c.GetComponent<HitboxToggle>() != null 
                     || c.GetComponent<HitboxDamage>() != null));

        // 3) Draw hurtboxes in blue
        Gizmos.color = hurtboxColor;
        foreach (var hb in hurtBoxes)
            DrawWire(hb);

        // 4) Draw hitboxes: red or green if overlapping a hurtbox
        foreach (var hb in hitBoxes)
        {
            bool isHitting = false;
            // Use OverlapCollider to detect any overlaps (including triggers)
            var filter  = new ContactFilter2D { useTriggers = true };
            var results = new Collider2D[16];
            int count   = hb.Overlap(filter, results);
            for (int i = 0; i < count; i++)
                if (hurtBoxes.Contains(results[i]))
                {
                    isHitting = true;
                    break;
                }

            Gizmos.color = isHitting ? hitboxActiveColor : hitboxIdleColor;
            DrawWire(hb);
        }
    }

    void DrawWire(Collider2D col)
    {
        var b = col.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}

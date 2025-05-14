using UnityEngine;

/// <summary>
/// Absolute stage limiter: every physics step it clamps the Rigidbody2D X
/// position to the [LeftX, RightX] range and zeros horizontal velocity if
/// clamped, so fighters can NEVER cross the arena walls.
/// Add to each fighter root (with FighterCharacter + Rigidbody2D).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(+100)]   // run at the very end of FixedUpdate queue
public class FighterBoundaryLimiter : MonoBehaviour
{
    StageBounds stage;
    Rigidbody2D rb;

    void Awake()
    {
        rb    = GetComponent<Rigidbody2D>();
        stage = FindObjectOfType<StageBounds>();

        if (stage == null)
            Debug.LogError("StageBounds not found in scene!", this);
    }

void FixedUpdate()
{
    if (stage == null) return;

    /* predict, then zero X velocity _before_ movement runs */
    float next = rb.position.x + rb.linearVelocity.x * Time.fixedDeltaTime;

    if (rb.linearVelocity.x < 0f && next < stage.LeftX)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    else if (rb.linearVelocity.x > 0f && next > stage.RightX)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
}

}

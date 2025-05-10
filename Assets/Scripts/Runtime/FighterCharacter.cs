using UnityEngine;
using UMK3;  // for CharacterState, MoveFrameData, Gender, and your SOs

[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class FighterCharacter : MonoBehaviour
{
    [Header("Data Tables (assign these in the Inspector)")]
    public MoveTableSO     moveTable;
    public ReactionTableSO reactionTable;
    public ThrowTableSO    throwTable;
    public RoundAudioBank  audioBank;      // now has hitClip & blockClip fields

    [Header("Controls")]
    public PlayerControlsProfile controlsProfile;

    [Header("Startup")]
    public bool   startFacingRight = true;
    public Gender gender;                  // shared from UMK3.Gender

    [HideInInspector] public FighterCharacterCore core;

    InputBuffer  inputBuffer;
    Rigidbody2D  rb;
    AudioSource  audioSource;

    void Awake()
    {
        // Cache components
        inputBuffer = GetComponent<InputBuffer>();
        rb          = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        // Wire in controls profile
        if (controlsProfile == null)
        {
            Debug.LogError($"{name}: controlsProfile not set!", this);
        }
        else
        {
            inputBuffer.profile = controlsProfile;
        }

        // Validate SO references
        if (moveTable     == null) Debug.LogError($"{name}: moveTable not set!",     this);
        if (reactionTable == null) Debug.LogError($"{name}: reactionTable not set!", this);
        if (throwTable    == null) Debug.LogError($"{name}: throwTable not set!",    this);
        if (audioBank     == null) Debug.LogError($"{name}: audioBank not set!",     this);

        // Instantiate core and inject our InputBuffer
        core = new FighterCharacterCore(moveTable, reactionTable, throwTable)
        {
            InputBuf = inputBuffer
        };

        // Initial spawn (position & facing)
        Vector2 pos = transform.position;
        core.SpawnAt(pos, startFacingRight);

        // Sync Rigidbody2D
        rb.position = pos;
        rb.velocity = Vector2.zero;
    }

    void Update()
    {
        // InputBuffer MonoBehaviour handles its own Update()
        // Don't call core.FixedTick() here
    }

    void FixedUpdate()
    {
        // 1) Advance simulation
        if (!core.IsPaused)
        {
            core.FixedTick();
        }

        // 2) Apply movement from core.Velocity
        Vector2 vel2D = new Vector2(core.Velocity.x, core.Velocity.y);
        Vector2 next  = rb.position + vel2D * Time.fixedDeltaTime;
        rb.MovePosition(next);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var attacker = other.GetComponentInParent<FighterCharacter>();
        if (attacker != null && attacker != this)
        {
            ReceiveHit(attacker);
        }
    }

    /// <summary>
    /// Called by hitbox scripts when a hit lands.
    /// </summary>
    public void ReceiveHit(FighterCharacter attacker)
    {
        // Determine blocked vs. hit
        var mv = attacker.core.CurrentMove;
        bool blocked =
               (core.State == CharacterState.Crouch && !mv.unblockable)
            || (core.State == CharacterState.Attacking && !mv.unblockable);

        // Forward to core
        core.ReceiveHit(mv, blocked, attacker.core);

        // Play hit or block SFX
        if (audioBank != null && audioSource != null)
        {
            AudioClip clip = blocked
                ? audioBank.blockClip
                : audioBank.hitClip;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }

    // ── API for RoundSystem ──────────────────────────

    /// <summary>Pause/resume simulation.</summary>
    public void ForcePaused(bool paused)
    {
        core.IsPaused = paused;
    }

    /// <summary>Force an FSM state (e.g. Knockdown, GetUp).</summary>
    public void ForceState(CharacterState st)
    {
        core.SetState(st);
    }

    /// <summary>Reset all state and position for a new round.</summary>
    public void RoundReset(Vector2 position, bool facingRight)
    {
        // Teleport
        transform.position = position;
        rb.position        = position;
        rb.velocity        = Vector2.zero;

        // Reset core
        core.FullReset();
        core.SpawnAt(position, facingRight);
    }

    /// <summary>Expose health for the round manager.</summary>
    public int Health => core.Health;
}

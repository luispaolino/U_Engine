using UnityEngine;
using UMK3;

[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class FighterCharacter : MonoBehaviour
{
    /* ── Inspector fields (unchanged) ─────────────────────────────────── */
    [Header("Data Tables")]
    public MoveTableSO     moveTable;
    public ReactionTableSO reactionTable;
    public ThrowTableSO    throwTable;
    public CombatAudioBank combatBank;

    [Header("Controls")]
    public PlayerControlsProfile controlsProfile;

    [Header("Graphics")]
    public Transform graphics;

    [Header("Startup")]
    public bool   startFacingRight = true;
    public Gender gender;

    [Header("Stage")]
    [Tooltip("World-space Y coordinate considered floor.")]
    public float groundY = 0f;

    [Header("Jump Tuning")]
    [Tooltip("Vertical launch speed in world-units / sec")]
    public float jumpUpVelocity = 7f;          // default

    [Tooltip("Horizontal speed when jumping toward the opponent")]
    public float jumpForwardVelocity = 6f;

    [Tooltip("Horizontal speed when jumping away from the opponent")]
    public float jumpBackVelocity = 6f;

    /* ── Runtime refs ─────────────────────────────────────────────────── */
    [HideInInspector] public FighterCharacterCore core;
    InputBuffer _input;
    Rigidbody2D _rb;
    AudioSource _aSrc;

    void Awake()
    {
        _input = GetComponent<InputBuffer>();
        _rb    = GetComponent<Rigidbody2D>();
        _aSrc  = GetComponent<AudioSource>();

        if (controlsProfile == null){
        throw new System.Exception($"{name}: controlsProfile missing!  Drag a PlayerControlsProfile onto this fighter.");
        }
        else
            _input.profile = controlsProfile;
        core = new FighterCharacterCore(moveTable, reactionTable, throwTable)
        {
            InputBuf   = _input,
            MinGroundY = groundY
        };

        Vector2 startPos = transform.position;
        core.SpawnAt(startPos, startFacingRight);

        if (graphics)
            graphics.localRotation =
                Quaternion.Euler(0f, startFacingRight ? 0f : 180f, 0f);

        core.JumpUpVelocity      = jumpUpVelocity;
        core.JumpForwardVelocity = jumpForwardVelocity;
        core.JumpBackVelocity    = jumpBackVelocity;
    }

    void Update()
    {
        if (!core.IsPaused)
            _input.Capture(core.FacingRight);
    }

    void FixedUpdate()
    {
        if (core.IsPaused) return;

        core.FixedTick();

        /* move kinematic Rigidbody to core-computed position */
        _rb.MovePosition(core.Position);

        if (graphics)
        {
            graphics.localRotation = Quaternion.Euler(
                0f,
                core.FacingRight ? 0f : 180f,
                0f);
}
        /* ── Animator bridge (optional) ───────────────────────────────── */
        if (graphics && graphics.TryGetComponent(out Animator anim))
        {
            anim.SetBool("IsJumping",     core.State == CharacterState.Jumping);
            anim.SetBool("InJumpStartup", core.InJumpStartup);
            anim.SetBool("Grounded",      core.State != CharacterState.Jumping
                                         && core.State != CharacterState.JumpStartup);
        }
    }

    /* ── HIT DETECTION / reactions (unchanged) ───────────────────────── */
    void OnTriggerEnter2D(Collider2D col)
    {
        var attacker = col.GetComponentInParent<FighterCharacter>();
        if (attacker && attacker != this)
            ReceiveHit(attacker);
    }

    public void ReceiveHit(FighterCharacter attacker)
    {
        MoveFrameData mv = attacker.core.CurrentMove;
        bool blocked =
            (core.State == CharacterState.Crouch  && !mv.unblockable) ||
            (core.State == CharacterState.Attacking && !mv.unblockable);

        core.ReceiveHit(mv, blocked, attacker.core);

        if (combatBank)
        {
            AudioClip clip = blocked ? combatBank.blockClip : combatBank.hitClip;
            if (clip) _aSrc.PlayOneShot(clip);
        }
    }

    /* ── External hooks ─────────────────────────────────────────────── */
    public void ForcePaused(bool p)           => core.IsPaused = p;
    public void ForceState(CharacterState s)  => core.SetState(s);

    public void RoundReset(Vector2 pos, bool faceRight)
    {
        transform.position = pos;
        _rb.position       = pos;
        _rb.linearVelocity       = Vector2.zero;

        core.FullReset();
        core.SpawnAt(pos, faceRight);
        core.InputBuf = _input;

        if (graphics)
            graphics.localRotation =
                Quaternion.Euler(0f, faceRight ? 0f : 180f, 0f);
    }

    public int Health => core.Health;
}

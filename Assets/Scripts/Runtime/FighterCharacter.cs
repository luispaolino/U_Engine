using UnityEngine;
using UMK3; // Assuming this namespace contains MoveTableSO, ReactionTableSO, etc.

[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class FighterCharacter : MonoBehaviour
{
    /* ── Inspector fields ─────────────────────────────────── */
    [Header("Data Tables")]
    public MoveTableSO     moveTable;
    public ReactionTableSO reactionTable;
    public ThrowTableSO    throwTable;
    public CombatAudioBank combatBank;

    [Header("Controls")]
    public PlayerControlsProfile controlsProfile;

    [Header("Graphics")]
    public Transform graphics; // Child GameObject for visual representation / Animator

    [Header("Startup")]
    public bool   startFacingRight = true;
    public Gender gender;

    [Header("Stage")]
    [Tooltip("World-space Y coordinate considered floor.")]
    public float groundY = 0f;

    [Header("Jump Tuning")]
    [Tooltip("Vertical launch speed in world-units / sec")]
    public float jumpUpVelocity = 7f;

    [Tooltip("Horizontal speed when jumping toward the opponent")]
    public float jumpForwardVelocity = 6f;

    [Tooltip("Horizontal speed when jumping away from the opponent")]
    public float jumpBackVelocity = 6f;

    /* ── Runtime refs ─────────────────────────────────────────────────── */
    public FighterCharacterCore core;
    private InputBuffer _input;
    private Rigidbody2D _rb;
    private AudioSource _aSrc;
    private Animator _anim; // Cached Animator reference

    void Awake()
    {
        _input = GetComponent<InputBuffer>();
        _rb    = GetComponent<Rigidbody2D>();
        _aSrc  = GetComponent<AudioSource>();

        if (graphics != null)
        {
            _anim = graphics.GetComponent<Animator>();
        }
        if (_anim == null) // Fallback to animator on this GameObject itself
        {
            _anim = GetComponent<Animator>();
            if (_anim != null && graphics != null) // If animator is on root but graphics child exists, warn or decide.
            {
                 Debug.LogWarning($"Animator found on {name} root, but a 'graphics' child also exists. Ensure Animator targets the correct hierarchy.", this);
            }
        }
        if (_anim == null && graphics == null)
        {
            Debug.LogWarning($"No Animator found on {name} or its 'graphics' child. Animations will not play.", this);
        }


        if (controlsProfile == null)
        {
            Debug.LogError($"{name}: controlsProfile missing! Drag a PlayerControlsProfile onto this fighter.", this);
            enabled = false;
            return;
        }
        _input.profile = controlsProfile;


        if (moveTable == null || reactionTable == null)
        {
            Debug.LogError($"{name}: One or more data tables (Move, Reaction) are missing!", this);
            enabled = false;
            return;
        }

        core = new FighterCharacterCore(moveTable, reactionTable, throwTable)
        {
            InputBuf   = _input,
            MinGroundY = groundY,
            JumpUpVelocity       = this.jumpUpVelocity,
            JumpForwardVelocity  = this.jumpForwardVelocity,
            JumpBackVelocity     = this.jumpBackVelocity
        };

        Vector2 startPos = transform.position;
        core.SpawnAt(startPos, startFacingRight); // This sets core.FacingRight
        _rb.position = startPos;

        UpdateGraphicsAndAnimatorOrientation(); // Initial orientation
    }

    void Update()
    {
        if (core != null && !core.IsPaused)
        {
            _input.Capture(core.FacingRight);
        }
    }

    void FixedUpdate()
    {
        if (core == null || core.IsPaused) return;

        core.SyncPosition(_rb.position);
        core.FixedTick(); // Core logic runs, core.FacingRight might be updated by RoundSystem via core.SetFacing()
        _rb.MovePosition(core.Position);

        UpdateGraphicsAndAnimatorOrientation(); // Update visuals based on core.FacingRight
        UpdateAnimatorStates();                 // Update animation state parameters
    }

    void UpdateGraphicsAndAnimatorOrientation()
    {
        if (core == null) return;

        bool shouldBeFacingRight = core.FacingRight;

        // 1. Update Transform graphics rotation
        if (graphics != null)
        {
            graphics.localRotation = Quaternion.Euler(0f, shouldBeFacingRight ? 0f : 180f, 0f);
        }
        // If using a 2D SpriteRenderer directly on this GameObject for visuals:
        // else if (TryGetComponent(out SpriteRenderer sr))
        // {
        //     sr.flipX = !shouldBeFacingRight; // flipX is true when facing left
        // }


        // 2. Update Animator "Mirror" parameter
        if (_anim != null)
        {
            // "Mirror" parameter in Animator is typically true when the character should be mirrored (i.e., facing left)
            _anim.SetBool("Mirror", !shouldBeFacingRight);
        }
    }

    void UpdateAnimatorStates() // Handles animation states based on character state
    {
        if (core == null || _anim == null) return;

        _anim.SetBool("IsJumping",     core.State == CharacterState.Jumping);
        _anim.SetBool("InJumpStartup", core.InJumpStartup);
        _anim.SetBool("Grounded",      core.State != CharacterState.Jumping && core.State != CharacterState.JumpStartup);
        _anim.SetBool("IsCrouching",   core.State == CharacterState.Crouch || core.State == CharacterState.BlockingLow);
        _anim.SetBool("IsWalking",     core.State == CharacterState.Walking);
        _anim.SetBool("IsRunning",     core.State == CharacterState.Running);
        _anim.SetBool("IsAttacking",   core.State == CharacterState.Attacking || core.State == CharacterState.BackDash);
        _anim.SetBool("IsBlocking",    core.State == CharacterState.BlockingHigh || core.State == CharacterState.BlockingLow);
        _anim.SetBool("IsHit",         core.State == CharacterState.HitStun || core.State == CharacterState.Knockdown);

        // Trigger specific attack animations
        if (core.IsMoveDataValid && (core.State == CharacterState.Attacking || core.State == CharacterState.BackDash))
        {
            // Ensure CurrentMove.tag is not null or empty before using it as a trigger
            if (!string.IsNullOrEmpty(core.CurrentMove.tag))
            {
                _anim.SetTrigger(core.CurrentMove.tag);
            }
        }
    }

    /* ── HIT DETECTION / reactions ─────────────────────────────────── */
    void OnTriggerEnter2D(Collider2D otherCollider)
    {
        if (core == null || core.State == CharacterState.HitStun || core.State == CharacterState.Knockdown || core.IsPaused)
        {
            return;
        }

        FighterCharacter attacker = otherCollider.GetComponentInParent<FighterCharacter>();

        if (attacker != null && attacker != this && attacker.core != null &&
            (attacker.core.State == CharacterState.Attacking || attacker.core.State == CharacterState.BackDash) &&
            attacker.core.Phase == MovePhase.Active &&
            attacker.core.IsMoveDataValid)
        {
            ReceiveHit(attacker, attacker.core.CurrentMove);
        }
    }

    public void ReceiveHit(FighterCharacter attacker, MoveFrameData moveData)
    {
        if (core == null || core.IsPaused) return;

        bool isActuallyBlocked = false;
        if (core.State == CharacterState.BlockingHigh) // Simplified blocking
        {
            isActuallyBlocked = true;
        }
        else if (core.State == CharacterState.BlockingLow) // Simplified blocking
        {
            isActuallyBlocked = true;
        }

        core.ReceiveHit(moveData, isActuallyBlocked, attacker.core);

        if (combatBank != null && _aSrc != null)
        {
            AudioClip clipToPlay = isActuallyBlocked ? combatBank.blockClip : combatBank.hitClip;
            if (clipToPlay != null)
            {
                _aSrc.PlayOneShot(clipToPlay);
            }
        }
    }

    /* ── External hooks ─────────────────────────────────────────────── */
    public void ForcePaused(bool isPaused)
    {
        if (core != null)
        {
            core.IsPaused = isPaused;
        }
    }

    public void ForceState(CharacterState newState)
    {
        if (core != null)
        {
            core.SetState(newState);
        }
    }

    public void RoundReset(Vector2 spawnPosition, bool shouldFaceRight)
    {
        transform.position = spawnPosition;
        if (_rb != null)
        {
            _rb.position = spawnPosition;
            _rb.linearVelocity = Vector2.zero;
        }

        if (core != null)
        {
            core.FullReset(); // Resets core internal state
            core.SpawnAt(spawnPosition, shouldFaceRight); // Sets initial position AND core.FacingRight
        }
        if (_input != null)
        {
            _input.ClearPrev(); // Clear input buffer to prevent stale presses
        }

        UpdateGraphicsAndAnimatorOrientation(); // Set initial visual orientation based on core.FacingRight

        // If animator states get stuck, consider more forceful reset:
        // if (_anim != null && _anim.runtimeAnimatorController != null)
        // {
        //     _anim.Rebind();
        //     _anim.Update(0f); // Force apply rebind
        // }
    }

    public int Health => (core != null) ? core.Health : 0;
}
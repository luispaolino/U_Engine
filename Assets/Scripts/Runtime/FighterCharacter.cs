using UnityEngine;
using UMK3; // Your project's namespace

[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class FighterCharacter : MonoBehaviour
{
    [Header("Character Definition Assets")]
    public CharacterInfoSO characterInfo;
    public MovementStatsSO movementStats;
    public SpecialMovesSO specialMovesList;
    public ReactionTableSO reactionTable;
    public ThrowTableSO throwTable;

    [Header("Other Data & References")]
    public CombatAudioBank combatBank;
    [HideInInspector] public PlayerControlsProfile controlsProfile; // Set by RoundSystem
    public Transform graphics; // Should be assigned in Prefab or found if child named "Graphics"
    // Gender is now in CharacterInfoSO, accessed via core.CharInfo.gender
    [Tooltip("World-space Y coordinate considered floor.")] public float groundY = 0f;

    public FighterCharacterCore core;
    private InputBuffer _input;
    private Rigidbody2D _rb;
    private AudioSource _aSrc;
    private Animator _anim;
    private float _initialVisualYRotationOffset = 0f;

    void Awake()
    {
        _input = GetComponent<InputBuffer>();
        _rb = GetComponent<Rigidbody2D>();
        _aSrc = GetComponent<AudioSource>();

        if (graphics == null)
        {
            graphics = transform.Find("Graphics");
            if (graphics == null)
            {
                Debug.LogWarning($"FighterCharacter '{name}': Graphics child not found. Visuals and animator might not work correctly.", this);
            }
        }

        if (graphics != null)
        {
            _anim = graphics.GetComponent<Animator>();
        }
        if (_anim == null) // Fallback to animator on this GameObject itself
        {
            _anim = GetComponent<Animator>();
        }
        if (_anim == null)
        {
            if (graphics != null) Debug.LogWarning($"FighterCharacter '{name}': Animator not found on 'graphics' child or root. Animations will not play.", this);
            else Debug.LogWarning($"FighterCharacter '{name}': No Animator found. Animations will not play (no graphics child assigned/found either).", this);
        }

        if (characterInfo == null) { Debug.LogError($"FighterCharacter '{name}': CharacterInfoSO NOT assigned! This is critical.", this); enabled = false; return; }
        if (movementStats == null) { Debug.LogError($"FighterCharacter '{name}': MovementStatsSO NOT assigned! This is critical.", this); enabled = false; return; }
        if (specialMovesList == null) { Debug.LogWarning($"FighterCharacter '{name}': SpecialMovesSO NOT assigned. Special moves may not function.", this); }
        if (reactionTable == null) { Debug.LogError($"FighterCharacter '{name}': ReactionTableSO NOT assigned! This is critical.", this); enabled = false; return; }
        if (throwTable == null) { Debug.LogWarning($"FighterCharacter '{name}': ThrowTableSO not assigned. Throws may not function.", this); }

        core = new FighterCharacterCore(characterInfo, movementStats, specialMovesList, reactionTable, throwTable)
        {
            MinGroundY = this.groundY
        };
    }

    public void InitializeCoreInput()
    {
        if (core != null && _input != null && controlsProfile != null)
        {
            _input.profile = controlsProfile;
            core.InputBuf = _input;
        }
        else
        {
            Debug.LogError($"Failed to initialize core input for {name}. Critical references missing (core, input, or controlsProfile).", this);
        }
    }

    public void MatchReset()
    {
        if (core != null)
        {
            core.FullMatchReset();
        }
    }

    public void RoundReset(Vector2 spawnPosition, bool shouldFaceRightLogically)
    {
        transform.position = spawnPosition;
        if (_rb != null)
        {
            _rb.position = spawnPosition;
            _rb.linearVelocity = Vector2.zero;
        }
        if (core != null)
        {
            core.FullRoundReset();
            core.SpawnAt(spawnPosition, shouldFaceRightLogically);
        }
        if (_input != null)
        {
            _input.ClearPrev();
        }
        UpdateGraphicsAndAnimatorOrientation();
    }

    public void SetInitialVisualYRotationOffset(float offset)
    {
        _initialVisualYRotationOffset = offset;
        if (core != null) // Ensure core exists before trying to update graphics based on its facing
        {
            UpdateGraphicsAndAnimatorOrientation();
        }
    }

    void Update()
    {
        if (core != null && !core.IsPaused && _input != null)
        {
            _input.Capture(core.FacingRight);
            if (core.Health <= 0 && core.MercyWindowTimeRemaining > 0 && core.State == CharacterState.MercyReceiving)
            {
                if (_input.State.PressedBlock)
                {
                    if (core.RegisterBlockTapForMercy())
                    {
                        Debug.Log(this.name + " successfully received Mercy via input!");
                    }
                }
            }
        }
    }

void FixedUpdate()
{
    if (core == null) return;

    // Always sync visuals & animator
    UpdateGraphicsAndAnimatorOrientation();
    UpdateAnimatorStates();

    if (core.IsPaused) 
        return;

    // Physics & game‐logic only when NOT paused
    core.FixedTick();
    _rb.MovePosition(core.Position);
}


    void UpdateGraphicsAndAnimatorOrientation()
    {
        if (core == null || graphics == null) return;
        bool shouldBeFacingRight = core.FacingRight;
        float facingYRotation = shouldBeFacingRight ? 0f : 180f;
        float totalVisualYRotation = _initialVisualYRotationOffset + facingYRotation;
        graphics.localRotation = Quaternion.Euler(0f, totalVisualYRotation, 0f);
        if (_anim != null)
        {
            _anim.SetBool("Mirror", !shouldBeFacingRight);
        }
    }

void UpdateAnimatorStates() 
{ 
    if (core == null || _anim == null) return; 

    _anim.SetBool("IsJumping", core.State == CharacterState.Jumping); 
    _anim.SetBool("InJumpStartup", core.InJumpStartup); 
    _anim.SetBool("Grounded", core.State != CharacterState.Jumping && core.State != CharacterState.JumpStartup); 
    _anim.SetBool("IsCrouching", core.State == CharacterState.Crouch || core.State == CharacterState.BlockingLow); 
    _anim.SetBool("IsWalking", core.State == CharacterState.Walking); 
    _anim.SetBool("IsRunning", core.State == CharacterState.Running); 
    _anim.SetBool("IsAttacking", core.State == CharacterState.Attacking || core.State == CharacterState.BackDash); 
    _anim.SetBool("IsBlocking", core.State == CharacterState.BlockingHigh || core.State == CharacterState.BlockingLow); 
    
    bool isTrulyKOd = core.Health <= 0 && 
                      core.State != CharacterState.FinishHimVictim && 
                      core.State != CharacterState.MercyReceiving;
    _anim.SetBool("IsKOd", isTrulyKOd); 
    
    bool isCurrentlyDizzy = core.State == CharacterState.FinishHimVictim;
    _anim.SetBool("IsDizzy", isCurrentlyDizzy); 

    // Debug log specifically when character *should* be dizzy
    if (isCurrentlyDizzy)
    {
        Debug.Log($"{this.name} (Core State: {core.State}) -> Animator params: IsDizzy={_anim.GetBool("IsDizzy")}, IsKOd={_anim.GetBool("IsKOd")}");
    }
    
    if (core.IsMoveDataValid && (core.State == CharacterState.Attacking || core.State == CharacterState.BackDash)) 
    { 
        if (!string.IsNullOrEmpty(core.CurrentMove.tag)) _anim.SetTrigger(core.CurrentMove.tag); 
    } 
}

    void OnTriggerEnter2D(Collider2D otherCollider)
    {
        if (core == null || core.IsPaused || (core.Health <= 0 && core.State != CharacterState.FinishHimVictim)) return;
        FighterCharacter attacker = otherCollider.GetComponentInParent<FighterCharacter>();
        if (attacker != null && attacker != this && attacker.core != null &&
            (attacker.core.State == CharacterState.Attacking || attacker.core.State == CharacterState.BackDash || attacker.core.State == CharacterState.FinishHimWinner) &&
            attacker.core.Phase == MovePhase.Active && attacker.core.IsMoveDataValid)
        {
            ReceiveHit(attacker, attacker.core.CurrentMove);
        }
    }

    // In FighterCharacter.cs
public void ReceiveHit(FighterCharacter attacker, MoveFrameData moveData) 
{ 
    if (core == null || core.IsPaused) return; 
    // Allow hitting if in FinishHimVictim or MercyReceiving, even if health is 0
    if (core.Health <= 0 && core.State != CharacterState.FinishHimVictim && core.State != CharacterState.MercyReceiving) return; 

    bool isActuallyBlocked = (core.State == CharacterState.BlockingHigh || core.State == CharacterState.BlockingLow);
    CharacterState stateBeforeHit = core.State;

    if (attacker?.core != null) 
    {
        attacker.core.SetKOAsFriendly(false); 
    }
    
    core.ReceiveHit(moveData, isActuallyBlocked, attacker?.core); // Pass attacker's core (can be null)

    if (combatBank != null && _aSrc != null) 
    { 
        AudioClip clipToPlay = isActuallyBlocked ? combatBank.blockClip : combatBank.hitClip; 
        if (clipToPlay != null) _aSrc.PlayOneShot(clipToPlay); 
    }

    if (_anim != null)
    {
        if (isActuallyBlocked)
        {
            _anim.SetTrigger("BlockImpact");
            // Debug.Log(name + " playing BlockImpact animation trigger."); // Already in previous version
        }
        else // Hit was not blocked
        {
            // Check if state changed to HitStun (and wasn't already in a similar non-interruptible state)
            if (core.State == CharacterState.HitStun && stateBeforeHit != CharacterState.HitStun && stateBeforeHit != CharacterState.Knockdown)
            {
                _anim.SetTrigger("Hit"); 
                // Debug.Log(name + " playing Hit (stun) animation trigger."); // Already in previous version
            }
            // Check if state changed to Knockdown (and wasn't already in a similar non-interruptible state)
            else if (core.State == CharacterState.Knockdown && stateBeforeHit != CharacterState.Knockdown && stateBeforeHit != CharacterState.FinishHimVictim)
            {
                // This covers both normal knockdowns and KO knockdowns that result in Knockdown state.
                _anim.SetTrigger("Knockdown"); 
                // Debug.Log(name + " playing Knockdown (or KO_Fall) animation trigger."); // Already in previous version
            }
        }
    }
}

    public void ForcePaused(bool isPaused) { if (core != null) core.IsPaused = isPaused; }
    public void ForceState(CharacterState newState) { if (core != null) core.SetState(newState); }
    public int Health => (core != null) ? core.Health : 0;

    public void PlayWinAnimation()
    {
        if (_anim != null && core != null && core.State != CharacterState.FinishHimWinner && Health > 0)
        {
            if (core.State != CharacterState.Idle && core.State != CharacterState.Walking && core.State != CharacterState.Crouch)
            {
                core.SetState(CharacterState.Idle);
            }
            _anim.SetTrigger("Victory"); // Using "Victory" as per your last instruction
            //Debug.Log(name + " playing Victory animation trigger.");
        }
        else if (_anim == null) {
            Debug.LogWarning(name + ": Animator is null, cannot play Victory animation.");
        }
    }

    public void TriggerDizzyAnim()
{
    if (_anim != null)
    {
        // If you went with a Trigger approach:
        _anim.ResetTrigger("Knockdown");
        _anim.SetTrigger("Dizzy");
    }
}

public void TriggerKnockdownAnim()
{
    if (_anim != null)
    {
        _anim.SetTrigger("Knockdown");
    }
}

    // PlayDefeatedAnimation is implicitly handled by setting core.State to Knockdown
    // and the Animator responding to the "IsKOd" bool and potentially a "Knockdown" trigger.
    // If a specific "DefeatedLoop" trigger is needed beyond what IsKOd + Knockdown state provides,
    // this method could be called by RoundSystem.
    public void PlayDefeatedAnimation() 
    { 
        if (_anim != null && core != null && core.State == CharacterState.Knockdown && core.Health <= 0) 
        { 
            // _anim.SetTrigger("DefeatedLoop"); // Example if you have a specific loop trigger
            Debug.Log(name + " is in Defeated state (Knockdown). Animator should reflect via IsKOd."); 
        } 
    }
}
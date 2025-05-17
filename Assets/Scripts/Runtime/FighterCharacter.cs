using UnityEngine;
using UMK3;

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
    [HideInInspector] public PlayerControlsProfile controlsProfile;
    public Transform graphics;
    // Gender is now in CharacterInfoSO, accessed via core.CharInfo.gender
    // public Gender gender; 
    [Tooltip("World-space Y coordinate considered floor.")] public float groundY = 0f;

    public FighterCharacterCore core;
    private InputBuffer _input; private Rigidbody2D _rb; private AudioSource _aSrc; private Animator _anim;
    private float _initialVisualYRotationOffset = 0f;

    void Awake()
    {
        _input = GetComponent<InputBuffer>(); _rb = GetComponent<Rigidbody2D>(); _aSrc = GetComponent<AudioSource>();
        if (graphics == null) graphics = transform.Find("Graphics");
        if (graphics != null) _anim = graphics.GetComponent<Animator>();
        if (_anim == null) _anim = GetComponent<Animator>();
        if (_anim == null) Debug.LogWarning($"No Animator for {name}.", this);
        
        if (characterInfo == null) { Debug.LogError($"FC '{name}': CharacterInfoSO NOT assigned!", this); enabled = false; return; }
        if (movementStats == null) { Debug.LogError($"FC '{name}': MovementStatsSO NOT assigned!", this); enabled = false; return; }
        if (specialMovesList == null) { Debug.LogWarning($"FC '{name}': SpecialMovesSO NOT assigned.", this); }
        if (reactionTable == null) { Debug.LogError($"FC '{name}': ReactionTableSO NOT assigned!", this); enabled = false; return; }
        
        core = new FighterCharacterCore(characterInfo, movementStats, specialMovesList, reactionTable, throwTable)
        { MinGroundY = this.groundY };
    }
    
    public void InitializeCoreInput(){ if(core!=null&&_input!=null&&controlsProfile!=null){_input.profile=controlsProfile;core.InputBuf=_input;}else{Debug.LogError($"Failed core input init {name}.",this);}}
    public void MatchReset(){if(core!=null)core.FullMatchReset();}
    public void RoundReset(Vector2 sP,bool sFR){transform.position=sP;if(_rb!=null){_rb.position=sP;_rb.linearVelocity=Vector2.zero;}if(core!=null){core.FullRoundReset();core.SpawnAt(sP,sFR);}if(_input!=null)_input.ClearPrev();UpdateGraphicsAndAnimatorOrientation();}
    public void SetInitialVisualYRotationOffset(float o){_initialVisualYRotationOffset=o;if(core!=null)UpdateGraphicsAndAnimatorOrientation();}
    void Update(){if(core!=null&&!core.IsPaused&&_input!=null){_input.Capture(core.FacingRight);if(core.Health<=0&&core.MercyWindowTimeRemaining>0&&core.State==CharacterState.MercyReceiving){if(_input.State.PressedBlock){if(core.RegisterBlockTapForMercy())Debug.Log(name+" Mercy!");}}}}
    void FixedUpdate(){if(core==null||core.IsPaused)return;core.SyncPosition(_rb.position);core.FixedTick();_rb.MovePosition(core.Position);UpdateGraphicsAndAnimatorOrientation();UpdateAnimatorStates();}
    
void UpdateGraphicsAndAnimatorOrientation()
{
    if (core == null || graphics == null) return; 

    bool shouldCoreBeFacingRight = core.FacingRight;
    float facingYRotation = shouldCoreBeFacingRight ? 0f : 180f;
    float totalVisualYRotation = _initialVisualYRotationOffset + facingYRotation;

    // Apply ONLY to the graphics child's local rotation
    graphics.localRotation = Quaternion.Euler(0f, totalVisualYRotation, 0f);

    if (_anim != null)
    {
        _anim.SetBool("Mirror", !shouldCoreBeFacingRight);
    }
}

    void UpdateAnimatorStates(){
        if (core == null || _anim == null) return;
        _anim.SetBool("IsJumping",core.State==CharacterState.Jumping);
        _anim.SetBool("InJumpStartup",core.InJumpStartup);
        _anim.SetBool("Grounded",core.State!=CharacterState.Jumping&&core.State!=CharacterState.JumpStartup);
        _anim.SetBool("IsCrouching",core.State==CharacterState.Crouch||core.State==CharacterState.BlockingLow);
        _anim.SetBool("IsWalking",core.State==CharacterState.Walking);
        _anim.SetBool("IsRunning",core.State==CharacterState.Running);
        _anim.SetBool("IsAttacking",core.State==CharacterState.Attacking||core.State==CharacterState.BackDash);
        _anim.SetBool("IsBlocking",core.State==CharacterState.BlockingHigh||core.State==CharacterState.BlockingLow);
        _anim.SetBool("IsHit",core.State==CharacterState.HitStun||core.State==CharacterState.Knockdown||core.State==CharacterState.MercyReceiving);
        _anim.SetBool("IsKOd",core.Health<=0&&core.State!=CharacterState.FinishHimVictim && core.State != CharacterState.MercyReceiving);
        _anim.SetBool("IsDizzy",core.State==CharacterState.FinishHimVictim);

        if (core.IsMoveDataValid && (core.State == CharacterState.Attacking || core.State == CharacterState.BackDash))
        {
            if (!string.IsNullOrEmpty(core.CurrentMove.tag)) _anim.SetTrigger(core.CurrentMove.tag);
        }
    }

    void OnTriggerEnter2D(Collider2D o){if(core==null||core.IsPaused||(core.Health<=0&&core.State!=CharacterState.FinishHimVictim))return;FighterCharacter att=o.GetComponentInParent<FighterCharacter>();if(att!=null&&att!=this&&att.core!=null&&(att.core.State==CharacterState.Attacking||att.core.State==CharacterState.BackDash||att.core.State==CharacterState.FinishHimWinner)&&att.core.Phase==MovePhase.Active&&att.core.IsMoveDataValid){ReceiveHit(att,att.core.CurrentMove);}}
    public void ReceiveHit(FighterCharacter attacker,MoveFrameData mD){if(core==null||core.IsPaused)return;if(core.Health<=0&&core.State!=CharacterState.FinishHimVictim&&core.State!=CharacterState.MercyReceiving)return;bool isBlk=(core.State==CharacterState.BlockingHigh||core.State==CharacterState.BlockingLow);if(attacker?.core!=null)attacker.core.SetKOAsFriendly(false);core.ReceiveHit(mD,isBlk,attacker.core);if(combatBank!=null&&_aSrc!=null){AudioClip clp=isBlk?combatBank.blockClip:combatBank.hitClip;if(clp!=null)_aSrc.PlayOneShot(clp);}}
    public void ForcePaused(bool p){if(core!=null)core.IsPaused=p;} public void ForceState(CharacterState s){if(core!=null)core.SetState(s);}
    public int Health=>(core!=null)?core.Health:0;

    public void PlayWinAnimation()
    {
        if (_anim != null && core != null && 
            core.State != CharacterState.FinishHimWinner && // Don't override if in FinishHim mode
            core.State != CharacterState.Attacking)      // Don't interrupt an attack for a win pose
        {
            core.SetState(CharacterState.Idle); // Or a specific CharacterState.VictoryPose
            _anim.SetTrigger("Victory"); // Ensure you have a "Win" trigger in your Animator
            //Debug.Log(name + " playing Win animation trigger.");
        }
    }

    public void PlayDefeatedAnimation()
    {
        if (_anim != null && core != null && core.State == CharacterState.Knockdown && core.Health <= 0)
        {
            //core.SetState(CharacterState.Idle);
            _anim.SetTrigger("Defeated");
            Debug.Log("Test if it is Knockdown");
        }
        else if (_anim == null)
        {
            Debug.LogWarning(name + ": Animator is null, cannot play Defeated animation.");
        }
    }
}
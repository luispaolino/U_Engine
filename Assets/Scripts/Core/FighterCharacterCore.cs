using UnityEngine;
using UMK3;   // MoveTableSO, ReactionTableSO, ThrowTableSO

#region PSX-style enums & flags -------------------------------------------------
public enum CharacterState
{
    Idle, Walking, Running, Crouch,
    BlockingHigh, BlockingLow, Jumping,

    Attacking, HitStun, Knockdown,
    BackDash, Thrown
}

public enum MovePhase { Startup, Active, Recovery }

public enum AttackPower { Light, Medium, Heavy, Special }

[System.Flags]
public enum DmgFlag
{
    NONE        = 0,
    DF_NO_CHIP  = 1 << 0,
    DF_UNBLOCK  = 1 << 1     // same idea as SF_UNBLOCKABLE
}
#endregion

public class FighterCharacterCore
{

    [Header("Ground Detection")]
    public Transform    groundCheck;       // a child empty at the character's feet
    public float        groundCheckRadius = 0.1f;
    public LayerMask    groundLayer;


    // ── Public surface ───────────────────────────────────────────────────────
    public InputBuffer     InputBuf    { get; set; }
    public MoveFrameData   CurrentMove { get; private set; }
    public bool            IsPaused    { get; set; }
    public int             Health      { get; private set; }
    public CharacterState  State       { get; private set; }
    public MovePhase       Phase       { get; private set; }
    public Vector3         Velocity    { get; private set; }
    public bool            FacingRight => _facingRight;

    // ── Data tables / refs ───────────────────────────────────────────────────
    readonly MoveTableSO     moveTable;
    readonly ReactionTableSO reactionTable;
    readonly ThrowTableSO    throwTable;

    // ── Constants (straight from PSX tables) ─────────────────────────────────
    readonly int[] block_len_tbl  = { 6, 8, 10, 10 };          // per AttackPower
    readonly int[] push_dist_tbl  = { 1, 2, 3, 3  };           // demo values (pixels)
    const int RUN_METER_MAX       = 128;                       // arbitrary scale
    const int RUN_DRAIN_PER_FRAME = 2;
    const int RUN_CRUSH_FRAMES    = 15;

    const float WALK_SPEED  = 3.5f;
    const float RUN_SPEED   = 6.0f;
    const float JUMP_VY     = 7.0f;
    const float JUMP_VX     = 3.0f;

    // ── Internal runtime fields ──────────────────────────────────────────────
    bool _facingRight;
    int  _phaseFrames;
    int  hitStun, block_cnt, knockTimer, run_crush_cnt;
    int  run_meter;

    MoveFrameData _currentMove;

    // ───────────────────────────────────────── ctor / reset
    public FighterCharacterCore(
        MoveTableSO mvTable,
        ReactionTableSO reactTable,
        ThrowTableSO thTable)
    {
        moveTable     = mvTable;
        reactionTable = reactTable;
        throwTable    = thTable;
        FullReset();
    }

    public void FullReset()
    {
        Health   = 1000;
        run_meter= RUN_METER_MAX;
        State    = CharacterState.Idle;
        Phase    = MovePhase.Startup;
        Velocity = Vector3.zero;

        hitStun = block_cnt = knockTimer = run_crush_cnt = 0;
    }

    public void SpawnAt(Vector2 pos, bool faceRight) => _facingRight = faceRight;
    public void SetFacing(bool right)                => _facingRight = right;

    // ───────────────────────────────────────── Fixed-tick main
    public void FixedTick()
    {
        if (IsPaused) return;

        /* ---- timers ---- */
        if (hitStun   > 0 && --hitStun   == 0) State = CharacterState.Idle;
        if (block_cnt > 0 && --block_cnt == 0) State = CharacterState.Idle;
        if (knockTimer> 0 && --knockTimer== 0) State = CharacterState.Idle;
        if (run_crush_cnt > 0) --run_crush_cnt;

        HandleMovement();
        HandleAttacks();
        AdvanceMovePhases();

        /* -------------------------------------------------------
        * Passive run-meter refill (1 unit per physics frame)   *
        * Only when NOT running or blocking.                   *
        * ----------------------------------------------------- */
        if (State != CharacterState.Running &&
            State != CharacterState.BlockingHigh &&
            State != CharacterState.BlockingLow &&
            run_meter < RUN_METER_MAX)
        {
            run_meter += 1;          // tweak value for slower/faster recharge
        }
    }

    // ───────────────────────────────────────── Movement & stance
    void HandleMovement()
{
    var  i       = InputBuf.State;
    int  dirSign = _facingRight ? 1 : -1;   // +1 if facing right, -1 if facing left
    float xDir   = 0f;

    /* ---------------------------------------------------------
     * 0) CROUCH (always processed first)
     * --------------------------------------------------------- */
    bool crouchBtn = i.Down && State != CharacterState.Jumping && State != CharacterState.HitStun;

    /* ---------------------------------------------------------
     * 1) Determine if guard-button is pressed
     * --------------------------------------------------------- */
    bool wantsBlock   = i.Block;
    bool inBlockStun  = block_cnt > 0;          // still frozen from last blocked hit
    bool crushLocked  = run_crush_cnt > 0;      // run-meter guard-crush lockout
    bool canBlockNow  = !crushLocked && !inBlockStun;

    /* ---------------------------------------------------------
     * 2) Blocking logic
     * --------------------------------------------------------- */
    if (wantsBlock && canBlockNow)
    {
        // choose high/low guard pose based on crouch button
        State    = crouchBtn ? CharacterState.BlockingLow
                             : CharacterState.BlockingHigh;

        Velocity = Vector3.zero;               // stop every frame you guard
        run_meter = Mathf.Max(0, run_meter - RUN_DRAIN_PER_FRAME);
        return;                                // no walk/run while guarding
    }
    else if ((State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow)
             && !wantsBlock && !inBlockStun)
    {
        // button released and stun over → revert to idle/crouch
        State = crouchBtn ? CharacterState.Crouch : CharacterState.Idle;
    }
    else if (inBlockStun)
    {
        // still frozen by block-stun counter
        Velocity = Vector3.zero;
        return;
    }

    /* ---------------------------------------------------------
     * 3) If crouching (but not blocking), freeze X movement
     * --------------------------------------------------------- */
    if (crouchBtn)
    {
        State    = CharacterState.Crouch;
        Velocity = new Vector3(0f, Velocity.y, 0f);
        return;                                // no walk/run while crouched
    }
    else if (State == CharacterState.Crouch)
    {
        State = CharacterState.Idle;           // Down released
    }

    /* ---------------------------------------------------------
     * 4) RUN  (requires run button + forward + meter)
     * --------------------------------------------------------- */
    if (i.Run && i.Forward && run_meter > 0)
    {
        State       = CharacterState.Running;
        xDir        = dirSign * RUN_SPEED;
        run_meter   = Mathf.Max(0, run_meter - 1);
    }
    else if (State == CharacterState.Running)
    {
        State = CharacterState.Idle;
        if (run_meter == 0) run_crush_cnt = RUN_CRUSH_FRAMES;
    }

    /* ---------------------------------------------------------
     * 5) WALK
     * --------------------------------------------------------- */
    if (State == CharacterState.Idle || State == CharacterState.Walking)
    {
        if (i.Forward)
        {
            xDir  = dirSign * WALK_SPEED;
            State = CharacterState.Walking;
        }
        else if (i.Back)
        {
            xDir  = -dirSign * WALK_SPEED;
            State = CharacterState.Walking;
        }
        else if (State == CharacterState.Walking)
        {
            State = CharacterState.Idle;
        }
    }

    Velocity = new Vector3(xDir, Velocity.y, 0f);

    /* ---------------------------------------------------------
     * 6) JUMP  (from Idle / Walk / Run)
     * --------------------------------------------------------- */
    if (i.PressedUp && (State == CharacterState.Idle ||
                        State == CharacterState.Walking ||
                        State == CharacterState.Running))
    {
        float jx = 0f;
        if (i.Forward) jx =  dirSign * JUMP_VX;
        if (i.Back)    jx = -dirSign * JUMP_VX;

        Velocity = new Vector3(jx, JUMP_VY, 0f);
        State    = CharacterState.Jumping;
    }
}



    // ───────────────────────────────────────── Attacks
    void HandleAttacks()
    {
        if (State != CharacterState.Idle &&
            State != CharacterState.Walking &&
            State != CharacterState.Running &&
            State != CharacterState.Crouch) return;

        var i = InputBuf.State;

        if      (i.PressedHighPunch) StartAttack("HighPunch");
        else if (i.PressedHighKick)  StartAttack("HighKick");
        else if (i.PressedLowPunch)  StartAttack("LowPunch");
        else if (i.PressedLowKick)   StartAttack("LowKick");
        else if (InputBuf.DoubleTappedBack()) StartBackDash();
    }

    void StartAttack(string tag)
    {
        if (!moveTable.TryGet(tag, out _currentMove)) return;

        State        = CharacterState.Attacking;
        Phase        = MovePhase.Startup;
        _phaseFrames = _currentMove.startUp;
    }

    void StartBackDash()
    {
        if (!moveTable.TryGet("BackDash", out _currentMove)) return;

        State        = CharacterState.BackDash;
        Phase        = MovePhase.Startup;
        _phaseFrames = _currentMove.startUp;
        Velocity     = new Vector3(_facingRight ? -5f : 5f, 0f, 0f);
    }

    // ───────────────────────────────────────── Phases
    void AdvanceMovePhases()
    {
        if (State != CharacterState.Attacking && State != CharacterState.BackDash) return;
        if (--_phaseFrames > 0) return;

        switch (Phase)
        {
            case MovePhase.Startup:
                Phase        = MovePhase.Active;
                _phaseFrames = _currentMove.active;
                break;

            case MovePhase.Active:
                Phase        = MovePhase.Recovery;
                _phaseFrames = _currentMove.recovery;
                break;

            case MovePhase.Recovery:
                State = CharacterState.Idle;
                Phase = MovePhase.Startup;
                break;
        }
    }

    // ───────────────────────────────────────── Reactions (hit / block)
    public void ReceiveHit(MoveFrameData mv, bool blocked, FighterCharacterCore attacker)
    {
        /* UNBLOCKABLE strikes ignore guard */
        bool unblockable = mv.unblockable;
        if (blocked && !unblockable)
            ResolveBlock(mv);
        else
            ResolveHit(mv);
    }

    void ResolveBlock(MoveFrameData mv)
    {
        /* chip damage */
        bool noChip = mv.noChip;
        if (!noChip) Health -= mv.damage / 4;

        /* start/block counter */
        block_cnt = block_len_tbl[(int)mv.power];

        /* push-back */
        int push = push_dist_tbl[(int)mv.power];
        Velocity = new Vector3(_facingRight ? -push : push, 0f, 0f);

        /* freeze in high/low state depending on stance */
        State = (State == CharacterState.Crouch) ? CharacterState.BlockingLow
                                                 : CharacterState.BlockingHigh;
    }

    void ResolveHit(MoveFrameData mv)
    {
        Health -= mv.damage;

        if (reactionTable.TryGet(mv.reaction, out var r))
            hitStun = r.hitStun;

        if (mv.knockDown)
        {
            State      = CharacterState.Knockdown;
            knockTimer = 16;   // fixed for now; could use r.knockdownDelay
        }
        else
            State = CharacterState.HitStun;
    }

    public void SetState(CharacterState s) => State = s;
}

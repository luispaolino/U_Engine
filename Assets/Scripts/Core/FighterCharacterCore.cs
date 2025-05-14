using UnityEngine;
using UMK3;   // MoveTableSO, ReactionTableSO, ThrowTableSO

#region PSX-style enums & flags
public enum CharacterState
{
    Idle, Walking, Running, Crouch,
    BlockingHigh, BlockingLow,
    JumpStartup,          // 7-frame crouch before lift-off
    Jumping,              // airborne
    Attacking, HitStun, Knockdown,
    BackDash, Thrown
}

public enum MovePhase { Startup, Active, Recovery }

public enum AttackPower { Light, Medium, Heavy, Special }

[System.Flags]
public enum DmgFlag
{
    NONE       = 0,
    DF_NO_CHIP = 1 << 0,
    DF_UNBLOCK = 1 << 1     // same idea as SF_UNBLOCKABLE
}
#endregion

/// <summary>Pure gameplay logic for one fighter (no MonoBehaviour calls).</summary>
public class FighterCharacterCore
{
    /* ───────────────── Tunables (per-prefab) ───────────────── */
    [SerializeField] public float JumpUpVelocity = 7.0f;   // in world-units / sec
    [SerializeField] public float JumpForwardVelocity =  6.0f;
    [SerializeField] public float JumpBackVelocity    =  6.0f;
    public float GravityPerSecond    = 25.0f;
    public int   JumpStartupFrames   = 7;
    public float MinGroundY          = 0f;

    /* Walk / Run constants (unchanged) */
    const int   RUN_METER_MAX       = 128;
    const int   RUN_DRAIN_PER_FRAME = 2;
    const int   RUN_CRUSH_FRAMES    = 15;
    const float WALK_SPEED          = 3.5f;
    const float RUN_SPEED           = 6.0f;

    /* ───────────────── Public surface ───────────────── */
    public InputBuffer     InputBuf    { get; set; }
    public MoveFrameData   CurrentMove { get; private set; }
    public bool            IsPaused    { get; set; }
    public int             Health      { get; private set; }
    public CharacterState  State       { get; private set; }
    public MovePhase       Phase       { get; private set; }
    public Vector3         Velocity    { get; private set; }
    public Vector3         Position    { get; private set; }
    public bool            FacingRight => _facingRight;
    public bool            InJumpStartup => State == CharacterState.JumpStartup;

    /* ───────────────── Data tables ───────────────── */
    readonly MoveTableSO     moveTable;
    readonly ReactionTableSO reactionTable;
    readonly ThrowTableSO    throwTable;

    /* ───────────────── Block / push tables ───────── */
    readonly int[] block_len_tbl = { 6, 8, 10, 10 };
    readonly int[] push_dist_tbl = { 1, 2, 3, 3  };

    /* ───────────────── Runtime vars ─────────────── */
    bool _facingRight;
    int  _phaseFrames;
    int  hitStun, block_cnt, knockTimer, run_crush_cnt;
    int  run_meter;
    int  _jumpStartupCnt;

    int _landLock;                       // frames you must stay grounded
    const int LAND_RECOVERY_FRAMES = 6;  // tweak to match your Land clip


    /* ───────────────── ctor / reset ─────────────── */
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
        Position = Vector3.zero;

        hitStun = block_cnt = knockTimer = run_crush_cnt = 0;
        _jumpStartupCnt = 0;
    }

    public void SpawnAt(Vector2 pos, bool faceRight)
    {
        Position     = new Vector3(pos.x, pos.y, 0f);
        _facingRight = faceRight;
        Velocity     = Vector3.zero;
        State        = CharacterState.Idle;
    }

    public void SetFacing(bool right) => _facingRight = right;

    /* ───────────────── Fixed-tick main loop ─────── */
    public void FixedTick()
    {
        if (IsPaused) return;

        /* countdown timers */
        if (hitStun   > 0 && --hitStun   == 0) State = CharacterState.Idle;
        if (block_cnt > 0 && --block_cnt == 0) State = CharacterState.Idle;
        if (knockTimer> 0 && --knockTimer== 0) State = CharacterState.Idle;
        if (run_crush_cnt > 0) --run_crush_cnt;

        HandleMovement();
        HandleAttacks();
        AdvanceMovePhases();

        /* jump-squat countdown & launch */
        if (State == CharacterState.JumpStartup && --_jumpStartupCnt == 0)
            LaunchJump();

        /* gravity */
        if (State == CharacterState.Jumping)
            Velocity += Vector3.down * GravityPerSecond * Time.fixedDeltaTime;

        /* integrate */
        Position += Velocity * Time.fixedDeltaTime;

        /* landing */
        if (State == CharacterState.Jumping &&
            Position.y <= MinGroundY && Velocity.y <= 0f)
        {
            Position = new Vector3(Position.x, MinGroundY, 0f);
            Velocity = Vector3.zero;
            State    = CharacterState.Idle;
            _landLock = LAND_RECOVERY_FRAMES;
        }

        /* run-meter recharge */
        if (State != CharacterState.Running &&
            State != CharacterState.BlockingHigh &&
            State != CharacterState.BlockingLow &&
            run_meter < RUN_METER_MAX)
        {
            run_meter += 1;
        }
    }

    /* ───────────────── Movement & stance ────────── */
    void HandleMovement()
    {
        var   i       = InputBuf.State;
        int   dirSign = _facingRight ? 1 : -1;
        float xDir    = 0f;

        /* 0) CROUCH */
        bool crouchBtn = i.Down && State != CharacterState.Jumping &&
                                       State != CharacterState.HitStun;

        /* 1) Blocking */
        bool wantsBlock  = i.Block;
        bool inBlockStun = block_cnt > 0;
        bool crushLock   = run_crush_cnt > 0;
        bool canBlockNow = !crushLock && !inBlockStun;

        if (wantsBlock && canBlockNow)
        {
            State    = crouchBtn ? CharacterState.BlockingLow
                                 : CharacterState.BlockingHigh;
            Velocity = Vector3.zero;
            run_meter = Mathf.Max(0, run_meter - RUN_DRAIN_PER_FRAME);
            return;
        }
        else if ((State == CharacterState.BlockingHigh ||
                  State == CharacterState.BlockingLow) &&
                  !wantsBlock && !inBlockStun)
        {
            State = crouchBtn ? CharacterState.Crouch : CharacterState.Idle;
        }
        else if (inBlockStun)
        {
            Velocity = Vector3.zero;
            return;
        }

        /* 2) CROUCH freeze */
        if (crouchBtn)
        {
            State    = CharacterState.Crouch;
            Velocity = new Vector3(0f, Velocity.y, 0f);
            return;
        }
        else if (State == CharacterState.Crouch)
        {
            State = CharacterState.Idle;
        }

        /* 3) RUN */
        if (i.Run && i.Forward && run_meter > 0)
        {
            State      = CharacterState.Running;
            xDir       = dirSign * RUN_SPEED;
            run_meter  = Mathf.Max(0, run_meter - 1);
        }
        else if (State == CharacterState.Running)
        {
            State = CharacterState.Idle;
            if (run_meter == 0) run_crush_cnt = RUN_CRUSH_FRAMES;
        }

        /* 4) WALK */
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

        /* 5) JUMP (enter JumpStartup) */
        bool wantJump = (i.PressedUp || (i.Up && _landLock == 0)) &&
                (State == CharacterState.Idle ||
                 State == CharacterState.Walking ||
                 State == CharacterState.Running);

        if (wantJump)
        {
            State           = CharacterState.JumpStartup;
            _jumpStartupCnt = JumpStartupFrames;
            Velocity        = Vector3.zero;
        }

        /* apply horizontal speed */
        /* Keep horizontal speed intact while airborne */
        if (State != CharacterState.Jumping)
        Velocity = new Vector3(xDir, Velocity.y, 0f);

        if (_landLock > 0) _landLock--;
    }

    /* ───────────────── Lift-off helper ──────────── */
void LaunchJump()
{
    /* refresh input so we see the keys being held right now */
    InputBuf.Capture(FacingRight);
    var i        = InputBuf.State;

    int dirSign  = _facingRight ? 1 : -1;      // +1 when facing right
    float jx     = 0f;

    /* local forward / back decides horizontal speed */
    if (i.Forward) jx =  dirSign * JumpForwardVelocity;   // jump toward
    else if (i.Back) jx = -dirSign * JumpBackVelocity;    // jump away

    Velocity = new Vector3(jx, JumpUpVelocity, 0f);
    State    = CharacterState.Jumping;
}



    /* ───────────────── Attacks & BackDash ───────── */
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
        if (!moveTable.TryGet(tag, out var mv)) return;
        CurrentMove  = mv;

        State        = CharacterState.Attacking;
        Phase        = MovePhase.Startup;
        _phaseFrames = CurrentMove.startUp;
    }

    void StartBackDash()
    {
        if (!moveTable.TryGet("BackDash", out var mv)) return;
        CurrentMove  = mv;

        State        = CharacterState.BackDash;
        Phase        = MovePhase.Startup;
        _phaseFrames = CurrentMove.startUp;
        Velocity     = new Vector3(_facingRight ? -5f : 5f, 0f, 0f);
    }

    /* ───────────────── Advance attack phases ───── */
    void AdvanceMovePhases()
    {
        if (State != CharacterState.Attacking && State != CharacterState.BackDash) return;
        if (--_phaseFrames > 0) return;

        switch (Phase)
        {
            case MovePhase.Startup:
                Phase        = MovePhase.Active;
                _phaseFrames = CurrentMove.active;
                break;

            case MovePhase.Active:
                Phase        = MovePhase.Recovery;
                _phaseFrames = CurrentMove.recovery;
                break;

            case MovePhase.Recovery:
                State = CharacterState.Idle;
                Phase = MovePhase.Startup;
                break;
        }
    }

    /* ───────────────── Reaction logic (hit / block) */
    public void ReceiveHit(MoveFrameData mv, bool blocked, FighterCharacterCore attacker)
    {
        bool unblockable = mv.unblockable;
        if (blocked && !unblockable)
            ResolveBlock(mv);
        else
            ResolveHit(mv);
    }

    void ResolveBlock(MoveFrameData mv)
    {
        if (!mv.noChip)
            Health -= mv.damage / 4;

        block_cnt = block_len_tbl[(int)mv.power];

        int push = push_dist_tbl[(int)mv.power];
        Velocity = new Vector3(_facingRight ? -push : push, 0f, 0f);

        State = (State == CharacterState.Crouch)
              ? CharacterState.BlockingLow
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
            knockTimer = 16;
        }
        else
            State = CharacterState.HitStun;
    }

    public void SetState(CharacterState s) => State = s;
}

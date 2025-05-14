using UnityEngine;
using UMK3;

// Enums (DmgFlag, CharacterState, MovePhase, AttackPower) as previously defined...
[System.Flags]
public enum DmgFlag { NONE = 0, DF_NO_CHIP = 1 << 0, DF_UNBLOCK = 1 << 1 }
public enum CharacterState { Idle, Walking, Running, Crouch, BlockingHigh, BlockingLow, JumpStartup, Jumping, Attacking, HitStun, Knockdown, BackDash, Thrown }
public enum MovePhase { Startup, Active, Recovery }
public enum AttackPower { Light, Medium, Heavy, Special }

public class FighterCharacterCore
{
    /* ───────────────── Tunables (per-prefab) ───────────────── */
    public float JumpUpVelocity = 7.0f;
    public float JumpForwardVelocity = 6.0f;
    public float JumpBackVelocity = 6.0f;
    public float GravityPerSecond = 25.0f;
    public int JumpStartupFrames = 7; // This is frame-based, stays int
    public float MinGroundY = 0f;

    /* Meter System Settings (Time-Based) */
    public const float METER_CAPACITY_FLOAT = 128f;       // Max meter capacity
    private const float DRAIN_RATE_RUNNING_PER_SEC = 60f; // Units drained per second when running
    private const float DRAIN_RATE_BLOCKING_PER_SEC = 120f; // Units drained per second when blocking (2 units/frame @ 60Hz = 120 units/sec)
    private const float REFILL_RATE_PER_SEC = 60f;        // Units restored per second when not running/blocking
    private const float CRUSH_LOCKOUT_DURATION_SEC = 15f / 60f; // ~0.25 seconds (15 frames @ 60Hz)

    /* Other Constants */
    private const float WALK_SPEED = 3.5f;
    private const float RUN_SPEED = 6.0f;

    /* ───────────────── Public surface ───────────────── */
    public InputBuffer InputBuf { get; set; }
    public MoveFrameData CurrentMove { get; private set; }
    public bool IsMoveDataValid { get; private set; }
    public bool IsPaused { get; set; }
    public int Health { get; private set; } // Health usually int
    public CharacterState State { get; private set; }
    public MovePhase Phase { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 Position { get; private set; }
    public bool FacingRight => _facingRight;
    public bool InJumpStartup => State == CharacterState.JumpStartup;
    public float CurrentMeterValue => meter_value_float; // Public getter for the meter (float)
    public float CurrentCrushTimer => crush_timer_sec; // Public getter for crush timer (float)

    /* ───────────────── Data tables ───────────────── */
    private readonly MoveTableSO moveTable;
    private readonly ReactionTableSO reactionTable;
    private readonly ThrowTableSO throwTable;

    /* ───────────────── Block / push tables ───────── */
    private readonly int[] block_len_tbl = { 6, 8, 10, 10 }; // Frame-based, stays int
    private readonly int[] push_dist_tbl = { 1, 2, 3, 3 };

    /* ───────────────── Runtime vars ─────────────── */
    private bool _facingRight;
    private int _phaseFrames; // Frame-based
    private int hitStun, block_cnt, knockTimer; // Frame-based (block_cnt for block stun from hit)
    private float meter_value_float;      // Unified meter (float for time-based calculations)
    private float crush_timer_sec;        // Lockout timer after meter empties from running (float for time)
    private int _jumpStartupCnt;          // Frame-based
    private int _landLock;                // Frame-based
    private const int LAND_RECOVERY_FRAMES = 6; // Frame-based
    private bool _mustReleaseRunToRestart;

    public FighterCharacterCore(
        MoveTableSO mvTable,
        ReactionTableSO reactTable,
        ThrowTableSO thTable)
    {
        moveTable = mvTable;
        reactionTable = reactTable;
        throwTable = thTable;
        IsMoveDataValid = false;
        FullReset();
    }

    public void FullReset()
    {
        Health = 1000;
        meter_value_float = METER_CAPACITY_FLOAT;
        crush_timer_sec = 0f;
        State = CharacterState.Idle;
        Phase = MovePhase.Startup;
        Velocity = Vector3.zero;
        Position = Vector3.zero;
        IsMoveDataValid = false;
        _mustReleaseRunToRestart = false;

        hitStun = block_cnt = knockTimer = 0; // Removed run_cr_cnt as crush is time-based now
        _jumpStartupCnt = 0;
        _landLock = 0;
    }

    public void SpawnAt(Vector2 pos, bool faceRight)
    {
        Position = new Vector3(pos.x, pos.y, 0f);
        _facingRight = faceRight;
        Velocity = Vector3.zero;
        State = CharacterState.Idle;
        IsMoveDataValid = false;
        meter_value_float = METER_CAPACITY_FLOAT;
        crush_timer_sec = 0f;
        _mustReleaseRunToRestart = false;
    }

    public void SyncPosition(Vector3 newWorldPosition)
    {
        Position = newWorldPosition;
    }

    public void SetFacing(bool right) => _facingRight = right;

    public void FixedTick() // Logic still runs in FixedUpdate for physics consistency
    {
        if (IsPaused) return;

        float dt = Time.fixedDeltaTime; // Use fixedDeltaTime for consistency with FixedUpdate

        // Countdown frame-based timers
        if (hitStun > 0 && --hitStun == 0) { State = CharacterState.Idle; IsMoveDataValid = false; }
        if (block_cnt > 0 && --block_cnt == 0) { State = CharacterState.Idle; IsMoveDataValid = false; }
        if (knockTimer > 0 && --knockTimer == 0) { State = CharacterState.Idle; IsMoveDataValid = false; }
        
        // Countdown time-based crush_timer_sec
        if (crush_timer_sec > 0f)
        {
            crush_timer_sec -= dt;
            if (crush_timer_sec < 0f) crush_timer_sec = 0f;
        }

        // Check if run button is released to clear the _mustReleaseRunToRestart flag
        if (InputBuf != null && !InputBuf.State.Run)
        {
            _mustReleaseRunToRestart = false;
        }

        // Meter handling (drain/recharge)
        bool isTryingToRun = (InputBuf.State.Run && InputBuf.State.Forward); // Player intends to run
        bool isCurrentlyRunning = (State == CharacterState.Running && isTryingToRun);
        bool isCurrentlyBlocking = (State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow);

        if (isCurrentlyRunning && crush_timer_sec <= 0f) // Drain for running, only if not in crush
        {
            meter_value_float -= DRAIN_RATE_RUNNING_PER_SEC * dt;
            if (meter_value_float <= 0f)
            {
                meter_value_float = 0f;
                crush_timer_sec = CRUSH_LOCKOUT_DURATION_SEC; // Activate crush lockout
                _mustReleaseRunToRestart = true; // Meter emptied while trying to run
            }
        }
        else if (isCurrentlyBlocking && meter_value_float > 0f) // Drain for blocking
        {
            // No crush timer check for blocking drain, it always drains if blocking and has meter
            meter_value_float -= DRAIN_RATE_BLOCKING_PER_SEC * dt;
            if (meter_value_float < 0f) meter_value_float = 0f;
        }
        else if (!isCurrentlyRunning && !isCurrentlyBlocking && crush_timer_sec <= 0f) // Recharge only if not running, not blocking, and not in crush
        {
            if (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch) // Only recharge in these states
            {
                if (meter_value_float < METER_CAPACITY_FLOAT)
                {
                    meter_value_float += REFILL_RATE_PER_SEC * dt;
                    if (meter_value_float > METER_CAPACITY_FLOAT) meter_value_float = METER_CAPACITY_FLOAT;
                }
            }
        }

        HandleMovement();
        HandleAttacks();
        AdvanceMovePhases();

        if (State == CharacterState.JumpStartup && --_jumpStartupCnt == 0)
            LaunchJump();

        if (State == CharacterState.Jumping)
            Velocity += Vector3.down * GravityPerSecond * dt; // Use dt

        Position += Velocity * dt; // Use dt

        if (State == CharacterState.Jumping && Position.y <= MinGroundY && Velocity.y <= 0f)
        {
            Position = new Vector3(Position.x, MinGroundY, 0f);
            Velocity = Vector3.zero;
            State = CharacterState.Idle;
            IsMoveDataValid = false;
            _landLock = LAND_RECOVERY_FRAMES;
        }
    }

    void HandleMovement()
    {
        // ... (Gate for appropriate states remains the same) ...
        if (!(State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running ||
              State == CharacterState.Crouch || State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow ||
              State == CharacterState.Jumping || State == CharacterState.JumpStartup) &&
             (State != CharacterState.BackDash && State != CharacterState.Attacking)
            )
        {
            return;
        }
        
        var i = InputBuf.State;
        int dirSign = _facingRight ? 1 : -1;
        float xDir = 0f;

        // Blocking Logic
        bool crouchBtn = i.Down && State != CharacterState.Jumping && State != CharacterState.HitStun;
        bool wantsBlock = i.Block;
        bool inBlockStun = block_cnt > 0;
        bool canPhysicallyBlock = !inBlockStun && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch || State == CharacterState.Running || State == CharacterState.BlockingLow || State == CharacterState.BlockingHigh);

        if (wantsBlock && canPhysicallyBlock)
        {
            if (meter_value_float > 0f) // Can only maintain block if has meter
            {
                State = crouchBtn ? CharacterState.BlockingLow : CharacterState.BlockingHigh;
                Velocity = Vector3.zero; // Stop movement when initiating/holding block
                // Meter drain for blocking is handled in FixedTick
                return; // Precedence for blocking
            }
            else // Tried to block but no meter
            {
                // Transition to a vulnerable state or just Idle/Crouch? For now, Idle/Crouch.
                State = crouchBtn ? CharacterState.Crouch : CharacterState.Idle;
            }
        }
        else if ((State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow))
        {
            // If was blocking, but now releases button OR runs out of meter
            if (!wantsBlock || meter_value_float <= 0f)
            {
                State = crouchBtn ? CharacterState.Crouch : CharacterState.Idle;
            }
            // Else, continue blocking (input held, has meter)
        }
        else if (inBlockStun)
        {
            return; // Cannot move or act during block stun
        }

        // Crouch Logic (if not blocking)
        if (crouchBtn && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch))
        {
            State = CharacterState.Crouch;
            xDir = 0f;
        }
        else if (State == CharacterState.Crouch && !crouchBtn)
        {
            State = CharacterState.Idle;
        }

        // Running Logic
        bool attemptingToRunInput = i.Run && i.Forward; // Player is holding run inputs
        bool canStartOrContinueRun = attemptingToRunInput && meter_value_float > 0f && crush_timer_sec <= 0f && !_mustReleaseRunToRestart;

        if (State == CharacterState.Running)
        {
            if (canStartOrContinueRun) // Continue running
            {
                xDir = dirSign * RUN_SPEED;
                // Meter drain handled in FixedTick
            }
            else // Stop running (input release, no meter, crush, or needs re-press)
            {
                if (i.Forward) // If still holding forward, fallback to walking
                {
                    State = CharacterState.Walking;
                }
                else // Otherwise, idle
                {
                    State = CharacterState.Idle;
                }
            }
        }
        // START running from Idle or Walking
        else if (canStartOrContinueRun && (State == CharacterState.Idle || State == CharacterState.Walking))
        {
            State = CharacterState.Running;
            xDir = dirSign * RUN_SPEED;
            // Meter drain for the first tick of running will be handled in FixedTick
        }

        // Walking Logic (if not running or in other exclusive states)
        if (State == CharacterState.Idle || State == CharacterState.Walking)
        {
            if (i.Forward)
            {
                xDir = dirSign * WALK_SPEED;
                State = CharacterState.Walking;
            }
            else if (i.Back)
            {
                xDir = -dirSign * WALK_SPEED;
                State = CharacterState.Walking;
            }
            else if (State == CharacterState.Walking)
            {
                State = CharacterState.Idle;
                xDir = 0f;
            }
        }
        
        // ... (Jump Logic, Apply horizontal velocity logic remain the same as previous version) ...
        bool canInitiateJump = _landLock == 0 && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running || State == CharacterState.Crouch);
        bool wantJump = (i.PressedUp || (i.Up && canInitiateJump)) && canInitiateJump;
        if (wantJump) { State = CharacterState.JumpStartup; _jumpStartupCnt = JumpStartupFrames; Velocity = Vector3.zero; xDir = 0f; }
        if (State != CharacterState.Jumping && State != CharacterState.JumpStartup && State != CharacterState.BackDash && State != CharacterState.Attacking && State != CharacterState.BlockingHigh && State != CharacterState.BlockingLow && State != CharacterState.HitStun && State != CharacterState.Knockdown) { Velocity = new Vector3(xDir, Velocity.y, 0f); } else if (State == CharacterState.JumpStartup) { Velocity = new Vector3(0f, Velocity.y, 0f); }
        if (_landLock > 0) _landLock--;
    }

    // ... (LaunchJump, HandleAttacks, StartAttack, StartBackDash, AdvanceMovePhases, ReceiveHit, ResolveBlock, ResolveHit, SetState methods remain the same as the last full version provided)
    // Ensure they use the correct field names if any were changed (e.g., meter_value_float, crush_timer_sec)
    // And that they correctly set IsMoveDataValid.
    void LaunchJump() { var i = InputBuf.State; int dirSign = _facingRight ? 1 : -1; float jx = 0f; if (i.Forward) jx = dirSign * JumpForwardVelocity; else if (i.Back) jx = -dirSign * JumpBackVelocity; Velocity = new Vector3(jx, JumpUpVelocity, 0f); State = CharacterState.Jumping; IsMoveDataValid = false; }
    void HandleAttacks() { if (State != CharacterState.Idle && State != CharacterState.Walking && State != CharacterState.Running && State != CharacterState.Crouch) return; var i = InputBuf.State; if (i.PressedHighPunch) StartAttack("HighPunch"); else if (i.PressedHighKick) StartAttack("HighKick"); else if (i.PressedLowPunch) StartAttack("LowPunch"); else if (i.PressedLowKick) StartAttack("LowKick"); else if (InputBuf.DoubleTappedBack()) StartBackDash(); }
    void StartAttack(string tag) { if (!moveTable.TryGet(tag, out var mv)) { IsMoveDataValid = false; return; } CurrentMove = mv; IsMoveDataValid = true; State = CharacterState.Attacking; Phase = MovePhase.Startup; _phaseFrames = mv.startUp; Velocity = Vector3.zero; }
    void StartBackDash() { if (!moveTable.TryGet("BackDash", out var mv)) { IsMoveDataValid = false; return; } CurrentMove = mv; IsMoveDataValid = true; State = CharacterState.BackDash; Phase = MovePhase.Startup; _phaseFrames = mv.startUp; Velocity = new Vector3(_facingRight ? -5f : 5f, 0f, 0f); } // Assuming MoveFrameData doesn't have travelSpeed
    void AdvanceMovePhases() { if (!IsMoveDataValid || (State != CharacterState.Attacking && State != CharacterState.BackDash)) return; _phaseFrames--; if (_phaseFrames > 0) return; switch (Phase) { case MovePhase.Startup: Phase = MovePhase.Active; _phaseFrames = CurrentMove.active; break; case MovePhase.Active: Phase = MovePhase.Recovery; _phaseFrames = CurrentMove.recovery; if (State == CharacterState.BackDash) Velocity = Vector3.zero; break; case MovePhase.Recovery: State = CharacterState.Idle; Phase = MovePhase.Startup; IsMoveDataValid = false; Velocity = Vector3.zero; break; } }
    public void ReceiveHit(MoveFrameData mv, bool blocked, FighterCharacterCore attacker) { bool isUnblockable = mv.unblockable; if (blocked && !isUnblockable) ResolveBlock(mv); else ResolveHit(mv); } // Assuming bool unblockable in MoveFrameData
    void ResolveBlock(MoveFrameData mv) { if (!mv.noChip) Health -= mv.damage / 4; block_cnt = block_len_tbl[(int)mv.power]; float pushDistance = push_dist_tbl[(int)mv.power]; Velocity = new Vector3(_facingRight ? -pushDistance * 5f : pushDistance * 5f, 0f, 0f); State = (State == CharacterState.Crouch || State == CharacterState.BlockingLow) ? CharacterState.BlockingLow : CharacterState.BlockingHigh; IsMoveDataValid = false; } // Assuming bool noChip in MoveFrameData
    void ResolveHit(MoveFrameData mv) { Health -= mv.damage; IsMoveDataValid = false; if (reactionTable.TryGet(mv.reaction, out var r)) { hitStun = r.hitStun; } else { hitStun = 10; } if (mv.knockDown) { State = CharacterState.Knockdown; knockTimer = 16; } else { State = CharacterState.HitStun; } } // Assuming no knockdownDuration in MoveFrameData
    public void SetState(CharacterState s) { State = s; if (s != CharacterState.Attacking && s != CharacterState.BackDash) { IsMoveDataValid = false; } }

}
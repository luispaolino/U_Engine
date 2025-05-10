using UnityEngine;

public enum CharacterState
{
    Idle, Walking, Running, Crouch, Jumping,
    Attacking, ThrowStartup, Throwing,
    HitStun, AirHitStun, Knockdown, GetUp,
    Recovery, BackDash, WakeRoll, Thrown
}

public enum MovePhase { Startup, Active, Recovery }

public class FighterCharacterCore
{
    // ─── Public API ────────────────────────────────────────────────
    public InputBuffer     InputBuf    { get; set; }
    public MoveFrameData   CurrentMove => _currentMove;
    public bool            IsPaused    { get; set; }
    public int             Health      { get; private set; }
    public bool            FacingRight => _facingRight;
    public CharacterState  State       { get; private set; }
    public MovePhase       Phase       { get; private set; }
    public Vector3         Velocity    { get; private set; }
    public bool            IsGrounded  { get; set; } = true;

    // ─── Data Tables ───────────────────────────────────────────────
    readonly MoveTableSO     _moveTable;
    readonly ReactionTableSO _reactionTable;
    readonly ThrowTableSO    _throwTable;

    // ─── Internal State ────────────────────────────────────────────
    MoveFrameData _currentMove;
    ThrowData     _currentThrow;
    int           _frameLeft;
    int blockStun, hitStun, airStun, lyingTimer, escapeWindow;
    bool _facingRight;

    // ─── Tuning ─────────────────────────────────────────────────────
    public float walkSpeed    = 2f;
    public float runSpeed     = 4f;
    public float jumpVelocity = 8f;
    public float gravity      = 20f;

    public FighterCharacterCore(
        MoveTableSO moves,
        ReactionTableSO reactions,
        ThrowTableSO throwsTable
    ) {
        _moveTable     = moves;
        _reactionTable = reactions;
        _throwTable    = throwsTable;
        FullReset();
    }

    public void FullReset()
    {
        Health        = 1000;
        State         = CharacterState.Idle;
        Phase         = MovePhase.Startup;
        Velocity      = Vector3.zero;
        _facingRight  = true;
        blockStun     = hitStun = airStun = lyingTimer = escapeWindow = 0;
    }

    public void SpawnAt(Vector2 position, bool faceRight)
    {
        _facingRight = faceRight;
        Velocity     = Vector3.zero;
        State        = CharacterState.Idle;
        Phase        = MovePhase.Startup;
    }

    // ─── Main Loop ─────────────────────────────────────────────────
    public void FixedTick()
    {
        if (IsPaused) return;

        // 1) timers
        if (blockStun    > 0) blockStun--;
        if (hitStun      > 0) hitStun--;
        if (airStun      > 0) airStun--;
        if (lyingTimer   > 0) lyingTimer--;
        if (escapeWindow > 0) escapeWindow--;

        // 2) freeze if stunned or in throw startup
        if (blockStun>0 || hitStun>0 || airStun>0 || State==CharacterState.ThrowStartup)
            return;

        var f = InputBuf.State;

        // 3) handle attacks & throws & jump-attacks
        if (TryHandleAttacks(f)) return;

        // 4) crouch enter/exit
        if (IsGrounded && State==CharacterState.Idle && f.Down)
        {
            State    = CharacterState.Crouch;
            Velocity = Vector3.zero;
            return;
        }
        if (State==CharacterState.Crouch && !f.Down)
            State = CharacterState.Idle;

        // 5) jump
        if (TryHandleJump(f)) return;

        // 6) movement (forward/back)
        if (TryHandleMovement(f)) return;

        // 7) advance any active move or throw
        AdvanceMovePhases();
        AdvanceThrowPhases();

        // 8) gravity if in air
        if (State==CharacterState.Jumping)
            ApplyGravity();
    }

    // ─── Input Handling ─────────────────────────────────────────────
    bool TryHandleAttacks(InputBuffer.Frame f)
    {
        // only from Idle/Walking/Crouch
        if (State!=CharacterState.Idle
         && State!=CharacterState.Walking
         && State!=CharacterState.Crouch)
            return false;

        // 1) crouch + HP => Uppercut
        if (State==CharacterState.Crouch && f.PressedHighPunch)
        {
            StartAttack("Uppercut");
            return true;
        }

        // 2) ground normals
        if (f.PressedHighPunch){ StartAttack("HighPunch");   return true;}
        if (f.PressedHighKick) { StartAttack("HighKick");    return true;}
        if (f.PressedLowPunch) { StartAttack("LowPunch");    return true;}
        if (f.PressedLowKick)  { StartAttack("LowKick");     return true;}

        // 3) combos
        if (f.PressedLowPunch && f.PressedLowKick)   { StartAttack("SweepKick");    return true; }
        if (f.PressedHighPunch&& f.PressedHighKick)  { StartAttack("Uppercut");     return true; }
        if (f.PressedHighKick && f.PressedLowKick)   { StartAttack("Roundhouse");   return true; }

        // 4) throws
        if (f.PressedHighPunch && f.PressedHighKick) { StartThrow("Throw_Fwd");     return true; }
        if (f.PressedLowPunch  && f.PressedLowKick)  { StartThrow("Throw_Rev");     return true; }

        // 5) jump attacks
        if (State==CharacterState.Jumping)
        {
            if (f.PressedHighPunch)
            {
                var tag = f.Up
                        ? "JumpPunch_U"
                        : f.Right == _facingRight
                            ? "JumpPunch_F"
                            : "JumpPunch_B";
                StartAttack(tag);
                return true;
            }
            if (f.PressedHighKick)
            {
                var tag = f.Up
                        ? "JumpKick_U"
                        : f.Right == _facingRight
                            ? "JumpKick_F"
                            : "JumpKick_B";
                StartAttack(tag);
                return true;
            }
        }

        // 6) back-dash
        if (InputBuf.DoubleTappedBack(_facingRight))
        {
            StartAttack("BackDash");
            return true;
        }

        // 7) wake-roll (crouch + LK)
        if (State==CharacterState.Crouch && f.PressedLowKick)
        {
            StartAttack("WakeupRoll");
            return true;
        }

        return false;
    }

    bool TryHandleJump(InputBuffer.Frame f)
    {
        if (State==CharacterState.Idle && IsGrounded && f.Up)
        {
            if (!_moveTable.TryGet("JumpStart", out var m)) return false;
            State        = CharacterState.Jumping;
            Phase        = MovePhase.Startup;
            _currentMove = m;
            _frameLeft   = m.startUp;
            Velocity     = new Vector3(0, jumpVelocity, 0);
            IsGrounded   = false;
            return true;
        }
        return false;
    }

    bool TryHandleMovement(InputBuffer.Frame f)
    {
        int dir = f.Left ? -1 : f.Right ? 1 : 0;
        if ((State==CharacterState.Idle
          || State==CharacterState.Walking
          || State==CharacterState.Running
          || State==CharacterState.Crouch)
          && IsGrounded)
        {
            if (dir != 0)
            {
                // facing stays fixed; no automatic flip
                if (f.Run && dir == (_facingRight ? 1 : -1))
                {
                    State    = CharacterState.Running;
                    Velocity = new Vector3(dir * runSpeed, 0, 0);
                }
                else
                {
                    State    = CharacterState.Walking;
                    Velocity = new Vector3(dir * walkSpeed, 0, 0);
                }
                return true;
            }
            else if (State==CharacterState.Walking
                  || State==CharacterState.Running)
            {
                State    = CharacterState.Idle;
                Velocity = Vector3.zero;
                return true;
            }
        }
        return false;
    }

    // ─── Phase Advancement ─────────────────────────────────────────
    void AdvanceMovePhases()
    {
        if (State==CharacterState.Attacking
         || State==CharacterState.BackDash
         || State==CharacterState.WakeRoll)
        {
            _frameLeft--;
            if (_frameLeft <= 0)
            {
                switch (Phase)
                {
                    case MovePhase.Startup:
                        Phase      = _currentMove.active > 0
                                     ? MovePhase.Active
                                     : MovePhase.Recovery;
                        _frameLeft = Phase==MovePhase.Active
                                   ? _currentMove.active
                                   : _currentMove.recovery;
                        break;
                    case MovePhase.Active:
                        Phase      = MovePhase.Recovery;
                        _frameLeft = _currentMove.recovery;
                        break;
                    case MovePhase.Recovery:
                        State      = CharacterState.Idle;
                        Velocity   = Vector3.zero;
                        break;
                }
            }
        }
    }

    void AdvanceThrowPhases()
    {
        if (State==CharacterState.ThrowStartup)
        {
            _frameLeft--;
            if (_frameLeft <= 0)
            {
                State      = CharacterState.Throwing;
                _frameLeft = _currentThrow.execute;
            }
        }
        else if (State==CharacterState.Throwing)
        {
            _frameLeft--;
            if (_frameLeft <= 0)
            {
                State      = CharacterState.Recovery;
                _frameLeft = _currentThrow.recovery;
            }
        }
    }

    void ApplyGravity()
    {
        Velocity += Vector3.down * gravity * Time.fixedDeltaTime;
        if (Velocity.y <= 0 && IsGrounded)
        {
            if (_moveTable.TryGet("JumpLand", out var land))
            {
                State        = CharacterState.Recovery;
                Phase        = MovePhase.Startup;
                _currentMove = land;
                _frameLeft   = land.startUp;
                Velocity     = Vector3.zero;
                IsGrounded   = true;
            }
        }
    }

    // ─── Attack / Throw Helpers ────────────────────────────────────
    void StartAttack(string tag)
    {
        Debug.Log($"[Core] StartAttack called with tag='{tag}'");
        if (!_moveTable.TryGet(tag, out var m))
        {
            Debug.LogWarning($"[Core] MoveTable has no '{tag}'");
            return;
        }
        Velocity      = Vector3.zero;      // lock movement
        State         = CharacterState.Attacking;
        Phase         = MovePhase.Startup;
        _currentMove  = m;
        _frameLeft    = m.startUp;
    }

    void StartThrow(string tag)
    {
        if (!_throwTable.TryGet(tag, out var t)) return;
        State         = CharacterState.ThrowStartup;
        Phase         = MovePhase.Startup;
        _currentThrow = t;
        _frameLeft    = t.startUp;
        escapeWindow  = t.escapeWindow;
    }

    // ─── Hit Reaction ───────────────────────────────────────────────
    public void ReceiveHit(MoveFrameData move, bool blocked, FighterCharacterCore attacker)
    {
        Health -= blocked ? 0 : move.damage;
        if (_reactionTable.TryGet(move.reaction, out var r))
        {
            blockStun = blocked ? r.blockStun : 0;
            hitStun   = blocked ? 0         : r.hitStun;
            airStun   = (!blocked && move.knockDown) ? r.airStun : 0;
        }
        State     = CharacterState.HitStun;
        Phase     = MovePhase.Startup;
        _frameLeft = blocked ? r.blockStun : r.hitStun;
    }

    public void SetState(CharacterState st)
    {
        State    = st;
        Phase    = MovePhase.Startup;
        _frameLeft = 0;
        Velocity = Vector3.zero;
    }
}

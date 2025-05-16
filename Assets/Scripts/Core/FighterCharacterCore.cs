using UnityEngine;
using UMK3;   // Assuming this namespace contains MoveTableSO, ReactionTableSO, ThrowTableSO

// Enums (Ensure these are defined correctly, possibly in a shared file or UMK3 namespace)
[System.Flags]
public enum DmgFlag { NONE = 0, DF_NO_CHIP = 1 << 0, DF_UNBLOCK = 1 << 1 }

public enum CharacterState
{
    Idle, Walking, Running, Crouch,
    BlockingHigh, BlockingLow,
    JumpStartup, Jumping,
    Attacking, HitStun, Knockdown, Thrown,
    BackDash,
    MercyReceiving,
    FinishHimVictim,
    FinishHimWinner
}
public enum MovePhase { Startup, Active, Recovery }
public enum AttackPower { Light, Medium, Heavy, Special }
public enum Gender { Male, Female, Other }


public class FighterCharacterCore
{
    /* ───────────────── Tunables & Constants ───────────────── */
    // These are now primarily read from MovementStatsSO, but keeping defaults here for clarity if SO is null
    // However, the design expects MovementStatsSO to be non-null.
    // public float JumpUpVelocity = 7.0f; (from SO)
    // ... other movement stats from SO ...

    public const int MAX_HEALTH = 1000;
    private const float MERCY_HEALTH_RESTORE_PERCENT = 0.25f;
    public const int MERCY_WINDOW_FRAMES = 180;
    private const int MERCY_BLOCK_TAPS_REQUIRED = 4;

    public const float METER_CAPACITY_FLOAT = 128f;
    private const float DRAIN_RATE_RUNNING_PER_SEC = 60f;
    private const float DRAIN_RATE_BLOCKING_PER_SEC = 120f;
    private const float REFILL_RATE_PER_SEC = 60f;
    private const float CRUSH_LOCKOUT_DURATION_SEC = 15f / 60f;

    private const int LAND_RECOVERY_FRAMES = 6;

    /* ───────────────── Public surface ───────────────── */
    public InputBuffer InputBuf { get; set; }
    public MoveFrameData CurrentMove { get; private set; }
    public bool IsMoveDataValid { get; private set; }
    public bool IsPaused { get; set; }
    public int Health { get; private set; }
    public CharacterState State { get; private set; }
    public MovePhase Phase { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 Position { get; private set; }
    public bool FacingRight => _facingRight;
    public bool InJumpStartup => State == CharacterState.JumpStartup;
    public float CurrentMeterValue => meter_value_float;
    public float CurrentCrushTimer => crush_timer_sec;
    public bool IsMercyEligibleThisRound { get; private set; }
    public bool CanPerformMercyThisMatch { get; private set; }
    public int MercyWindowTimeRemaining => mercyWindowTimerFrames;
    public bool IsKOFriendly => isKOfriendly;
    public bool BlockedThisRound { get; private set; }
    public float MinGroundY { get; set; }
    public CharacterInfoSO CharInfo => characterInformation;

    /* ───────────────── Data tables & Runtime vars ───────────────── */
    private readonly CharacterInfoSO characterInformation;
    private readonly MovementStatsSO movementStats;
    private readonly SpecialMovesSO specialMovesList;
    private readonly ReactionTableSO reactionTable;
    private readonly ThrowTableSO throwTable;
    private readonly int[] block_len_tbl = { 6, 8, 10, 10 };
    private readonly int[] push_dist_tbl = { 1, 2, 3, 3 };

    private bool _facingRight; private int _phaseFrames; private int hitStun, block_cnt, knockTimer;
    private float meter_value_float; private float crush_timer_sec; private int _jumpStartupCnt;
    private int _landLock; private bool _mustReleaseRunToRestart;
    private int mercyWindowTimerFrames; private int blockTapCountForMercy; private bool isKOfriendly;

    public FighterCharacterCore(CharacterInfoSO charInfo, MovementStatsSO moveStats, SpecialMovesSO specialMoves, ReactionTableSO reactTable, ThrowTableSO thTable)
    {
        this.characterInformation = charInfo; this.movementStats = moveStats; this.specialMovesList = specialMoves;
        this.reactionTable = reactTable; this.throwTable = thTable;
        if (this.characterInformation == null) Debug.LogError("Core: CharacterInfoSO null!");
        if (this.movementStats == null) Debug.LogError("Core: MovementStatsSO null!");
        if (this.specialMovesList == null) Debug.LogWarning("Core: SpecialMovesSO null.");
        IsMoveDataValid = false;
    }

    public void FullMatchReset() { CanPerformMercyThisMatch = true; FullRoundReset(); }
    public void FullRoundReset() { Health = MAX_HEALTH; meter_value_float = METER_CAPACITY_FLOAT; crush_timer_sec = 0f; State = CharacterState.Idle; Phase = MovePhase.Startup; Velocity = Vector3.zero; IsMoveDataValid = false; _mustReleaseRunToRestart = false; IsMercyEligibleThisRound = true; mercyWindowTimerFrames = 0; blockTapCountForMercy = 0; isKOfriendly = false; hitStun = 0; block_cnt = 0; knockTimer = 0; _jumpStartupCnt = 0; _landLock = 0; BlockedThisRound = false; }
    public void SpawnAt(Vector2 pos, bool faceRight) { Position = new Vector3(pos.x, pos.y, 0f); _facingRight = faceRight; }
    public void SyncPosition(Vector3 nP) { Position = nP; } public void SetFacing(bool r) { _facingRight = r; }
    public void SetKOAsFriendly(bool fKO) { isKOfriendly = fKO; } public void SetMercyEligibility(bool iE) { IsMercyEligibleThisRound = iE; }

    public void FixedTick()
    {
        if (IsPaused || movementStats == null) return; float dt = Time.fixedDeltaTime;
        if (hitStun > 0 && --hitStun == 0) { State = CharacterState.Idle; IsMoveDataValid = false; }
        if (block_cnt > 0 && --block_cnt == 0) { State = CharacterState.Idle; IsMoveDataValid = false; }
        if (knockTimer > 0 && --knockTimer == 0) { if (State == CharacterState.Knockdown || State == CharacterState.FinishHimVictim || State == CharacterState.MercyReceiving) { State = CharacterState.Idle; SetMercyEligibility(false); } IsMoveDataValid = false; }
        if (crush_timer_sec > 0f) { crush_timer_sec -= dt; if (crush_timer_sec < 0f) crush_timer_sec = 0f; }
        if (mercyWindowTimerFrames > 0) { mercyWindowTimerFrames--; if (mercyWindowTimerFrames == 0 && Health <= 0 && State == CharacterState.MercyReceiving) { State = CharacterState.Knockdown; knockTimer = 30; SetMercyEligibility(false); Debug.Log((InputBuf?.profile?.name ?? "P") + " Mercy window expired."); } }
        if (State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow) BlockedThisRound = true;
        if (InputBuf != null && !InputBuf.State.Run) _mustReleaseRunToRestart = false;
        bool iTR = (InputBuf != null && InputBuf.State.Run && InputBuf.State.Forward); bool iER = (State == CharacterState.Running && iTR && meter_value_float > 0f && crush_timer_sec <= 0f && !_mustReleaseRunToRestart); bool iCIBS = (State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow); bool iPHB = (InputBuf != null && InputBuf.State.Block);
        if (iER) { meter_value_float -= DRAIN_RATE_RUNNING_PER_SEC * dt; if (meter_value_float <= 0f) { meter_value_float = 0f; crush_timer_sec = CRUSH_LOCKOUT_DURATION_SEC; _mustReleaseRunToRestart = true; } }
        else if (iCIBS && iPHB && meter_value_float > 0f) { meter_value_float -= DRAIN_RATE_BLOCKING_PER_SEC * dt; if (meter_value_float < 0f) meter_value_float = 0f; }
        else if (!iER && !iCIBS && crush_timer_sec <= 0f) { if (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch) { if (meter_value_float < METER_CAPACITY_FLOAT) { meter_value_float += REFILL_RATE_PER_SEC * dt; if (meter_value_float > METER_CAPACITY_FLOAT) meter_value_float = METER_CAPACITY_FLOAT; } } }
        HandleMovement(); HandleAttacks(); AdvanceMovePhases();
        if (State == CharacterState.JumpStartup && --_jumpStartupCnt == 0) LaunchJump();
        if (State == CharacterState.Jumping) Velocity += Vector3.down * movementStats.gravityPerSecond * dt; Position += Velocity * dt;
        if (State == CharacterState.Jumping && Position.y <= MinGroundY && Velocity.y <= 0f) { Position = new Vector3(Position.x, MinGroundY, 0f); Velocity = Vector3.zero; State = CharacterState.Idle; IsMoveDataValid = false; _landLock = LAND_RECOVERY_FRAMES; }
    }

    void HandleMovement()
    {
        if (movementStats == null) return;
        if ((Health <= 0 && State != CharacterState.MercyReceiving && State != CharacterState.FinishHimVictim) || (State == CharacterState.HitStun || State == CharacterState.Thrown || (State == CharacterState.Knockdown && knockTimer > 0))) { if (State != CharacterState.Jumping && State != CharacterState.JumpStartup) Velocity = new Vector3(0, Velocity.y, 0); return; }
        if (State == CharacterState.MercyReceiving || State == CharacterState.FinishHimVictim || State == CharacterState.FinishHimWinner) { Velocity = Vector3.zero; return; }
        if (State == CharacterState.Attacking && IsMoveDataValid) { bool canMove = false; if (Phase == MovePhase.Startup && CurrentMove.canMoveDuringStartUp) canMove = true; else if (Phase == MovePhase.Active && CurrentMove.canMoveDuringActive) canMove = true; if (!canMove) { if (State != CharacterState.Jumping && State != CharacterState.JumpStartup) Velocity = new Vector3(0, Velocity.y, 0); return; } }
        var i = InputBuf.State; int dS = _facingRight ? 1 : -1; float xD = 0; bool cB = i.Down && State != CharacterState.Jumping && State != CharacterState.HitStun; bool wB = i.Block; bool iBSH = block_cnt > 0; bool cPB = !iBSH && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch || State == CharacterState.Running || State == CharacterState.BlockingLow || State == CharacterState.BlockingHigh); if (wB && cPB) { if (meter_value_float > 0f) { State = cB ? CharacterState.BlockingLow : CharacterState.BlockingHigh; Velocity = Vector3.zero; return; } else { if (State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow) State = cB ? CharacterState.Crouch : CharacterState.Idle; } } else if ((State == CharacterState.BlockingHigh || State == CharacterState.BlockingLow)) { if (!wB || meter_value_float <= 0f) State = cB ? CharacterState.Crouch : CharacterState.Idle; } else if (iBSH) return;
        if (State == CharacterState.Crouch || State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running) { if (cB) { State = CharacterState.Crouch; xD = 0; } else if (State == CharacterState.Crouch && !cB) State = CharacterState.Idle; }
        bool attR = i.Run && i.Forward; bool canSOR = attR && meter_value_float > 0f && crush_timer_sec <= 0f && !_mustReleaseRunToRestart; if (State == CharacterState.Running) { if (canSOR) xD = dS * movementStats.runVelocity; else State = i.Forward ? CharacterState.Walking : CharacterState.Idle; } else if (canSOR && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Crouch)) { State = CharacterState.Running; xD = dS * movementStats.runVelocity; }
        if (State == CharacterState.Idle || State == CharacterState.Walking) { if (i.Forward) { xD = dS * movementStats.walkForwardVelocity; State = CharacterState.Walking; } else if (i.Back) { xD = -dS * movementStats.walkBackVelocity; State = CharacterState.Walking; } else if (State == CharacterState.Walking) { State = CharacterState.Idle; xD = 0; } }
        bool cIJ = _landLock == 0 && (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running || State == CharacterState.Crouch); bool wantJ = (i.PressedUp || (i.Up && cIJ)) && cIJ; if (wantJ) { State = CharacterState.JumpStartup; _jumpStartupCnt = movementStats.jumpStartupFrames; Velocity = Vector3.zero; xD = 0; }
        bool allowInputDrivenXVel = (State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running || State == CharacterState.Crouch || State == CharacterState.FinishHimWinner); if (State == CharacterState.Attacking && IsMoveDataValid) { if ((Phase == MovePhase.Startup && CurrentMove.canMoveDuringStartUp) || (Phase == MovePhase.Active && CurrentMove.canMoveDuringActive)) allowInputDrivenXVel = true; }
        if (allowInputDrivenXVel) { Velocity = new Vector3(xD, Velocity.y, 0); } else if (State == CharacterState.JumpStartup) { Velocity = new Vector3(0, Velocity.y, 0); } if (_landLock > 0) _landLock--;
    }

    public void TakeDamage(int dA) { if (Health <= 0 && State != CharacterState.MercyReceiving && State != CharacterState.FinishHimVictim) { if (State == CharacterState.MercyReceiving) { Debug.Log((InputBuf?.profile?.name ?? "P") + " Mercy interrupted!"); State = CharacterState.Knockdown; mercyWindowTimerFrames = 0; SetMercyEligibility(false); knockTimer = 30; } return; } Health -= dA; SetMercyEligibility(true); string pN = (InputBuf?.profile?.name ?? "P"); Debug.Log($"{pN} took {dA} debug. H: {Health}"); if (Health <= 0) { Health = 0; State = CharacterState.Knockdown; knockTimer = MERCY_WINDOW_FRAMES + 120; Debug.Log($"{pN} KO'd by debug."); } else { if (State != CharacterState.HitStun && State != CharacterState.Knockdown && State != CharacterState.MercyReceiving) { State = CharacterState.HitStun; hitStun = 20; IsMoveDataValid = false; } } }
    public void EnterMercyWindow() { if (IsMercyEligibleThisRound && Health <= 0) { State = CharacterState.MercyReceiving; mercyWindowTimerFrames = MERCY_WINDOW_FRAMES; blockTapCountForMercy = 0; Velocity = Vector3.zero; Debug.Log((InputBuf?.profile?.name ?? "P") + " entered Mercy. Tap block " + MERCY_BLOCK_TAPS_REQUIRED + "x"); } }
    public bool RegisterBlockTapForMercy() { if (State == CharacterState.MercyReceiving && mercyWindowTimerFrames > 0 && Health <= 0) { blockTapCountForMercy++; Debug.Log((InputBuf?.profile?.name ?? "P") + " mercy taps: " + blockTapCountForMercy); if (blockTapCountForMercy >= MERCY_BLOCK_TAPS_REQUIRED) { GrantMercy(); return true; } } return false; }
    private void GrantMercy() { Health = Mathf.RoundToInt(MAX_HEALTH * MERCY_HEALTH_RESTORE_PERCENT); State = CharacterState.Idle; Velocity = Vector3.zero; mercyWindowTimerFrames = 0; SetMercyEligibility(false); isKOfriendly = false; Debug.Log((InputBuf?.profile?.name ?? "P") + " MERCY! H: " + Health); }
    public void MarkMercyAsPerformedByThisPlayer() { CanPerformMercyThisMatch = false; }
    public void ReviveForMercy() { Health = Mathf.RoundToInt(MAX_HEALTH * MERCY_HEALTH_RESTORE_PERCENT); State = CharacterState.Idle; Velocity = Vector3.zero; Debug.Log((characterInformation?.characterName ?? "Player") + " REVIVED BY MERCY! Health: " + Health); SetMercyEligibility(false); }

    void LaunchJump() { if (movementStats == null) return; var i = InputBuf.State; int dS = _facingRight ? 1 : -1; float jx = 0; if (i.Forward) jx = dS * movementStats.jumpForwardHorizontalVelocity; else if (i.Back) jx = -dS * movementStats.jumpBackHorizontalVelocity; Velocity = new Vector3(jx, movementStats.jumpUpVelocity, 0); State = CharacterState.Jumping; IsMoveDataValid = false; }
    void HandleAttacks() { if (!(State == CharacterState.Idle || State == CharacterState.Walking || State == CharacterState.Running || State == CharacterState.Crouch)) { if (State == CharacterState.FinishHimWinner && (InputBuf.State.PressedHighPunch || InputBuf.State.PressedLowPunch || InputBuf.State.PressedHighKick || InputBuf.State.PressedLowKick)) { StartAttack("DefaultHitFinisher"); return; } return; } var i = InputBuf.State; if (i.PressedHighPunch) StartAttack("HighPunch"); else if (i.PressedHighKick) StartAttack("HighKick"); else if (i.PressedLowPunch) StartAttack("LowPunch"); else if (i.PressedLowKick) StartAttack("LowKick"); else if (InputBuf.DoubleTappedBack()) StartBackDash(); }
    
    void StartAttack(string tag)
    {
        MoveFrameData moveDataToUse; // Use a clearly new local variable
        bool isValidMove = false;

        if (specialMovesList != null && specialMovesList.TryGetMoveData(tag, out MoveFrameData foundMoveData))
        {
            moveDataToUse = foundMoveData;
            isValidMove = true;
        }
        else if (tag == "DefaultHitFinisher" && State == CharacterState.FinishHimWinner)
        {
            moveDataToUse = new MoveFrameData
            {
                tag = "DefaultHitFinisher", damage = 10, knockDown = true,
                startUp = 5, active = 5, recovery = 10,
                unblockable = true, noChip = true,
                canMoveDuringStartUp = false, canMoveDuringActive = false
                // power = AttackPower.Light (ensure AttackPower is defined if MoveFrameData uses it)
            };
            isValidMove = true;
        }
        else
        {
            IsMoveDataValid = false;
            string charName = (characterInformation != null) ? characterInformation.characterName : (InputBuf?.profile?.name ?? "UnknownPlayer");
            Debug.LogWarning($"FighterCharacterCore ({charName}): Move '{tag}' not found in SpecialMovesSO and is not DefaultHitFinisher.");
            return; 
        }

        CurrentMove = moveDataToUse;
        IsMoveDataValid = true;
        State = CharacterState.Attacking;
        Phase = MovePhase.Startup;
        _phaseFrames = CurrentMove.startUp; 
        Velocity = Vector3.zero;
    }

    void StartBackDash() { if (!(specialMovesList?.TryGetMoveData("BackDash", out var mv) ?? false)) { IsMoveDataValid = false; return; } CurrentMove = mv; IsMoveDataValid = true; State = CharacterState.BackDash; Phase = MovePhase.Startup; _phaseFrames = mv.startUp; Velocity = new Vector3(_facingRight ? -5f : 5f, 0, 0); } // Assuming MoveFrameData may not have travelSpeed for backdash
    void AdvanceMovePhases() { if (!IsMoveDataValid || !(State == CharacterState.Attacking || State == CharacterState.BackDash)) return; _phaseFrames--; if (_phaseFrames > 0) return; switch (Phase) { case MovePhase.Startup: Phase = MovePhase.Active; _phaseFrames = CurrentMove.active; break; case MovePhase.Active: Phase = MovePhase.Recovery; _phaseFrames = CurrentMove.recovery; if (State == CharacterState.BackDash) Velocity = Vector3.zero; break; case MovePhase.Recovery: State = CharacterState.Idle; Phase = MovePhase.Startup; IsMoveDataValid = false; Velocity = Vector3.zero; break; } }
    public void ReceiveHit(MoveFrameData mv, bool blk, FighterCharacterCore attkr) { if (Health <= 0 && State != CharacterState.MercyReceiving && State != CharacterState.FinishHimVictim) return; bool unblk = mv.unblockable; if (State == CharacterState.FinishHimVictim) { ResolveHit(mv, attkr); return; } if (blk && !unblk) ResolveBlock(mv); else ResolveHit(mv, attkr); }
    void ResolveBlock(MoveFrameData mv) { if (!mv.noChip) Health -= mv.damage / 4; block_cnt = block_len_tbl[(int)mv.power]; float pD = push_dist_tbl[(int)mv.power]; Velocity = new Vector3(_facingRight ? -pD * 5 : pD * 5, 0, 0); State = (State == CharacterState.Crouch || State == CharacterState.BlockingLow) ? CharacterState.BlockingLow : CharacterState.BlockingHigh; IsMoveDataValid = false; }
    void ResolveHit(MoveFrameData mv, FighterCharacterCore attkr) { if (Health <= 0 && State != CharacterState.MercyReceiving && State != CharacterState.FinishHimVictim) return; Health -= mv.damage; IsMoveDataValid = false; SetMercyEligibility(true); if (reactionTable.TryGet(mv.reaction, out var r)) { hitStun = r.hitStun; } else { hitStun = 10; } if (Health <= 0) { Health = 0; State = CharacterState.Knockdown; knockTimer = MERCY_WINDOW_FRAMES + 120; Debug.Log((characterInformation?.characterName ?? "P") + " KO'd by " + (attkr?.CharInfo?.characterName ?? "Opp")); if (attkr != null) attkr.SetKOAsFriendly(false); } else if (mv.knockDown) { State = CharacterState.Knockdown; knockTimer = 30; } else { State = CharacterState.HitStun; } }
    public void SetState(CharacterState s) { State = s; if (s != CharacterState.Attacking && s != CharacterState.BackDash && s != CharacterState.FinishHimWinner && s != CharacterState.FinishHimVictim && s != CharacterState.MercyReceiving) { IsMoveDataValid = false; } if (s == CharacterState.FinishHimVictim || s == CharacterState.MercyReceiving || (Health <= 0 && s == CharacterState.Knockdown)) Velocity = Vector3.zero; }
}
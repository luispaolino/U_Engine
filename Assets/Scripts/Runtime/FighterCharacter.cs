using UnityEngine;
using UMK3;

[RequireComponent(typeof(InputBuffer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class FighterCharacter : MonoBehaviour
{
    [Header("Data Tables")]
    public MoveTableSO     moveTable;
    public ReactionTableSO reactionTable;
    public ThrowTableSO    throwTable;
    public CombatAudioBank combatBank;    // hit/block S-FX bank

    [Header("Controls")]
    public PlayerControlsProfile controlsProfile;

    [Header("Graphics")]
    public Transform graphics;           // model pivot for facing flip

    [Header("Startup")]
    public bool startFacingRight = true;
    public Gender gender;

    [HideInInspector] public FighterCharacterCore core;

    InputBuffer inputBuffer;
    Rigidbody2D rb;
    AudioSource aSrc;

    void Awake()
    {
        inputBuffer = GetComponent<InputBuffer>();
        rb          = GetComponent<Rigidbody2D>();
        aSrc        = GetComponent<AudioSource>();

        if (controlsProfile == null)
            Debug.LogError($"{name}: controlsProfile not set", this);
        else
            inputBuffer.profile = controlsProfile;

        core = new FighterCharacterCore(moveTable, reactionTable, throwTable)
        {
            InputBuf = inputBuffer
        };

        Vector2 pos = transform.position;
        core.SpawnAt(pos, startFacingRight);
        rb.position = pos;
        rb.linearVelocity = Vector2.zero;

        if (graphics)
            graphics.localRotation = Quaternion.Euler(0f, startFacingRight ? 0f : 180f, 0f);
    }

    void Update()
    {
        // only capture input when not paused
        if (!core.IsPaused)
            inputBuffer.Capture(core.FacingRight);
    }

    void FixedUpdate()
    {
        // skip physics and movement when paused
        if (core.IsPaused)
            return;

        core.FixedTick();
        Vector2 vel2D = new Vector2(core.Velocity.x, core.Velocity.y);
        rb.MovePosition(rb.position + vel2D * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        var attacker = col.GetComponentInParent<FighterCharacter>();
        if (attacker && attacker != this)
            ReceiveHit(attacker);
    }

    public void ReceiveHit(FighterCharacter attacker)
    {
        var mv = attacker.core.CurrentMove;
        bool blocked = (core.State == CharacterState.Crouch  && !mv.unblockable)
                    || (core.State == CharacterState.Attacking && !mv.unblockable);

        core.ReceiveHit(mv, blocked, attacker.core);

        if (combatBank != null)
        {
            AudioClip clip = blocked ? combatBank.blockClip : combatBank.hitClip;
            if (clip) aSrc.PlayOneShot(clip);
        }
    }

    public void ForcePaused(bool p)  => core.IsPaused = p;
    public void ForceState(CharacterState s) => core.SetState(s);

    public void RoundReset(Vector2 pos, bool faceRight)
    {
        transform.position = pos;
        rb.position        = pos;
        rb.linearVelocity        = Vector2.zero;

        core.FullReset();
        core.SpawnAt(pos, faceRight);
        core.InputBuf = inputBuffer;

        if (graphics)
            graphics.localRotation = Quaternion.Euler(0f, faceRight ? 0f : 180f, 0f);
    }

    public int Health => core.Health;
}

using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(FighterCharacter))]
public class FighterPresenter : MonoBehaviour
{
    [Header("Hurtbox & Effects")]
    public CapsuleCollider2D hurtbox;
    public GameObject        projectileShield;

    Animator         anim;
    FighterCharacter fc;

    CharacterState lastState;
    MovePhase      lastPhase;

    void Awake()
    {
        anim = GetComponent<Animator>();
        fc   = GetComponent<FighterCharacter>();

        if (hurtbox == null)
            Debug.LogWarning("Hurtbox not assigned", this);
        if (projectileShield == null)
            Debug.LogWarning("ProjectileShield not assigned", this);
    }

    void Start()
    {
        lastState = fc.core.State;
        lastPhase = fc.core.Phase;
    }

    void LateUpdate()
    {

        if (fc == null || fc.core == null || anim == null)
        return;
        
        if (anim == null || fc == null || fc.core == null)
            return;

        var core = fc.core;
        var inp  = core.InputBuf.State;

        // --- 1) Log core state & phase ---
        Debug.Log($"[Presenter] Core State={core.State}, Phase={core.Phase}");

        // --- 2) Continuous parameters ---
        float moveX = core.Velocity.x;
        anim.SetFloat("MoveX", moveX);

        bool walkingBack = core.IsGrounded
                        && moveX != 0
                        && ((moveX < 0 && core.FacingRight)
                         || (moveX > 0 && !core.FacingRight));
        anim.SetBool("IsWalkingBack", walkingBack);

        bool isCrouch = core.State == CharacterState.Crouch;
        anim.SetBool("IsCrouching", isCrouch);

        bool blocking = inp.Block && core.IsGrounded && core.State == CharacterState.Idle;
        bool blockHigh = blocking && inp.Up;
        bool blockLow  = blocking && inp.Down;
        anim.SetBool("IsBlockingHigh", blockHigh);
        anim.SetBool("IsBlockingLow",  blockLow);

        Debug.Log($"[Presenter] MoveX={moveX:+0.00;-0.00}, Back={walkingBack}, Crouch={isCrouch}, BHigh={blockHigh}, BLow={blockLow}");

        // invuln visuals
        bool invuln = core.State == CharacterState.BackDash
                    && core.Phase == MovePhase.Active;
        if (hurtbox)          hurtbox.enabled = !invuln;
        if (projectileShield) projectileShield.SetActive(invuln);

        // --- 3) One‐shot triggers ---

        // Attack
        if (core.State == CharacterState.Attacking
         && core.Phase == MovePhase.Startup
         && lastPhase != MovePhase.Startup)
        {
            string tag = core.CurrentMove.tag;
            if (!string.IsNullOrEmpty(tag))
            {
                string trig = "Attack_" + tag;
                Debug.Log($"[Presenter] Firing trigger: {trig}");
                anim.SetTrigger(trig);
            }
            else
            {
                Debug.LogWarning("[Presenter] CurrentMove.tag is empty!");
            }
        }

        // Throw
        if (core.State == CharacterState.ThrowStartup
         && lastState != CharacterState.ThrowStartup)
        {
            Debug.Log("[Presenter] Firing trigger: Throw_Start");
            anim.SetTrigger("Throw_Start");
        }

        // Jump
        if (core.State == CharacterState.Jumping
         && core.Phase == MovePhase.Startup
         && lastPhase != MovePhase.Startup)
        {
            Debug.Log("[Presenter] Firing trigger: JumpStart");
            anim.SetTrigger("JumpStart");
        }

        // Knockdown / GetUp
        if (core.State == CharacterState.Knockdown
         && lastState != CharacterState.Knockdown)
        {
            Debug.Log("[Presenter] Firing trigger: Knockdown");
            anim.SetTrigger("Knockdown");
        }
        if (core.State == CharacterState.GetUp
         && lastState != CharacterState.GetUp)
        {
            Debug.Log("[Presenter] Firing trigger: GetUp");
            anim.SetTrigger("GetUp");
        }

        // --- 4) Save for next frame ---
        lastState = core.State;
        lastPhase = core.Phase;
    }
}

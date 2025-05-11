using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FighterPresenter : MonoBehaviour
{
    public BoxCollider2D hurtbox;
    public GameObject    projectileShield;

    FighterCharacter fc;
    Animator         anim;
    InputBuffer      ib;

    void Awake()
    {
        fc   = GetComponent<FighterCharacter>();
        anim = GetComponent<Animator>();
        ib   = GetComponent<InputBuffer>();
    }

    void LateUpdate()
    {
        var core = fc.core;
        var i    = ib.State;

        // facing + local speed
        anim.SetBool ("FacingRight", core.FacingRight);
        float localX = core.FacingRight ? core.Velocity.x : -core.Velocity.x;
        anim.SetFloat("MoveX", localX);

        // state booleans
        anim.SetBool("IsJumping",  core.State == CharacterState.Jumping);
        anim.SetBool("IsRunning",  core.State == CharacterState.Running);
        bool isGuard = core.State == CharacterState.BlockingHigh || core.State == CharacterState.BlockingLow;

        // ── animation booleans -------------------------------------------------
        bool isCrouching =  core.State == CharacterState.Crouch        ||
                            core.State == CharacterState.BlockingLow;

        bool isBlocking  =  core.State == CharacterState.BlockingHigh ||
                            core.State == CharacterState.BlockingLow;

        anim.SetBool("IsCrouching", isCrouching);     // drives Crouch_Idle & BlockLow
        anim.SetBool("IsBlocking",  isBlocking);      // drives both guard poses
        anim.SetBool("IsRunning",   core.State == CharacterState.Running);

        // walk-back flag remains unchanged
        bool walkingBack = core.State == CharacterState.Walking && i.Back;
        anim.SetBool("IsWalkingBack", walkingBack);

        // invuln during back-dash active
        bool invuln = core.State == CharacterState.BackDash && core.Phase == MovePhase.Active;
        if (hurtbox)          hurtbox.enabled = !invuln;
        if (projectileShield) projectileShield.SetActive(invuln);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UMK3;   // FighterCharacter, GameEvent, etc.

public class RoundSystem : MonoBehaviour
{
    /* ───────────────────────────── Inspector ─────────────────────────── */
    [Header("Fighters")]
    public FighterCharacter fighterA;
    public FighterCharacter fighterB;

    [Header("UI")]
    public Text centerMessage;
    public Text timerText;
    public Text scoreA;
    public Text scoreB;

    [Header("Round & Timer")]
    public int   timeLimit   = 90;
    public int   roundsToWin = 2;
    public float introFreeze = 1.2f;   // “ROUND x” hold
    public float fightFlash  = 0.8f;   // “FIGHT!” flash
    public float koFreeze    = 0.4f;   // freeze pose after KO / time-up

    [Header("Fade")]
    public float fadeDuration         = 0.5f;   // between rounds
    public float gameOverFadeDuration = 0.8f;

    /* ───────────────────────────── Runtime ───────────────────────────── */
    int  roundTimer;
    int  winsA, winsB;
    bool lowHealthPlayed;

    readonly Vector2 spawnA = new Vector2(-2f, 0f);
    readonly Vector2 spawnB = new Vector2( 2f, 0f);

    /* ───────────────────────────── Unity Lifetime ───────────────────── */
    void Start()
    {
        if (FadeController.I)
            FadeController.I.fadeMaterial.SetFloat("Intensity", 1f);

        fighterA.ForcePaused(true);
        fighterB.ForcePaused(true);

        StartCoroutine(MatchLoop());
    }

    void FixedUpdate()
    {
        FaceOpponent(fighterA, fighterB);
        FaceOpponent(fighterB, fighterA);
    }

    /* ───────────────────────────── Facing Helper ────────────────────── */
    void FaceOpponent(FighterCharacter me, FighterCharacter foe)
    {
        bool faceRight = foe.transform.position.x > me.transform.position.x;
        me.core.SetFacing(faceRight);

        if (me.graphics)
            me.graphics.localRotation =
                Quaternion.Euler(0f, faceRight ? 0f : 180f, 0f);
        else if (me.TryGetComponent(out SpriteRenderer sr))
            sr.flipX = !faceRight;

        if (me.GetComponentInChildren<Animator>() is Animator anim)
            anim.SetBool("Mirror", !faceRight);
    }

    /* ───────────────────────────── Match Loop ───────────────────────── */
    IEnumerator MatchLoop()
    {
        winsA = winsB = 0;
        UpdateScoreUI();

        while (true)
        {
            SnapRoundStart();                                   // teleport while black
            yield return FadeController.I.FadeIn(fadeDuration); // cores still paused

            for (int rd = 1; rd <= roundsToWin; rd++)
            {
                yield return RoundLoop(rd);

                if (winsA == roundsToWin || winsB == roundsToWin)
                    break;

                yield return FadeController.I.FadeOut(fadeDuration);
                SnapRoundStart();                               // move again while black
                yield return FadeController.I.FadeIn(fadeDuration);
                // cores remain paused until next round’s “FIGHT!” flash
            }

            yield return GameOver();
        }
    }

    /* ───────────────────────────── Single Round ─────────────────────── */
    IEnumerator RoundLoop(int roundNum)
    {
        /* ROUND banner (fighters still paused) */

        SetAnimatorSpeed(fighterA, 1f);
        SetAnimatorSpeed(fighterB, 1f);

        centerMessage.text = $"ROUND {roundNum}";
        AudioRoundManager.Play(roundNum == 1 ? GameEvent.RoundOne
                                             : GameEvent.RoundTwo);
        yield return new WaitForSeconds(introFreeze);
        centerMessage.text = string.Empty;

        /* FIGHT banner */
        centerMessage.text = "FIGHT!";
        AudioRoundManager.Play(GameEvent.Fight);
        yield return new WaitForSeconds(fightFlash);
        centerMessage.text = string.Empty;

        /* NOW enable controls / physics */
        BeginCombat();

        /* timer loop */
        while (true)
        {
            timerText.text = roundTimer.ToString("00");

            if (roundTimer <= 10 && roundTimer > 0)
                AudioRoundManager.Play(GameEvent.Last10SecTick);

            if (!lowHealthPlayed &&
               (fighterA.Health <= 10 || fighterB.Health <= 10))
            {
                AudioRoundManager.Play(GameEvent.LastHitWarning);
                lowHealthPlayed = true;
            }

            if (fighterA.Health <= 0 || fighterB.Health <= 0 || roundTimer <= 0)
                break;

            yield return new WaitForSeconds(1f);
            roundTimer--;
        }

        EndCombatFreeze();
        yield return new WaitForSeconds(koFreeze);

        DeclareWinner();
        yield return new WaitForSeconds(2f);
    }

    /* ───────────────────────────── Game Over ────────────────────────── */
    IEnumerator GameOver()
    {
        centerMessage.text = "GAME OVER";
        AudioRoundManager.Play(GameEvent.Flawless);
        yield return new WaitForSeconds(3f);

        yield return FadeController.I.FadeOut(gameOverFadeDuration);
        centerMessage.text = string.Empty;

        winsA = winsB = 0;
        UpdateScoreUI();
    }

    /* ───────────────────────────── Round Helpers ───────────────────── */

    /* Teleport fighters, clear input, keep everything paused */
void SnapRoundStart()
{
    fighterA.RoundReset(spawnA, true);
    fighterB.RoundReset(spawnB, false);

    fighterA.transform.position = spawnA;
    fighterB.transform.position = spawnB;

    ResetPhysicsVelocity();

    /* …and immediately capture the current hardware state
       so the first un-paused frame sees no “Pressed” edges */
    fighterA.GetComponent<InputBuffer>().Capture(true);   // facingRight doesn’t matter here
    fighterB.GetComponent<InputBuffer>().Capture(false);

    fighterA.ForcePaused(true);
    fighterB.ForcePaused(true);

    RewindAnimator(fighterA);
    RewindAnimator(fighterB);
}


    /* Unpause cores, physics, anim AFTER “FIGHT!” flash */
    void BeginCombat()
    {
        fighterA.ForcePaused(false);
        fighterB.ForcePaused(false);

        UnfreezePhysics();

        fighterA.transform.position = spawnA;
        fighterB.transform.position = spawnB;

        if (fighterA.TryGetComponent<Rigidbody2D>(out var rbA)) rbA.position = spawnA;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbB)) rbB.position = spawnB;

        roundTimer      = timeLimit;
        lowHealthPlayed = false;
    }

    void EndCombatFreeze()
    {
        fighterA.ForcePaused(true);
        fighterB.ForcePaused(true);

        FreezePhysics();
        SetAnimatorSpeed(fighterA, 0f);
        SetAnimatorSpeed(fighterB, 0f);

        timerText.text = "00";
    }

    void DeclareWinner()
    {
        if (fighterA.Health > fighterB.Health)
        {
            winsA++;
            centerMessage.text = "PLAYER 1 WINS";
            AudioRoundManager.Play(GameEvent.Wins);
        }
        else if (fighterB.Health > fighterA.Health)
        {
            winsB++;
            centerMessage.text = "PLAYER 2 WINS";
            AudioRoundManager.Play(GameEvent.Wins);
        }
        else
        {
            centerMessage.text = "DRAW!";
        }
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        scoreA.text = new string('■', winsA);
        scoreB.text = new string('■', winsB);
    }

    /* ────────── Physics helpers ───────── */
    void FreezePhysics()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rbA)) rbA.simulated = false;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbB)) rbB.simulated = false;
    }
    void UnfreezePhysics()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rbA)) rbA.simulated = true;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbB)) rbB.simulated = true;
    }
    void ResetPhysicsVelocity()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rbA)) rbA.linearVelocity = Vector2.zero;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbB)) rbB.linearVelocity = Vector2.zero;
    }

    /* ────────── Animator helpers ───────── */
    void SetAnimatorSpeed(FighterCharacter fc, float speed)
    {
    if (fc.GetComponentInChildren<Animator>() is Animator anim)
        anim.speed = speed;
    }
    void RewindAnimator(FighterCharacter fc)
    {
        if (fc.GetComponentInChildren<Animator>() is Animator anim)
        {
            const string DEFAULT_STATE = "Locomotion";      // name of your idle/walk blend-tree
            int layer = 0;
            int hash  = Animator.StringToHash(DEFAULT_STATE);

            /* if the state exists, jump to it; otherwise fall back to current state */
            if (anim.HasState(layer, hash))
                anim.Play(hash, layer, 0f);
            else
                anim.Play(anim.GetCurrentAnimatorStateInfo(layer).fullPathHash, layer, 0f);

            anim.Update(0f);                                // apply pose immediately while paused
        }
    }
}

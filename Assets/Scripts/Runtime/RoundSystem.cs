using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UMK3; // for FighterCharacter, GameEvent, etc.

/// <summary>
/// Controls round flow: intros, fades, timer, announcements, win logic.
/// Uses URP fade pass and freezes fighters/physics throughout.
/// Implements two-round match with draws counting as rounds.
/// Fade durations are configurable via inspector.
/// </summary>
public class RoundSystem : MonoBehaviour
{
    [Header("Fighters")]
    public FighterCharacter fighterA;
    public FighterCharacter fighterB;

    [Header("UI")]
    public Text centerMessage;
    public Text timerText;
    public Text scoreA;
    public Text scoreB;

    [Header("Settings")]
    public int timeLimit = 90;
    public int roundsToWin = 2;
    public float introFreeze = 1.2f;
    public float fightFlash = 0.8f;
    public float koFreeze = 0.4f;

    [Header("Fade Settings")]
    public float fadeDuration = 0.5f;            // fade in/out between resets
    public float gameOverFadeDuration = 0.8f;

    private int roundTimer;
    private int winsA, winsB;
    private bool lowHealthPlayed;

    void Start()
    {
        // start with screen black
        if (FadeController.I != null)
            FadeController.I.fadeMaterial.SetFloat("Intensity", 1f);
        // ensure fighters frozen
        fighterA.ForcePaused(true);
        fighterB.ForcePaused(true);
        // begin first round
        StartCoroutine(MatchLoop());
    }

    void FixedUpdate()
    {
        FaceOpponent(fighterA, fighterB);
        FaceOpponent(fighterB, fighterA);
    }

    void FaceOpponent(FighterCharacter me, FighterCharacter foe)
    {
        bool faceRight = foe.transform.position.x > me.transform.position.x;
        me.core.SetFacing(faceRight);
        if (me.graphics != null)
            me.graphics.localRotation = Quaternion.Euler(0, faceRight ? 0 : 180, 0);
        else if (me.GetComponent<SpriteRenderer>() is var sr && sr)
            sr.flipX = !faceRight;
    }

    IEnumerator MatchLoop()
    {
        winsA = winsB = 0;
        UpdateScoreUI();

        while (true)
        {
            // setup and fade in for new match start or after game over
            ResetRoundState();
            yield return FadeController.I.FadeIn(fadeDuration);

            // play up to roundsToWin rounds
            for (int rd = 1; rd <= roundsToWin; rd++)
        {
            // run the round
            yield return RoundLoop(rd);

            // if someone won, proceed to Game Over
            if (winsA == roundsToWin || winsB == roundsToWin)
                break;

            // only fade between rounds, not after the last one
            if (rd < roundsToWin)
            {
                yield return FadeController.I.FadeOut(fadeDuration);
                yield return FadeController.I.FadeIn(fadeDuration);
            }
        }

            // game over after two rounds or win
            yield return GameOver();
        }
    }

    IEnumerator RoundLoop(int roundNum)
    {
        // announce round
        centerMessage.text = $"ROUND {roundNum}";
        AudioRoundManager.Play(
            roundNum == 1 ? GameEvent.RoundOne : GameEvent.RoundTwo);
        yield return new WaitForSeconds(introFreeze);
        centerMessage.text = string.Empty;

        // announce fight
        centerMessage.text = "FIGHT!";
        AudioRoundManager.Play(GameEvent.Fight);
        yield return new WaitForSeconds(fightFlash);
        centerMessage.text = string.Empty;

        // start combat
        fighterA.ForcePaused(false);
        fighterB.ForcePaused(false);
        UnfreezePhysics();
        ResetAnimations();
        roundTimer = timeLimit;
        lowHealthPlayed = false;

        // timer and battle loop
        while (true)
        {
            timerText.text = roundTimer.ToString("00");
            if (roundTimer <= 10 && roundTimer > 0)
                AudioRoundManager.Play(GameEvent.Last10SecTick);
            if (!lowHealthPlayed && (fighterA.Health <= 10 || fighterB.Health <= 10))
            {
                AudioRoundManager.Play(GameEvent.LastHitWarning);
                lowHealthPlayed = true;
            }
            if (fighterA.Health <= 0 || fighterB.Health <= 0 || roundTimer <= 0)
                break;
            yield return new WaitForSeconds(1f);
            roundTimer--;
        }

        // freeze at round end
        fighterA.ForcePaused(true);
        fighterB.ForcePaused(true);
        FreezeAnimations();
        FreezePhysics();
        timerText.text = "00";
        yield return new WaitForSeconds(koFreeze);

        // announce result
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
        yield return new WaitForSeconds(2f);
    }

    IEnumerator GameOver()
    {
        // display game over
        centerMessage.text = "GAME OVER";
        AudioRoundManager.Play(GameEvent.Flawless);
        yield return new WaitForSeconds(3f);

        // fade out
        yield return FadeController.I.FadeOut(gameOverFadeDuration);
        centerMessage.text = string.Empty;

        // reset match and loop back
        winsA = winsB = 0;
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        scoreA.text = new string('■', winsA);
        scoreB.text = new string('■', winsB);
    }

    void ResetRoundState()
    {
        fighterA.RoundReset(new Vector2(-2f, 0f), true);
        fighterB.RoundReset(new Vector2(2f, 0f), false);
        UnfreezePhysics();
        ResetPhysicsVelocity();
        ResetAnimations();
    }

    void FreezePhysics()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rba)) rba.simulated = false;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbb)) rbb.simulated = false;
    }

    void UnfreezePhysics()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rba)) rba.simulated = true;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbb)) rbb.simulated = true;
    }

    void ResetPhysicsVelocity()
    {
        if (fighterA.TryGetComponent<Rigidbody2D>(out var rba)) rba.linearVelocity = Vector2.zero;
        if (fighterB.TryGetComponent<Rigidbody2D>(out var rbb)) rbb.linearVelocity = Vector2.zero;
    }

    void FreezeAnimations()
    {
        SetAnimatorSpeed(fighterA, 0f);
        SetAnimatorSpeed(fighterB, 0f);
    }

    void ResetAnimations()
    {
        SetAnimatorSpeed(fighterA, 1f);
        SetAnimatorSpeed(fighterB, 1f);
    }

    void SetAnimatorSpeed(FighterCharacter fc, float speed)
    {
        if (fc.GetComponentInChildren<Animator>() is Animator anim)
            anim.speed = speed;
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UMK3; // Your project's namespace

public class RoundSystem : MonoBehaviour
{
    /* ───────────────────────────── Inspector Fields ─────────────────────────── */
    [Header("Fighters")]
    public FighterCharacter fighterA;
    public FighterCharacter fighterB;

    [Header("UI")]
    public Text centerMessage;
    public Text timerText;
    public Text meterTextA; // UI Text for Player A's Meter Percentage
    public Text meterTextB; // UI Text for Player B's Meter Percentage
    // Add separate Text fields for win counts if needed:
    // public Text winCountTextA;
    // public Text winCountTextB;

    [Header("Round & Timer")]
    public int timeLimit = 90;
    public int roundsToWin = 2;
    public float introFreeze = 1.2f;
    public float fightFlash = 0.8f;
    public float koFreeze = 0.4f;

    [Header("Distance Clamping")]
    public float maxCharacterXDistance = 5.82f;

    [Header("Fade")]
    public float fadeDuration = 0.5f;
    public float gameOverFadeDuration = 0.8f;

    /* ───────────────────────────── Runtime Variables ───────────────────────────── */
    private int roundTimer;
    private int winsA, winsB;
    private bool roundActive;
    private bool lowHealthPlayed;

    private readonly Vector2 spawnA = new Vector2(-2f, 0f);
    private readonly Vector2 spawnB = new Vector2(2f, 0f);

    private Rigidbody2D rbA;
    private Rigidbody2D rbB;

    private Vector2 prevP1Pos;
    private Vector2 prevP2Pos;
    private bool firstFrameAfterUnpause = true;

    /* ───────────────────────────── Unity Lifecycle Methods ───────────────────── */
    void Start()
    {
        if (fighterA == null || fighterB == null)
        {
            Debug.LogError("RoundSystem: FighterA or FighterB not assigned. Disabling script.", this);
            enabled = false;
            return;
        }

        rbA = fighterA.GetComponent<Rigidbody2D>();
        rbB = fighterB.GetComponent<Rigidbody2D>();

        if (rbA == null || rbB == null)
        {
            Debug.LogError("RoundSystem: One or both fighters are missing Rigidbody2D components. Clamping requires them. Disabling script.", this);
            enabled = false;
            return;
        }

        if (FadeController.I != null)
        {
            FadeController.I.fadeMaterial.SetFloat("_Intensity", 1f);
        }

        fighterA.ForcePaused(true);
        fighterB.ForcePaused(true);
        roundActive = false;
        firstFrameAfterUnpause = true;

        if (rbA != null) prevP1Pos = rbA.position; // Initialize based on actual start
        if (rbB != null) prevP2Pos = rbB.position;

        UpdateWinCountUI();
        ClearMeterUI();

        StartCoroutine(MatchLoop());
    }

    void Update()
    {
        if (roundActive)
        {
            UpdateRunMeterUI();

            if (timerText != null)
            {
                timerText.text = roundTimer.ToString("00");
            }
        }
        else
        {
            ClearMeterUI();
        }
    }

    void FixedUpdate()
    {
        if (!roundActive || fighterA == null || fighterB == null || fighterA.core == null || fighterB.core == null || rbA == null || rbB == null)
        {
            return;
        }

        if (firstFrameAfterUnpause)
        {
            if (rbA != null) prevP1Pos = rbA.position;
            if (rbB != null) prevP2Pos = rbB.position;
            firstFrameAfterUnpause = false;
        }

        Vector2 p1CurrentFramePos = rbA.position;
        Vector2 p2CurrentFramePos = rbB.position;

        // Step 1: Determine facing direction based on current positions
        FaceOpponent(fighterA, fighterB); // This now only calls fighterA.core.SetFacing()
        FaceOpponent(fighterB, fighterA); // This now only calls fighterB.core.SetFacing()
                                          // FighterCharacter.FixedUpdate will handle its own graphics/animator updates

        // Step 2: Apply distance clamping
        EnforceMaxCharacterDistance(p1CurrentFramePos, p2CurrentFramePos);

        // Step 3: Update previous positions for the next frame
        // (using the Rigidbody positions which now reflect any clamping)
        if (rbA != null) prevP1Pos = rbA.position;
        if (rbB != null) prevP2Pos = rbB.position;
    }

    /* ───────────────────────────── Facing Logic ────────────────────── */
    void FaceOpponent(FighterCharacter me, FighterCharacter foe)
    {
        if (me == null || foe == null || me.core == null) return;

        // Get Rigidbody components to determine positions
        // It's assumed these RBs are already cached (rbA, rbB) or can be fetched if needed,
        // but for this method, let's use the direct references if they are passed.
        // However, the most up-to-date positions are from rbA and rbB for player1 and player2.
        Rigidbody2D meActualRb = (me == fighterA) ? rbA : rbB;
        Rigidbody2D foeActualRb = (foe == fighterA) ? rbA : rbB;

        if (meActualRb == null || foeActualRb == null) return;

        bool shouldFaceRight = foeActualRb.position.x > meActualRb.position.x;
        me.core.SetFacing(shouldFaceRight); // ONLY tell the core which way to face.
                                            // FighterCharacter.cs will handle its own visual update.
    }

    /* ───────────────────────────── Max Distance Clamping Logic ("Hard Wall") ──────────────── */
    void EnforceMaxCharacterDistance(Vector2 p1OriginalPos, Vector2 p2OriginalPos)
    {
        float currentXDistance = Mathf.Abs(p1OriginalPos.x - p2OriginalPos.x);
        float tolerance = 0.001f;

        if (currentXDistance > maxCharacterXDistance + tolerance)
        {
            Vector2 finalClampedP1 = p1OriginalPos;
            Vector2 finalClampedP2 = p2OriginalPos;
            bool p1IsLeft = p1OriginalPos.x < p2OriginalPos.x;

            if (p1IsLeft)
            {
                float p1TargetX = p2OriginalPos.x - maxCharacterXDistance;
                if (finalClampedP1.x < p1TargetX) finalClampedP1.x = p1TargetX;
                float p2TargetX = finalClampedP1.x + maxCharacterXDistance; // Use P1's potentially new position
                if (finalClampedP2.x > p2TargetX) finalClampedP2.x = p2TargetX;
            }
            else // P1 is right, P2 is left
            {
                float p1TargetX = p2OriginalPos.x + maxCharacterXDistance;
                if (finalClampedP1.x > p1TargetX) finalClampedP1.x = p1TargetX;
                float p2TargetX = finalClampedP1.x - maxCharacterXDistance; // Use P1's potentially new position
                if (finalClampedP2.x < p2TargetX) finalClampedP2.x = p2TargetX;
            }
            
            float distanceAfterHardClamps = Mathf.Abs(finalClampedP1.x - finalClampedP2.x);
            if (Mathf.Abs(distanceAfterHardClamps - maxCharacterXDistance) > tolerance)
            {
                float remainingOverlapOrGap = distanceAfterHardClamps - maxCharacterXDistance;
                float correction = remainingOverlapOrGap / 2.0f;
                if (finalClampedP1.x < finalClampedP2.x)
                {
                    finalClampedP1.x += correction;
                    finalClampedP2.x -= correction;
                }
                else
                {
                    finalClampedP1.x -= correction;
                    finalClampedP2.x += correction;
                }
            }

            // Apply final calculated positions to Rigidbodies only if they changed
            if (rbA != null && Vector2.Distance(rbA.position, finalClampedP1) > tolerance) rbA.MovePosition(finalClampedP1);
            if (rbB != null && Vector2.Distance(rbB.position, finalClampedP2) > tolerance) rbB.MovePosition(finalClampedP2);

            // Sync the cores with these new, finally clamped positions
            if (fighterA?.core != null) fighterA.core.SyncPosition(finalClampedP1);
            if (fighterB?.core != null) fighterB.core.SyncPosition(finalClampedP2);
        }
    }

    /* ───────────────────────────── Match Flow Coroutines & Helpers ───────────────────── */
    IEnumerator MatchLoop()
    {
        winsA = winsB = 0;
        UpdateWinCountUI();
        ClearMeterUI();

        while (true)
        {
            SnapRoundStartVisuals();
            if (FadeController.I != null) yield return FadeController.I.FadeIn(fadeDuration);

            for (int rd = 1; ; rd++)
            {
                PrepareRound(rd);
                yield return StartCoroutine(RoundLoop(rd));
                UpdateWinCountUI();
                if (winsA >= roundsToWin || winsB >= roundsToWin) break;

                if (FadeController.I != null) yield return FadeController.I.FadeOut(fadeDuration);
                SnapRoundStartVisuals();
                if (FadeController.I != null) yield return FadeController.I.FadeIn(fadeDuration);
            }

            UpdateWinCountUI();
            yield return StartCoroutine(GameOverSequence());
            
            winsA = winsB = 0;
            UpdateWinCountUI();
            ClearMeterUI();
        }
    }

    void SnapRoundStartVisuals()
    {
        if (fighterA != null) fighterA.RoundReset(spawnA, true);
        if (fighterB != null) fighterB.RoundReset(spawnB, false);
        if (rbA != null) rbA.position = spawnA;
        if (rbB != null) rbB.position = spawnB;
        if (rbA != null) prevP1Pos = rbA.position;
        if (rbB != null) prevP2Pos = rbB.position;
        firstFrameAfterUnpause = true;
        RewindAnimator(fighterA);
        RewindAnimator(fighterB);
        ClearMeterUI();
    }

    void PrepareRound(int roundNum)
    {
        if (fighterA != null) fighterA.ForcePaused(true);
        if (fighterB != null) fighterB.ForcePaused(true);
        lowHealthPlayed = false;
        roundTimer = timeLimit;
        if (timerText != null) timerText.text = roundTimer.ToString("00");
        InputBuffer inputA = fighterA?.GetComponent<InputBuffer>();
        InputBuffer inputB = fighterB?.GetComponent<InputBuffer>();
        if (inputA != null) inputA.ClearPrev();
        if (inputB != null) inputB.ClearPrev();
    }

    IEnumerator RoundLoop(int roundNum)
    {
        SetAnimatorSpeed(fighterA, 1f);
        SetAnimatorSpeed(fighterB, 1f);

        if (centerMessage != null) centerMessage.text = $"ROUND {roundNum}";
        AudioRoundManager.Play(roundNum == 1 ? GameEvent.RoundOne : GameEvent.RoundTwo);
        yield return new WaitForSeconds(introFreeze);
        if (centerMessage != null) centerMessage.text = string.Empty;

        if (centerMessage != null) centerMessage.text = "FIGHT!";
        AudioRoundManager.Play(GameEvent.Fight);
        yield return new WaitForSeconds(fightFlash);
        if (centerMessage != null) centerMessage.text = string.Empty;

        BeginCombat();

        float lastTickPlayedTime = Time.time;
        while (roundActive)
        {
            if (roundTimer <= 10 && roundTimer > 0)
            {
                if (Time.time >= lastTickPlayedTime + 0.95f)
                {
                    AudioRoundManager.Play(GameEvent.Last10SecTick);
                    lastTickPlayedTime = Time.time;
                }
            }
            if (!lowHealthPlayed && fighterA?.core != null && fighterB?.core != null)
            {
                if (fighterA.Health <= fighterA.core.Health * 0.1f || fighterB.Health <= fighterB.core.Health * 0.1f)
                {
                    AudioRoundManager.Play(GameEvent.LastHitWarning);
                    lowHealthPlayed = true;
                }
            }
            bool p1Dead = (fighterA != null && fighterA.Health <= 0);
            bool p2Dead = (fighterB != null && fighterB.Health <= 0);
            if (p1Dead || p2Dead || roundTimer <= 0) EndCombat();

            yield return new WaitForSeconds(1.0f);
            if (roundActive) roundTimer--;
        }
        yield return new WaitForSeconds(koFreeze);
        DeclareRoundWinner();
        yield return new WaitForSeconds(2f);
    }
    
    IEnumerator GameOverSequence()
    {
        if (centerMessage != null)
        {
            string matchWinnerText = "MATCH OVER";
            if (winsA >= roundsToWin) matchWinnerText = "PLAYER 1 WINS THE MATCH!";
            else if (winsB >= roundsToWin) matchWinnerText = "PLAYER 2 WINS THE MATCH!";
            centerMessage.text = matchWinnerText;
        }
        AudioRoundManager.Play(GameEvent.Flawless); // Consider a "Match Over" sound
        yield return new WaitForSeconds(3f);
        if (FadeController.I != null) yield return FadeController.I.FadeOut(gameOverFadeDuration);
        if (centerMessage != null) centerMessage.text = string.Empty;
    }

    void BeginCombat()
    {
        if (fighterA != null) fighterA.ForcePaused(false);
        if (fighterB != null) fighterB.ForcePaused(false);
        roundActive = true;
        firstFrameAfterUnpause = true; // Ensure prevPos is re-initialized correctly on first FixedUpdate
        if (rbA != null && fighterA != null) rbA.position = fighterA.transform.position;
        if (rbB != null && fighterB != null) rbB.position = fighterB.transform.position;
        // Sync core positions after setting Rigidbody positions and unpausing
        if (fighterA?.core != null && rbA != null) fighterA.core.SyncPosition(rbA.position);
        if (fighterB?.core != null && rbB != null) fighterB.core.SyncPosition(rbB.position);
        roundTimer = timeLimit;
        lowHealthPlayed = false;
    }

    void EndCombat()
    {
        roundActive = false;
        if (fighterA != null) fighterA.ForcePaused(true);
        if (fighterB != null) fighterB.ForcePaused(true);
        SetAnimatorSpeed(fighterA, 0f);
        SetAnimatorSpeed(fighterB, 0f);
        if (timerText != null && roundTimer <= 0 && fighterA?.Health > 0 && fighterB?.Health > 0)
        {
            timerText.text = "00";
        }
    }

    void DeclareRoundWinner()
    {
        string winnerText = "DRAW ROUND";
        GameEvent winEvent = GameEvent.Wins; // Default win sound
        int healthA = (fighterA?.core != null) ? fighterA.Health : -1;
        int healthB = (fighterB?.core != null) ? fighterB.Health : -1;

        if (healthA > 0 || healthB > 0) // Check if the round ended with at least one player having health (not a double KO from external source)
        {
            if (healthA > healthB) { winsA++; winnerText = "PLAYER 1 WINS"; }
            else if (healthB > healthA) { winsB++; winnerText = "PLAYER 2 WINS"; }
            // If healthA == healthB, it remains "DRAW ROUND"
        }
        
        if (centerMessage != null) centerMessage.text = winnerText;
        AudioRoundManager.Play(winEvent);
        UpdateWinCountUI(); // Update win counts (if using dedicated UI for it)
    }

    // --- UI Update Methods ---
    void UpdateWinCountUI()
    {
        // Example: if you have public Text winCountTextA; public Text winCountTextB;
        // if (winCountTextA != null) winCountTextA.text = "P1: " + winsA;
        // if (winCountTextB != null) winCountTextB.text = "P2: " + winsB;
    }

    void UpdateRunMeterUI()
    {
        // Called from Update() when roundActive is true.
        if (fighterA?.core != null && meterTextA != null)
        {
            if (FighterCharacterCore.METER_CAPACITY_FLOAT > 0) // Use the float constant
            {
                float p1MeterPercentage = fighterA.core.CurrentMeterValue / FighterCharacterCore.METER_CAPACITY_FLOAT;
                meterTextA.text = Mathf.RoundToInt(p1MeterPercentage * 100) + "%";
            }
            else meterTextA.text = "N/A";
        }
        else if (meterTextA != null) meterTextA.text = "---"; // Default if no P1

        if (fighterB?.core != null && meterTextB != null)
        {
            if (FighterCharacterCore.METER_CAPACITY_FLOAT > 0) // Use the float constant
            {
                float p2MeterPercentage = fighterB.core.CurrentMeterValue / FighterCharacterCore.METER_CAPACITY_FLOAT;
                meterTextB.text = Mathf.RoundToInt(p2MeterPercentage * 100) + "%";
            }
            else meterTextB.text = "N/A";
        }
        else if (meterTextB != null) meterTextB.text = "---"; // Default if no P2
    }

    void ClearMeterUI()
    {
        if (meterTextA != null) meterTextA.text = "100%"; // Default display when round not active
        if (meterTextB != null) meterTextB.text = "100%";
    }

    /* ────────── Animator Helpers ───────── */
    void SetAnimatorSpeed(FighterCharacter fc, float speed)
    {
        if (fc == null) return;
        Animator anim = fc.GetComponentInChildren<Animator>() ?? fc.GetComponent<Animator>();
        if (anim != null) anim.speed = speed;
    }

    void RewindAnimator(FighterCharacter fc)
    {
        if (fc == null) return;
        Animator anim = fc.GetComponentInChildren<Animator>() ?? fc.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            const string DEFAULT_STATE_NAME = "Locomotion";
            int layerIndex = 0;
            int defaultStateHash = Animator.StringToHash(DEFAULT_STATE_NAME);
            if (anim.HasState(layerIndex, defaultStateHash)) anim.Play(defaultStateHash, layerIndex, 0f);
            else if (anim.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash != 0)
                anim.Play(anim.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash, layerIndex, 0f);
            anim.Update(0f);
        }
    }
}
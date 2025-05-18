using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using UMK3;

public class RoundSystem : MonoBehaviour
{
    /* ───────────────────────────── Inspector Fields ─────────────────────────── */
    [Header("Player Setup")]
    public GameObject player1Prefab;
    public GameObject player2Prefab;
    public Transform player1SpawnPoint;
    public Transform player2SpawnPoint;
    public PlayerControlsProfile player1Controls;
    public PlayerControlsProfile player2Controls;
    [Tooltip("Additional Y-axis rotation offset for Player 1's GRAPHICS when spawned.")]
    public float player1YRotationOffset = 0f;
    [Tooltip("Additional Y-axis rotation offset for Player 2's GRAPHICS when spawned.")]
    public float player2YRotationOffset = 0f;

    [Header("System References")]
    public CameraController mainGameCamera;

    [Header("UI Elements")]
    public Text roundNumberText;
    public Text roundOutcomeText;
    public Text flawlessVictoryText;
    public Text timerText;

    [Header("Round & Timer Settings")]
    public int timeLimit = 90;
    public int roundsToWin = 2;
    public float introFreeze = 1.2f;
    public float fightFlash = 0.8f;
    public float endOfRoundPause = 0.4f;
    public const float FINISHER_TIMER_DURATION = 7.0f;
    private const float MERCY_OUTCOME_WAIT_SEC = 1.5f;
    private const float STANDARD_ROUND_END_MESSAGE_DURATION = 2.0f;

    [Header("Gameplay Mechanics")]
    public float maxCharacterXDistance = 5.82f;
    
    [Header("Visuals")]
    public float fadeDuration = 0.5f;
    public float gameOverFadeDuration = 0.8f;

    private FighterCharacter fighterA_instance;
    private FighterCharacter fighterB_instance;
    public FighterCharacter Player1 => fighterA_instance;
    public FighterCharacter Player2 => fighterB_instance;

    private int roundTimerSec;
    private int winsA, winsB;
    private bool roundActive;
    private bool lowHealthWarningPlayedP1;
    private bool lowHealthWarningPlayedP2;
    private const float LOW_HEALTH_THRESHOLD_PERCENT = 0.10f;

    private Rigidbody2D rbA;
    private Rigidbody2D rbB;
    private Vector2 prevP1Pos;
    private Vector2 prevP2Pos;
    private bool firstFrameAfterUnpause = true;

    private enum RoundEndType { None, KO, DoubleKO, TimeUpWin, TimeUpDraw }
    private FighterCharacter currentRoundVictor = null;
    private FighterCharacter currentRoundDefeated = null;
    private RoundEndType currentRoundEndCondition = RoundEndType.None;
    private bool matchAnimalityEnabled = false;
    private int currentMatchRoundNumber = 0;

    private enum PostKOSubState { None, AwaitingFinisherInput, FinisherPerformed, MercyGranted, ProceedToRoundEnd }
    private PostKOSubState postKOState = PostKOSubState.None;

    private const int LIFE_BAR_WIDTH_PX = 250;
    private const int LIFE_BAR_HEIGHT_PX = 25;
    private const float LIFE_BAR_Y_POS = 10f;
    private const float LIFE_BAR_P1_X_POS = 10f;

    void Awake()
    {
        if (!ValidatePlayerSetup()) { enabled = false; return; }
        if (mainGameCamera == null) { mainGameCamera = Camera.main?.GetComponent<CameraController>(); if (mainGameCamera == null) Debug.LogWarning("RS: CameraController not assigned/found!", this); }
        SpawnAndSetupPlayers();
        if (fighterA_instance != null) rbA = fighterA_instance.GetComponent<Rigidbody2D>(); else { Debug.LogError("RS: P1 instance missing RB2D after spawn!", this); enabled = false; return; }
        if (fighterB_instance != null) rbB = fighterB_instance.GetComponent<Rigidbody2D>(); else { Debug.LogError("RS: P2 instance missing RB2D after spawn!", this); enabled = false; return; }
        if (mainGameCamera != null) { if (fighterA_instance != null && fighterB_instance != null) { mainGameCamera.InitializePlayerTargets(fighterA_instance.transform, fighterB_instance.transform); } else { Debug.LogError("RS: Could not init camera, players null after spawn.", this); } }
        if (FadeController.I != null) FadeController.I.fadeMaterial.SetFloat("_Intensity", 1f);
        if (fighterA_instance != null) fighterA_instance.MatchReset(); 
        if (fighterB_instance != null) fighterB_instance.MatchReset();
        if (fighterA_instance != null) fighterA_instance.ForcePaused(true); 
        if (fighterB_instance != null) fighterB_instance.ForcePaused(true);
        roundActive = false; firstFrameAfterUnpause = true;
        if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position;
        UpdateWinCountUI(); StartCoroutine(MatchLoop());
    }

    bool ValidatePlayerSetup()
    {
        bool valid = true;
        if (player1Prefab == null) { Debug.LogError("RS: Player 1 Prefab not assigned!", this); valid = false; }
        if (player2Prefab == null) { Debug.LogError("RS: Player 2 Prefab not assigned!", this); valid = false; }
        if (player1SpawnPoint == null) { Debug.LogError("RS: Player 1 Spawn Point not assigned!", this); valid = false; }
        if (player2SpawnPoint == null) { Debug.LogError("RS: Player 2 Spawn Point not assigned!", this); valid = false; }
        if (player1Controls == null) { Debug.LogError("RS: Player 1 Controls Profile not assigned!", this); valid = false; }
        if (player2Controls == null) { Debug.LogError("RS: Player 2 Controls Profile not assigned!", this); valid = false; }
        return valid;
    }

    void SpawnAndSetupPlayers()
    {
        Quaternion p1InitialRootRotation = player1SpawnPoint.rotation;
        GameObject p1Obj = Instantiate(player1Prefab, player1SpawnPoint.position, p1InitialRootRotation);
        p1Obj.name = "Player1_Fighter_Instance";
        fighterA_instance = p1Obj.GetComponent<FighterCharacter>();
        if (fighterA_instance == null) { Debug.LogError("RS: P1 Prefab missing FighterCharacter script!", this); Destroy(p1Obj); return; }
        fighterA_instance.controlsProfile = player1Controls; fighterA_instance.InitializeCoreInput();
        fighterA_instance.SetInitialVisualYRotationOffset(player1YRotationOffset);

        Quaternion p2InitialRootRotation = player2SpawnPoint.rotation;
        GameObject p2Obj = Instantiate(player2Prefab, player2SpawnPoint.position, p2InitialRootRotation);
        p2Obj.name = "Player2_Fighter_Instance";
        fighterB_instance = p2Obj.GetComponent<FighterCharacter>();
        if (fighterB_instance == null) { Debug.LogError("RS: P2 Prefab missing FighterCharacter script!", this); Destroy(p2Obj); return; }
        fighterB_instance.controlsProfile = player2Controls; fighterB_instance.InitializeCoreInput();
        fighterB_instance.SetInitialVisualYRotationOffset(player2YRotationOffset);
    }

    void Update()
    {
        if (roundActive)
        {
            if (timerText != null) timerText.text = roundTimerSec.ToString("00");
            CheckLowHealthWarnings();
        }
    }

    void FixedUpdate()
    {
        if (!roundActive || fighterA_instance?.core == null || fighterB_instance?.core == null || rbA == null || rbB == null) return;
        if (firstFrameAfterUnpause) { if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position; firstFrameAfterUnpause = false; }
        Vector2 p1CFP = rbA.position; Vector2 p2CFP = rbB.position;
        FaceOpponent(fighterA_instance, fighterB_instance); FaceOpponent(fighterB_instance, fighterA_instance);
        EnforceMaxCharacterDistance(p1CFP, p2CFP);
        if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position;
    }

    void FaceOpponent(FighterCharacter me, FighterCharacter foe)
    {
        if (me?.core == null || foe?.core == null) return;
        Rigidbody2D meRB = me.GetComponent<Rigidbody2D>(); Rigidbody2D foeRB = foe.GetComponent<Rigidbody2D>();
        if (meRB == null || foeRB == null) return;
        me.core.SetFacing(foeRB.position.x > meRB.position.x);
    }

    void EnforceMaxCharacterDistance(Vector2 p1OriginalPos, Vector2 p2OriginalPos)
    {
        float currentXDistance = Mathf.Abs(p1OriginalPos.x - p2OriginalPos.x);
        float tolerance = 0.001f;
        if (currentXDistance > maxCharacterXDistance + tolerance)
        {
            Vector2 finalClampedP1 = p1OriginalPos; Vector2 finalClampedP2 = p2OriginalPos;
            bool p1IsLeft = p1OriginalPos.x < p2OriginalPos.x;
            if (p1IsLeft) { float p1TX = p2OriginalPos.x - maxCharacterXDistance; if (finalClampedP1.x < p1TX) finalClampedP1.x = p1TX; float p2TX = finalClampedP1.x + maxCharacterXDistance; if (finalClampedP2.x > p2TX) finalClampedP2.x = p2TX; }
            else { float p1TX = p2OriginalPos.x + maxCharacterXDistance; if (finalClampedP1.x > p1TX) finalClampedP1.x = p1TX; float p2TX = finalClampedP1.x - maxCharacterXDistance; if (finalClampedP2.x < p2TX) finalClampedP2.x = p2TX; }
            float distanceAfterHardClamps = Mathf.Abs(finalClampedP1.x - finalClampedP2.x);
            if (Mathf.Abs(distanceAfterHardClamps - maxCharacterXDistance) > tolerance) { float remainingOverlapOrGap = distanceAfterHardClamps - maxCharacterXDistance; float correction = remainingOverlapOrGap / 2.0f; if (finalClampedP1.x < finalClampedP2.x) { finalClampedP1.x += correction; finalClampedP2.x -= correction; } else { finalClampedP1.x -= correction; finalClampedP2.x += correction; } }
            if (rbA != null && Vector2.Distance(rbA.position, finalClampedP1) > tolerance) rbA.MovePosition(finalClampedP1);
            if (rbB != null && Vector2.Distance(rbB.position, finalClampedP2) > tolerance) rbB.MovePosition(finalClampedP2);
            if (fighterA_instance?.core != null) fighterA_instance.core.SyncPosition(finalClampedP1);
            if (fighterB_instance?.core != null) fighterB_instance.core.SyncPosition(finalClampedP2);
        }
    }
    
    IEnumerator MatchLoop()
    {
        winsA = 0; winsB = 0; UpdateWinCountUI(); matchAnimalityEnabled = false; currentMatchRoundNumber = 0;
        while (true)
        {
            currentMatchRoundNumber = 0; winsA = 0; winsB = 0; matchAnimalityEnabled = false;
            if (fighterA_instance != null) fighterA_instance.MatchReset();
            if (fighterB_instance != null) fighterB_instance.MatchReset();
            
            if (roundOutcomeText != null) roundOutcomeText.gameObject.SetActive(false); // Ensure cleared at match start
            if (flawlessVictoryText != null) flawlessVictoryText.gameObject.SetActive(false);
            if (roundNumberText != null) roundNumberText.gameObject.SetActive(false);

            for (int rdDisplayNum = 1; ; rdDisplayNum++)
            {
                currentMatchRoundNumber++;
                SnapRoundStartVisuals();
                if (FadeController.I != null) yield return FadeController.I.FadeIn(fadeDuration);
                PrepareRound(rdDisplayNum);
                yield return StartCoroutine(RoundLoop(rdDisplayNum));
                if (postKOState == PostKOSubState.MercyGranted) { rdDisplayNum--; postKOState = PostKOSubState.None; continue; }
                if (winsA >= roundsToWin || winsB >= roundsToWin) break;
                UpdateWinCountUI();
                if (FadeController.I != null)
                {
                    yield return FadeController.I.FadeOut(fadeDuration);
                    // once the screen is fully black, hide the old round texts
                    if (roundOutcomeText    != null) roundOutcomeText.gameObject.SetActive(false);
                    if (flawlessVictoryText  != null) flawlessVictoryText.gameObject.SetActive(false);
                }
            }
            UpdateWinCountUI();
            yield return StartCoroutine(GameOverSequence((winsA > winsB) ? fighterA_instance : fighterB_instance));
        }
    }

    void SnapRoundStartVisuals()
    {
        if (fighterA_instance != null) fighterA_instance.RoundReset(player1SpawnPoint.position, true);
        if (fighterB_instance != null) fighterB_instance.RoundReset(player2SpawnPoint.position, false);
        if (rbA != null) rbA.position = player1SpawnPoint.position; if (rbB != null) rbB.position = player2SpawnPoint.position;
        if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position;
        firstFrameAfterUnpause = true;
        RewindAnimator(fighterA_instance); RewindAnimator(fighterB_instance);
    }

    void PrepareRound(int displayRoundNum)
    {
        if (fighterA_instance != null) fighterA_instance.ForcePaused(true);
        if (fighterB_instance != null) fighterB_instance.ForcePaused(true);
        lowHealthWarningPlayedP1 = false; lowHealthWarningPlayedP2 = false;
        roundTimerSec = timeLimit;
        if (timerText != null) { timerText.gameObject.SetActive(true); timerText.text = roundTimerSec.ToString("00"); }
        if (roundNumberText != null) roundNumberText.gameObject.SetActive(true);
        if (roundOutcomeText != null) roundOutcomeText.gameObject.SetActive(false);
        if (flawlessVictoryText != null) flawlessVictoryText.gameObject.SetActive(false);
        InputBuffer iA = fighterA_instance?.GetComponent<InputBuffer>(); InputBuffer iB = fighterB_instance?.GetComponent<InputBuffer>();
        if (iA != null) iA.ClearPrev(); if (iB != null) iB.ClearPrev();
        postKOState = PostKOSubState.None; currentRoundVictor = null; currentRoundDefeated = null; currentRoundEndCondition = RoundEndType.None;
    }
    
    IEnumerator RoundLoop(int displayRoundNum)
    {
        SetAnimatorSpeed(fighterA_instance, 1f); SetAnimatorSpeed(fighterB_instance, 1f);
        if (roundNumberText != null) roundNumberText.text = $"ROUND {displayRoundNum}";
        AudioRoundManager.Play(displayRoundNum == 1 ? GameEvent.RoundOne : (displayRoundNum == 2 ? GameEvent.RoundTwo : GameEvent.RoundThree));
        yield return new WaitForSeconds(introFreeze);
        if (roundNumberText != null) roundNumberText.text = "FIGHT!"; 
        AudioRoundManager.Play(GameEvent.Fight);
        yield return new WaitForSeconds(fightFlash);
        if (roundNumberText != null) roundNumberText.gameObject.SetActive(false);
        BeginCombat();

        float lastSecondUpdateTime = Time.time;
        while (roundActive)
        {
            bool p1IsDead = (fighterA_instance != null && fighterA_instance.Health <= 0);
            bool p2IsDead = (fighterB_instance != null && fighterB_instance.Health <= 0);
            bool timeHasExpired = roundTimerSec <= 0;
            if (p1IsDead || p2IsDead || timeHasExpired) { EndCombat(); ProcessRoundOutcome(p1IsDead, p2IsDead, timeHasExpired); break; }
            if (Time.time - lastSecondUpdateTime >= 1.0f) { if (roundActive) roundTimerSec--; lastSecondUpdateTime += 1.0f; if (roundTimerSec <= 10 && roundTimerSec > 0 && roundActive) AudioRoundManager.Play(GameEvent.Last10SecTick); }
            yield return null;
        }

        bool matchWonThisRound = (currentRoundVictor != null && ((currentRoundVictor == fighterA_instance && winsA >= roundsToWin) || (currentRoundVictor == fighterB_instance && winsB >= roundsToWin)));
        int previousWinsA = (currentRoundVictor == fighterA_instance && currentRoundEndCondition != RoundEndType.DoubleKO && currentRoundEndCondition != RoundEndType.TimeUpDraw) ? winsA - 1 : winsA;
        int previousWinsB = (currentRoundVictor == fighterB_instance && currentRoundEndCondition != RoundEndType.DoubleKO && currentRoundEndCondition != RoundEndType.TimeUpDraw) ? winsB - 1 : winsB;
        bool mercyConditionsMet = currentRoundEndCondition == RoundEndType.KO && currentRoundVictor != null && currentRoundVictor.core.CanPerformMercyThisMatch && currentRoundDefeated.core.IsMercyEligibleThisRound && currentMatchRoundNumber == 3 && previousWinsA == 1 && previousWinsB == 1;

        if ((matchWonThisRound && currentRoundEndCondition == RoundEndType.KO && !mercyConditionsMet) || mercyConditionsMet)
        {
            yield return StartCoroutine(HandleFinisherSequence(currentRoundVictor, currentRoundDefeated, mercyConditionsMet));
            if (postKOState == PostKOSubState.MercyGranted) { yield break; }
        }
        else
        {
            yield return StartCoroutine(StandardRoundEndSequence());
            float pauseDuration = (currentRoundEndCondition == RoundEndType.TimeUpWin || currentRoundEndCondition == RoundEndType.TimeUpDraw) ? endOfRoundPause : 0.4f;
            yield return new WaitForSeconds(pauseDuration + STANDARD_ROUND_END_MESSAGE_DURATION);
            // Texts cleared by PrepareRound or GameOverSequence after fade
        }
    }

    void ProcessRoundOutcome(bool p1Dead, bool p2Dead, bool timeUp) 
    { 
        currentRoundVictor = null; currentRoundDefeated = null; currentRoundEndCondition = RoundEndType.None; 
        if (p1Dead && p2Dead) { currentRoundEndCondition = RoundEndType.DoubleKO; } 
        else if (p1Dead) { currentRoundVictor = fighterB_instance; currentRoundDefeated = fighterA_instance; currentRoundEndCondition = RoundEndType.KO; } 
        else if (p2Dead) { currentRoundVictor = fighterA_instance; currentRoundDefeated = fighterB_instance; currentRoundEndCondition = RoundEndType.KO; } 
        else if (timeUp) { if (fighterA_instance.Health > fighterB_instance.Health) { currentRoundVictor = fighterA_instance; currentRoundDefeated = fighterB_instance; currentRoundEndCondition = RoundEndType.TimeUpWin; } else if (fighterB_instance.Health > fighterA_instance.Health) { currentRoundVictor = fighterB_instance; currentRoundDefeated = fighterA_instance; currentRoundEndCondition = RoundEndType.TimeUpWin; } else { currentRoundEndCondition = RoundEndType.TimeUpDraw; } } 
        if (currentRoundVictor != null) { if (currentRoundVictor == fighterA_instance) winsA++; else winsB++; if(currentRoundDefeated?.core != null) currentRoundDefeated.core.SetKOAsFriendly(false); } 
    }
    
   IEnumerator HandleFinisherSequence(
    FighterCharacter winner,
    FighterCharacter loser,
    bool isMercyPossibleThisRound)
{
    winner.ForcePaused(false);
    // 1) Hide the round timer
    if (timerText != null)
        timerText.gameObject.SetActive(false);

    // 2) FINISH HIM/HER prompt
    if (roundNumberText != null)
    {
        roundNumberText.gameObject.SetActive(true);
        roundNumberText.text = (loser.core.CharInfo.gender == Gender.Female)
            ? "FINISH HER!"
            : "FINISH HIM!";
    }
    AudioRoundManager.Play(
        loser.core.CharInfo.gender == Gender.Female
            ? GameEvent.FinishHer
            : GameEvent.FinishHim);

    // 3) Freeze victim, unfreeze winner
    loser.core.SetState(CharacterState.FinishHimVictim);
    loser.ForcePaused(true);
    winner.core.SetState(CharacterState.FinishHimWinner);
    winner.ForcePaused(false);
    postKOState = PostKOSubState.AwaitingFinisherInput;

    // 4) Hold prompt
    yield return new WaitForSeconds(2f);
    roundNumberText?.gameObject.SetActive(false);

    // 5) Input window (3s)
    float timer = 3f;
    bool acted = false;
    while (timer > 0f && !acted)
    {
        var ib = winner.GetComponent<InputBuffer>();

        // Mercy check…
        // (omitted here for brevity)

        timer -= Time.deltaTime;
        yield return null;
    }

    // 6) Default collapse if no finisher
    if (!acted)
    {
        loser.core.SetState(CharacterState.Knockdown);
        loser.TriggerKnockdownAnim();
        // Winner can already move—but don't yet play victory here;
        // we'll do it in step 9.
    }

        winner.ForcePaused(true);
        winner.PlayWinAnimation();

    // 7) “X WINS” text
    string name = (winner.core?.CharInfo?.characterName?.ToUpper())
        ?? (winner == Player1 ? "PLAYER 1" : "PLAYER 2");
    roundOutcomeText?.gameObject.SetActive(true);
    roundOutcomeText.text = $"{name} WINS";

    // 8) Name+Wins SFX sequence
    yield return StartCoroutine(PlayWinAudioSequence(winner));

    // 9) Pause for readability
    yield return new WaitForSeconds(1f);

    // 10) Flawless?
    bool flawless = winner.core.Health == FighterCharacterCore.MAX_HEALTH
                   && currentRoundEndCondition == RoundEndType.KO;
    if (flawless)
    {
        flawlessVictoryText?.gameObject.SetActive(true);
        flawlessVictoryText.text = "FLAWLESS VICTORY";
        AudioRoundManager.Play(GameEvent.FlawlessVictory);
        yield return new WaitForSeconds(1.5f);
    }

    // 11) Final pause before clearing both texts
yield return new WaitForSeconds(1.5f);

// 12) Hide both winner and flawless texts together
if (roundOutcomeText    != null) roundOutcomeText.gameObject.SetActive(false);
if (flawlessVictoryText  != null) flawlessVictoryText.gameObject.SetActive(false);

// 13) Play the winner’s victory animation


// 14) Immediately freeze the winner so they can no longer move


// 15) Mark that we're done with the finisher sequence
postKOState = PostKOSubState.ProceedToRoundEnd;
}


    
    IEnumerator GameOverSequence(FighterCharacter matchWinner)
    {
        // hide any leftover texts
        roundNumberText   ?.gameObject.SetActive(false);
        roundOutcomeText  ?.gameObject.SetActive(false);
        flawlessVictoryText?.gameObject.SetActive(false);

        // 1) wait 1.5s before showing GAME OVER
        yield return new WaitForSeconds(1.5f);

        // 2) show GAME OVER
        roundOutcomeText?.gameObject.SetActive(true);
        roundOutcomeText.text = "GAME OVER";

        // 3) play the Game Over clip
        AudioRoundManager.Play(GameEvent.GameOver);

        // 4) wait another 1.5s so player reads it
        yield return new WaitForSeconds(1.5f);

        // 5) then fade out
        if (FadeController.I != null)
            yield return FadeController.I.FadeOut(gameOverFadeDuration);

        // finally clean up
        roundOutcomeText?.gameObject.SetActive(false);
    }
    
    void BeginCombat(){if(fighterA_instance!=null)fighterA_instance.ForcePaused(false);if(fighterB_instance!=null)fighterB_instance.ForcePaused(false);roundActive=true;firstFrameAfterUnpause=true;if(rbA!=null&&fighterA_instance!=null)rbA.position=fighterA_instance.transform.position;if(rbB!=null&&fighterB_instance!=null)rbB.position=fighterB_instance.transform.position;if(fighterA_instance?.core!=null&&rbA!=null)fighterA_instance.core.SyncPosition(rbA.position);if(fighterB_instance?.core!=null&&rbB!=null)fighterB_instance.core.SyncPosition(rbB.position);roundTimerSec=timeLimit;lowHealthWarningPlayedP1=false;lowHealthWarningPlayedP2=false;}
    void EndCombat(){roundActive=false;
    if(fighterA_instance!=null)fighterA_instance.ForcePaused(true);
    if(fighterB_instance!=null)fighterB_instance.ForcePaused(true);}
    
    IEnumerator StandardRoundEndSequence()
    { 
        yield return new WaitForSeconds(0.5f); 
        string outcomeMsg="DRAW";
        FighterCharacter winnerForAudio=currentRoundVictor; 
        if(roundOutcomeText!=null)roundOutcomeText.gameObject.SetActive(true); 
        if(flawlessVictoryText!=null)flawlessVictoryText.gameObject.SetActive(false);
        if (currentRoundVictor != null)
        {
            if (currentRoundVictor.core?.CharInfo != null)
                outcomeMsg = currentRoundVictor.core.CharInfo.characterName.ToUpper() + " WINS";
            else
                outcomeMsg = (currentRoundVictor == fighterA_instance ? "PLAYER 1" : "PLAYER 2") + " WINS";
                currentRoundVictor.PlayWinAnimation();

            if (currentRoundDefeated?.core != null)
            {
                if (currentRoundDefeated.core.Health <= 0) {
                    currentRoundDefeated.core.SetState(CharacterState.Knockdown);
                    currentRoundDefeated.PlayDefeatedAnimation();
                } else if (currentRoundEndCondition == RoundEndType.TimeUpWin){
                    currentRoundDefeated.core.SetState(CharacterState.Idle);
                }
            }
        }
        else if (currentRoundEndCondition == RoundEndType.TimeUpDraw || currentRoundEndCondition == RoundEndType.DoubleKO)
        {
            outcomeMsg = "DRAW";
            fighterA_instance?.core?.SetState(CharacterState.Idle);
            fighterB_instance?.core?.SetState(CharacterState.Idle);
        }
        
        if (roundOutcomeText != null) roundOutcomeText.text = outcomeMsg;
        yield return StartCoroutine(PlayWinAudioSequence(winnerForAudio));
        yield return new WaitForSeconds(1.0f); 
        
        bool wasFlawless = 
        (currentRoundVictor != null 
        && currentRoundVictor.core.Health == FighterCharacterCore.MAX_HEALTH 
        && currentRoundEndCondition == RoundEndType.KO);

        if (wasFlawless) { if (flawlessVictoryText != null) { flawlessVictoryText.gameObject.SetActive(true); flawlessVictoryText.text = "FLAWLESS VICTORY"; } AudioRoundManager.Play(GameEvent.FlawlessVictory); } 
        UpdateWinCountUI();
    }

    IEnumerator PlayWinAudioSequence(FighterCharacter winner)
    {
        if (winner != null && winner.core?.CharInfo != null)
        {
            AudioClip nameClip = winner.core.CharInfo.nameAudioClip;
            if (nameClip != null)
            {
                AudioSource tempAudioSource = gameObject.GetComponent<AudioSource>();
                if (tempAudioSource == null) tempAudioSource = gameObject.AddComponent<AudioSource>();
                tempAudioSource.PlayOneShot(nameClip);
                yield return new WaitForSeconds(nameClip.length + 0.1f);
            }
            else { yield return new WaitForSeconds(0.3f); }
            AudioRoundManager.Play(GameEvent.Wins);
            yield return new WaitForSeconds(0.7f); 
        }
        else if (currentRoundEndCondition == RoundEndType.TimeUpDraw || currentRoundEndCondition == RoundEndType.DoubleKO) { yield return new WaitForSeconds(0.5f); }
    }

    void CheckLowHealthWarnings(){if(fighterA_instance?.core!=null&&!lowHealthWarningPlayedP1&&fighterA_instance.Health>0&&fighterA_instance.Health<=FighterCharacterCore.MAX_HEALTH*LOW_HEALTH_THRESHOLD_PERCENT){AudioRoundManager.Play(GameEvent.LastHitWarning);lowHealthWarningPlayedP1=true;}if(fighterB_instance?.core!=null&&!lowHealthWarningPlayedP2&&fighterB_instance.Health>0&&fighterB_instance.Health<=FighterCharacterCore.MAX_HEALTH*LOW_HEALTH_THRESHOLD_PERCENT){AudioRoundManager.Play(GameEvent.LastHitWarning);lowHealthWarningPlayedP2=true;}}
    void UpdateWinCountUI(){/* For dedicated win UI */}
    
void OnGUI()
{
    if (!Application.isPlaying || fighterA_instance == null || fighterB_instance == null)
        return;

    // Always draw both health bars whenever the game is running
    DrawPlayerLifebar(fighterA_instance, true);
    DrawPlayerLifebar(fighterB_instance, false);
}


    void DrawPlayerLifebar(FighterCharacter player, bool isPlayer1)
    {
        if (player?.core == null) return;
        float currentHealth = player.Health; float maxHealth = FighterCharacterCore.MAX_HEALTH;
        float fillRatio = (maxHealth > 0) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        int filledWidthPixels = Mathf.RoundToInt(fillRatio * LIFE_BAR_WIDTH_PX);
        Rect backgroundRect, foregroundRect;
        Color emptyBarColor = new Color(0.3f, 0.3f, 0.3f, 0.8f); Color filledBarColor = Color.green;
        bool useFlashingColor = false;
        if (player.Health > 0 && player.Health <= FighterCharacterCore.MAX_HEALTH * LOW_HEALTH_THRESHOLD_PERCENT)
        {
            if (player == fighterA_instance && lowHealthWarningPlayedP1) useFlashingColor = true;
            if (player == fighterB_instance && lowHealthWarningPlayedP2) useFlashingColor = true;
        }
        if (useFlashingColor) { filledBarColor = (Time.time % 0.4f < 0.2f) ? Color.red : new Color(1f, 0.3f, 0.3f); }

        if (isPlayer1)
        {
            backgroundRect = new Rect(LIFE_BAR_P1_X_POS, LIFE_BAR_Y_POS, LIFE_BAR_WIDTH_PX, LIFE_BAR_HEIGHT_PX);
            foregroundRect = new Rect(backgroundRect.x, backgroundRect.y, filledWidthPixels, backgroundRect.height);
        }
        else 
        {
            float p2_Xpos = Screen.width - LIFE_BAR_P1_X_POS - LIFE_BAR_WIDTH_PX;
            backgroundRect = new Rect(p2_Xpos, LIFE_BAR_Y_POS, LIFE_BAR_WIDTH_PX, LIFE_BAR_HEIGHT_PX);
            foregroundRect = new Rect(backgroundRect.xMax - filledWidthPixels, backgroundRect.y, filledWidthPixels, backgroundRect.height);
        }
        GUI.color = emptyBarColor; GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = filledBarColor; GUI.DrawTexture(foregroundRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = Color.black; DrawGUIRectOutline(backgroundRect, 1); GUI.color = Color.white;
    }

    void DrawGUIRectOutline(Rect rect, int thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

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
            const string DSN = "Locomotion"; int lI = 0; int dSH = Animator.StringToHash(DSN); 
            if (anim.HasState(lI, dSH)) anim.Play(dSH, lI, 0f); 
            else if (anim.GetCurrentAnimatorStateInfo(lI).fullPathHash != 0) anim.Play(anim.GetCurrentAnimatorStateInfo(lI).fullPathHash, lI, 0f); 
            anim.Update(0f); 
        } 
    }
}
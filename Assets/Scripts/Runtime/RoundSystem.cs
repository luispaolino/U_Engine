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

    [Header("UI (Excluding OnGUI elements)")]
    public Text centerMessage;
    public Text timerText;

    [Header("Round & Timer")]
    public int timeLimit = 90;
    public int roundsToWin = 2;
    public float introFreeze = 1.2f;
    public float fightFlash = 0.8f;
    public float koFreeze = 0.4f; // Base pause after normal KO or finisher timeout
    public const float FINISHER_TIMER_DURATION = 7.0f;
    private const float MERCY_OUTCOME_WAIT_SEC = 0.5f; // Pause after mercy related messages

    [Header("Distance Clamping")]
    public float maxCharacterXDistance = 5.82f;

    [Header("Fade")]
    public float fadeDuration = 0.5f;
    public float gameOverFadeDuration = 0.8f;

    /* ───────────────────────────── Runtime References & State ───────────────────────────── */
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
        fighterA_instance.MatchReset(); fighterB_instance.MatchReset();
        fighterA_instance.ForcePaused(true); fighterB_instance.ForcePaused(true);
        roundActive = false; firstFrameAfterUnpause = true;
        if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position;
        UpdateWinCountUI(); StartCoroutine(MatchLoop());
    }

    bool ValidatePlayerSetup() { bool v = true; if (player1Prefab == null) { v = false; Debug.LogError("RS: P1 Prefab missing!"); } if (player2Prefab == null) { v = false; Debug.LogError("RS: P2 Prefab missing!"); } if (player1SpawnPoint == null) { v = false; Debug.LogError("RS: P1 Spawn missing!"); } if (player2SpawnPoint == null) { v = false; Debug.LogError("RS: P2 Spawn missing!"); } if (player1Controls == null) { v = false; Debug.LogError("RS: P1 Controls missing!"); } if (player2Controls == null) { v = false; Debug.LogError("RS: P2 Controls missing!"); } return v; }
    void SpawnAndSetupPlayers() { Quaternion p1Rot = player1SpawnPoint.rotation; GameObject p1Obj = Instantiate(player1Prefab, player1SpawnPoint.position, p1Rot); p1Obj.name = "P1_Fighter_Inst"; fighterA_instance = p1Obj.GetComponent<FighterCharacter>(); if (fighterA_instance == null) { Debug.LogError("RS: P1 Prefab no FC script!", this); Destroy(p1Obj); return; } fighterA_instance.controlsProfile = player1Controls; fighterA_instance.InitializeCoreInput(); fighterA_instance.SetInitialVisualYRotationOffset(player1YRotationOffset); Quaternion p2Rot = player2SpawnPoint.rotation; GameObject p2Obj = Instantiate(player2Prefab, player2SpawnPoint.position, p2Rot); p2Obj.name = "P2_Fighter_Inst"; fighterB_instance = p2Obj.GetComponent<FighterCharacter>(); if (fighterB_instance == null) { Debug.LogError("RS: P2 Prefab no FC script!", this); Destroy(p2Obj); return; } fighterB_instance.controlsProfile = player2Controls; fighterB_instance.InitializeCoreInput(); fighterB_instance.SetInitialVisualYRotationOffset(player2YRotationOffset); }
    void Update() { if (roundActive) { if (timerText != null) timerText.text = roundTimerSec.ToString("00"); CheckLowHealthWarnings(); } }
    void FixedUpdate() { if (!roundActive || fighterA_instance?.core == null || fighterB_instance?.core == null || rbA == null || rbB == null) return; if (firstFrameAfterUnpause) { if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position; firstFrameAfterUnpause = false; } Vector2 p1CFP = rbA.position; Vector2 p2CFP = rbB.position; FaceOpponent(fighterA_instance, fighterB_instance); FaceOpponent(fighterB_instance, fighterA_instance); EnforceMaxCharacterDistance(p1CFP, p2CFP); if (rbA != null) prevP1Pos = rbA.position; if (rbB != null) prevP2Pos = rbB.position; }
    void FaceOpponent(FighterCharacter me, FighterCharacter foe) { if (me?.core == null || foe?.core == null) return; Rigidbody2D meRB = me.GetComponent<Rigidbody2D>(); Rigidbody2D foeRB = foe.GetComponent<Rigidbody2D>(); if (meRB == null || foeRB == null) return; me.core.SetFacing(foeRB.position.x > meRB.position.x); }
    void EnforceMaxCharacterDistance(Vector2 p1O, Vector2 p2O) { float curXDist = Mathf.Abs(p1O.x - p2O.x); float tol = 0.001f; if (curXDist > maxCharacterXDistance + tol) { Vector2 fcp1 = p1O; Vector2 fcp2 = p2O; bool p1L = p1O.x < p2O.x; if (p1L) { float p1TX = p2O.x - maxCharacterXDistance; if (fcp1.x < p1TX) fcp1.x = p1TX; float p2TX = fcp1.x + maxCharacterXDistance; if (fcp2.x > p2TX) fcp2.x = p2TX; } else { float p1TX = p2O.x + maxCharacterXDistance; if (fcp1.x > p1TX) fcp1.x = p1TX; float p2TX = fcp1.x - maxCharacterXDistance; if (fcp2.x < p2TX) fcp2.x = p2TX; } float distAHC = Mathf.Abs(fcp1.x - fcp2.x); if (Mathf.Abs(distAHC - maxCharacterXDistance) > tol) { float remOG = distAHC - maxCharacterXDistance; float corr = remOG / 2.0f; if (fcp1.x < fcp2.x) { fcp1.x += corr; fcp2.x -= corr; } else { fcp1.x -= corr; fcp2.x += corr; } } if (rbA != null && Vector2.Distance(rbA.position, fcp1) > tol) rbA.MovePosition(fcp1); if (rbB != null && Vector2.Distance(rbB.position, fcp2) > tol) rbB.MovePosition(fcp2); if (fighterA_instance?.core != null) fighterA_instance.core.SyncPosition(fcp1); if (fighterB_instance?.core != null) fighterB_instance.core.SyncPosition(fcp2); } }
    
    IEnumerator MatchLoop() { winsA=0; winsB=0; UpdateWinCountUI(); matchAnimalityEnabled=false; currentMatchRoundNumber=0; while(true){ currentMatchRoundNumber=0; winsA=0; winsB=0; matchAnimalityEnabled=false; if(fighterA_instance!=null)fighterA_instance.MatchReset(); if(fighterB_instance!=null)fighterB_instance.MatchReset(); for(int rdDN=1;;rdDN++){ currentMatchRoundNumber++; SnapRoundStartVisuals(); if(FadeController.I!=null)yield return FadeController.I.FadeIn(fadeDuration); PrepareRound(rdDN); yield return StartCoroutine(RoundLoop(rdDN)); if(postKOState == PostKOSubState.MercyGranted) { /* Mercy was done, round effectively restarts combat, don't increment display round num here */ rdDN--; /* Redo this round display number after mercy */ continue; /* Skip to next iteration of for loop to restart round properly */ } if(winsA>=roundsToWin||winsB>=roundsToWin)break; UpdateWinCountUI(); if(FadeController.I!=null)yield return FadeController.I.FadeOut(fadeDuration); } UpdateWinCountUI(); yield return StartCoroutine(GameOverSequence((winsA>winsB)?fighterA_instance:fighterB_instance));}}
    void SnapRoundStartVisuals() { if(fighterA_instance!=null)fighterA_instance.RoundReset(player1SpawnPoint.position,true); if(fighterB_instance!=null)fighterB_instance.RoundReset(player2SpawnPoint.position,false); if(rbA!=null)rbA.position=player1SpawnPoint.position; if(rbB!=null)rbB.position=player2SpawnPoint.position; if(rbA!=null)prevP1Pos=rbA.position; if(rbB!=null)prevP2Pos=rbB.position; firstFrameAfterUnpause=true; RewindAnimator(fighterA_instance);RewindAnimator(fighterB_instance);}
    void PrepareRound(int dRN){if(fighterA_instance!=null)fighterA_instance.ForcePaused(true);if(fighterB_instance!=null)fighterB_instance.ForcePaused(true);lowHealthWarningPlayedP1=false;lowHealthWarningPlayedP2=false;roundTimerSec=timeLimit;if(timerText!=null)timerText.text=roundTimerSec.ToString("00");InputBuffer iA=fighterA_instance?.GetComponent<InputBuffer>();InputBuffer iB=fighterB_instance?.GetComponent<InputBuffer>();if(iA!=null)iA.ClearPrev();if(iB!=null)iB.ClearPrev();postKOState=PostKOSubState.None;currentRoundVictor=null;currentRoundDefeated=null;currentRoundEndCondition=RoundEndType.None;}
    
    IEnumerator RoundLoop(int displayRoundNum)
    {
        SetAnimatorSpeed(fighterA_instance, 1f); SetAnimatorSpeed(fighterB_instance, 1f);
        if (centerMessage != null) centerMessage.text = $"ROUND {displayRoundNum}";
        AudioRoundManager.Play(displayRoundNum == 1 ? GameEvent.RoundOne : (displayRoundNum == 2 ? GameEvent.RoundTwo : GameEvent.RoundThree));
        yield return new WaitForSeconds(introFreeze);
        if (centerMessage != null) centerMessage.text = string.Empty;
        if (centerMessage != null) centerMessage.text = "FIGHT!"; AudioRoundManager.Play(GameEvent.Fight);
        yield return new WaitForSeconds(fightFlash);
        if (centerMessage != null) centerMessage.text = string.Empty;
        
        BeginCombat(); // Unpauses fighters, resets some round states

        float lastSecondUpdateTime = Time.time;
        while (roundActive)
        {
            bool p1IsDead = (fighterA_instance != null && fighterA_instance.Health <= 0);
            bool p2IsDead = (fighterB_instance != null && fighterB_instance.Health <= 0);
            bool timeHasExpired = roundTimerSec <= 0;

            if (p1IsDead || p2IsDead || timeHasExpired)
            {
                EndCombat(); // Pauses fighters, sets roundActive = false
                ProcessRoundOutcome(p1IsDead, p2IsDead, timeHasExpired); // Determines victor, updates win counts
                break; 
            }

            if (Time.time - lastSecondUpdateTime >= 1.0f)
            {
                if (roundActive) roundTimerSec--;
                lastSecondUpdateTime += 1.0f;
                if (roundTimerSec <= 10 && roundTimerSec > 0 && roundActive) AudioRoundManager.Play(GameEvent.Last10SecTick);
            }
            yield return null;
        }

        // --- Post-Round Active Logic ---
        bool matchWonThisRound = (currentRoundVictor != null && ((currentRoundVictor == fighterA_instance && winsA >= roundsToWin) || (currentRoundVictor == fighterB_instance && winsB >= roundsToWin)));
        
        int previousWinsA = (currentRoundVictor == fighterA_instance && currentRoundEndCondition != RoundEndType.DoubleKO && currentRoundEndCondition != RoundEndType.TimeUpDraw) ? winsA - 1 : winsA;
        int previousWinsB = (currentRoundVictor == fighterB_instance && currentRoundEndCondition != RoundEndType.DoubleKO && currentRoundEndCondition != RoundEndType.TimeUpDraw) ? winsB - 1 : winsB;
        
        bool mercyConditionsMetForThisKO = currentRoundEndCondition == RoundEndType.KO && currentRoundVictor != null &&
                                      currentRoundVictor.core.CanPerformMercyThisMatch && 
                                      currentRoundDefeated.core.IsMercyEligibleThisRound &&
                                      currentMatchRoundNumber == 3 && 
                                      previousWinsA == 1 && previousWinsB == 1;

        if (matchWonThisRound && currentRoundEndCondition == RoundEndType.KO && !mercyConditionsMetForThisKO)
        {
            yield return StartCoroutine(HandleFinisherSequence(currentRoundVictor, currentRoundDefeated, false)); // isMercyEligibleRound = false
        }
        else if (mercyConditionsMetForThisKO) // Mercy conditions are met
        {
             yield return StartCoroutine(HandleFinisherSequence(currentRoundVictor, currentRoundDefeated, true)); // isMercyEligibleRound = true
             if (postKOState == PostKOSubState.MercyGranted)
             {
                 yield break; // Exit RoundLoop, MatchLoop will continue to effectively restart the round due to Mercy
             }
        }
        else // Normal round end (not a match-deciding KO), or match won by Time Up, or Draw
        {
            // This calls PlayWinAnnouncementSequence for winner/loser animations and audio
            yield return StartCoroutine(DeclareRoundOutcomeAndAnimate()); 
            yield return new WaitForSeconds(koFreeze + 1.0f); // Adjusted wait time
        }
    }

    void ProcessRoundOutcome(bool p1Dead, bool p2Dead, bool timeUp) { /* As before */ currentRoundVictor = null; currentRoundDefeated = null; currentRoundEndCondition = RoundEndType.None; if (p1Dead && p2Dead) { currentRoundEndCondition = RoundEndType.DoubleKO; } else if (p1Dead) { currentRoundVictor = fighterB_instance; currentRoundDefeated = fighterA_instance; currentRoundEndCondition = RoundEndType.KO; } else if (p2Dead) { currentRoundVictor = fighterA_instance; currentRoundDefeated = fighterB_instance; currentRoundEndCondition = RoundEndType.KO; } else if (timeUp) { if (fighterA_instance.Health > fighterB_instance.Health) { currentRoundVictor = fighterA_instance; currentRoundDefeated = fighterB_instance; currentRoundEndCondition = RoundEndType.TimeUpWin; } else if (fighterB_instance.Health > fighterA_instance.Health) { currentRoundVictor = fighterB_instance; currentRoundDefeated = fighterA_instance; currentRoundEndCondition = RoundEndType.TimeUpWin; } else { currentRoundEndCondition = RoundEndType.TimeUpDraw; } } if (currentRoundVictor != null) { if (currentRoundVictor == fighterA_instance) winsA++; else winsB++; if(currentRoundDefeated?.core != null) currentRoundDefeated.core.SetKOAsFriendly(false); } }
    
    IEnumerator HandleFinisherSequence(FighterCharacter winner, FighterCharacter loser, bool isMercyPossibleThisRound)
    {
        if (winner?.core == null || loser?.core == null) { postKOState = PostKOSubState.ProceedToRoundEnd; yield break; }

        if (timerText != null) timerText.gameObject.SetActive(false); // Hide round timer

        if (centerMessage != null) centerMessage.text = (loser.core.CharInfo.gender == Gender.Female) ? "FINISH HER!" : "FINISH HIM!";
        AudioRoundManager.Play((loser.core.CharInfo.gender == Gender.Female) ? GameEvent.FinishHer : GameEvent.FinishHim);
        
        loser.core.SetState(CharacterState.FinishHimVictim); // Loser plays dizzy animation
        winner.core.SetState(CharacterState.FinishHimWinner); // Winner can move
        winner.ForcePaused(false); 
        loser.ForcePaused(true); // Loser is unresponsive
        
        postKOState = PostKOSubState.AwaitingFinisherInput;
        float finisherCountdown = FINISHER_TIMER_DURATION; 
        bool finisherActionTaken = false;

        while (finisherCountdown > 0 && !finisherActionTaken)
        {
            InputBuffer winnerInput = winner.GetComponent<InputBuffer>();
            // Display finisherCountdown if desired (e.g. centerMessage.text = finisherCountdown.ToString("F1"))

            if (isMercyPossibleThisRound && winner.core.CanPerformMercyThisMatch && CheckMercyInput(winnerInput))
            {
                Debug.Log(winner.name + " performs Mercy!"); loser.core.ReviveForMercy(); winner.core.MarkMercyAsPerformedByThisPlayer(); matchAnimalityEnabled = true;
                if (centerMessage != null) centerMessage.text = "MERCY!"; AudioRoundManager.Play(GameEvent.Mercy);
                yield return new WaitForSeconds(1.5f); 
                postKOState = PostKOSubState.MercyGranted; BeginCombat(); finisherActionTaken = true; 
                if (timerText != null) timerText.gameObject.SetActive(true); // Re-show round timer
                yield break; 
            }
            
            // TODO: Replace with actual input sequence detection for Fatalities, Friendships, etc.
            if (winnerInput.State.PressedHighPunch) // Placeholder for any finisher
            {
                finisherActionTaken = true;
                if (!winner.core.BlockedThisRound) { Debug.Log(winner.name + " performs Friendship/Babality! (Placeholder)"); /* Play Anim */ }
                else { Debug.Log(winner.name + " performs Fatality! (Placeholder)");  /* Play Anim */ }
                loser.core.SetState(CharacterState.Knockdown); // Loser gets finished
            }
            else if (winner.core.State == CharacterState.Attacking && winner.core.IsMoveDataValid && winner.core.CurrentMove.tag == "DefaultHitFinisher")
            {
                 Debug.Log(winner.name + " lands a normal hit to end Finish Him sequence.");
                 loser.core.SetState(CharacterState.Knockdown);
                 finisherActionTaken = true;
            }
            finisherCountdown -= Time.deltaTime; 
            yield return null;
        }

        if (!finisherActionTaken && loser?.core != null) 
        { 
            if (centerMessage != null) centerMessage.text = (winner.core?.CharInfo?.characterName ?? "WINNER").ToUpper() + " WINS";
            loser.core.SetState(CharacterState.Knockdown); 
            winner.PlayWinAnimation(); // Winner plays victory
            AudioRoundManager.Play(GameEvent.Wins); // Play general wins sound
            loser.core.SetMercyEligibility(false); 
            Debug.Log("Finisher timer expired. " + loser.name + " collapses."); 
        }
        postKOState = finisherActionTaken ? PostKOSubState.FinisherPerformed : PostKOSubState.ProceedToRoundEnd;
        if (timerText != null) timerText.gameObject.SetActive(true); // Re-show round timer
        yield return new WaitForSeconds(finisherActionTaken ? 2.0f : koFreeze); // Longer pause if finisher, shorter if default
    }

    bool CheckMercyInput(InputBuffer input){ /* TODO: UMK3 Mercy: Hold Run, D,D,D, Release Run. */ return input.State.PressedBlock && input.State.Down && input.State.Run; }
    IEnumerator GameOverSequence(FighterCharacter matchWinner){if(centerMessage!=null){centerMessage.text=(matchWinner==fighterA_instance?"PLAYER 1":"PLAYER 2")+" WINS MATCH!";}AudioRoundManager.Play(GameEvent.Flawless);yield return new WaitForSeconds(3f);if(FadeController.I!=null)yield return FadeController.I.FadeOut(gameOverFadeDuration);if(centerMessage!=null)centerMessage.text=string.Empty;}
    void BeginCombat(){if(fighterA_instance!=null)fighterA_instance.ForcePaused(false);if(fighterB_instance!=null)fighterB_instance.ForcePaused(false);roundActive=true;firstFrameAfterUnpause=true;if(rbA!=null&&fighterA_instance!=null)rbA.position=fighterA_instance.transform.position;if(rbB!=null&&fighterB_instance!=null)rbB.position=fighterB_instance.transform.position;if(fighterA_instance?.core!=null&&rbA!=null)fighterA_instance.core.SyncPosition(rbA.position);if(fighterB_instance?.core!=null&&rbB!=null)fighterB_instance.core.SyncPosition(rbB.position);roundTimerSec=timeLimit;lowHealthWarningPlayedP1=false;lowHealthWarningPlayedP2=false;}
    void EndCombat(){roundActive=false;if(fighterA_instance!=null)fighterA_instance.ForcePaused(true);if(fighterB_instance!=null)fighterB_instance.ForcePaused(true);SetAnimatorSpeed(fighterA_instance,0f);SetAnimatorSpeed(fighterB_instance,0f);}
    
    IEnumerator DeclareRoundOutcomeAndAnimate()
    {
        string outcomeText = "DRAW";
        FighterCharacter winnerForAudio = null;

        if (currentRoundVictor != null && currentRoundVictor.core?.CharInfo != null)
        {
            outcomeText = currentRoundVictor.core.CharInfo.characterName.ToUpper() + " WINS ROUND";
            winnerForAudio = currentRoundVictor;
            currentRoundVictor.PlayWinAnimation();
            if (currentRoundDefeated?.core != null && currentRoundDefeated.core.Health <= 0)
            {
                currentRoundDefeated.core.SetState(CharacterState.Knockdown);
            }
        }
        else if (currentRoundEndCondition == RoundEndType.TimeUpDraw || currentRoundEndCondition == RoundEndType.DoubleKO)
        {
            outcomeText = "DRAW";
            fighterA_instance?.core?.SetState(CharacterState.Idle);
            fighterB_instance?.core?.SetState(CharacterState.Idle);
        }

        if (centerMessage != null) centerMessage.text = outcomeText;
        
        if (winnerForAudio != null && winnerForAudio.core.CharInfo.nameAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(winnerForAudio.core.CharInfo.nameAudioClip, Camera.main ? Camera.main.transform.position : Vector3.zero);
            yield return new WaitForSeconds(winnerForAudio.core.CharInfo.nameAudioClip.length + 0.1f); // Add slight delay
        }
        else if (winnerForAudio != null) // Has a winner but no name audio
        {
            yield return new WaitForSeconds(0.5f); // Default delay
        }
        
        if (winnerForAudio != null) { AudioRoundManager.Play(GameEvent.Wins); }
        else { /* Play draw sound if applicable */ }

        UpdateWinCountUI();
    }
    void CheckLowHealthWarnings(){if(fighterA_instance?.core!=null&&!lowHealthWarningPlayedP1&&fighterA_instance.Health>0&&fighterA_instance.Health<=FighterCharacterCore.MAX_HEALTH*LOW_HEALTH_THRESHOLD_PERCENT){AudioRoundManager.Play(GameEvent.LastHitWarning);lowHealthWarningPlayedP1=true;}if(fighterB_instance?.core!=null&&!lowHealthWarningPlayedP2&&fighterB_instance.Health>0&&fighterB_instance.Health<=FighterCharacterCore.MAX_HEALTH*LOW_HEALTH_THRESHOLD_PERCENT){AudioRoundManager.Play(GameEvent.LastHitWarning);lowHealthWarningPlayedP2=true;}}
    void UpdateWinCountUI(){/* For dedicated win UI */}
    void OnGUI(){if(!Application.isPlaying||(fighterA_instance==null&&fighterB_instance==null))return;if(roundActive||postKOState==PostKOSubState.AwaitingFinisherInput||postKOState==PostKOSubState.MercyGranted){DrawPlayerLifebar(fighterA_instance,true);DrawPlayerLifebar(fighterB_instance,false);}}
    void DrawPlayerLifebar(FighterCharacter p,bool isP1){if(p?.core==null)return;float cH=p.Health;float mH=FighterCharacterCore.MAX_HEALTH;float fR=(mH>0)?Mathf.Clamp01(cH/mH):0f;int fWPx=Mathf.RoundToInt(fR*LIFE_BAR_WIDTH_PX);Rect bgR,fgR;Color eC=new Color(0.3f,0.3f,0.3f,0.8f);Color fC=Color.green;bool useFlash=false;if(p.Health>0&&p.Health<=FighterCharacterCore.MAX_HEALTH*LOW_HEALTH_THRESHOLD_PERCENT){if(p==fighterA_instance&&lowHealthWarningPlayedP1)useFlash=true;if(p==fighterB_instance&&lowHealthWarningPlayedP2)useFlash=true;}if(useFlash)fC=(Time.time%0.4f<0.2f)?Color.red:new Color(1f,0.3f,0.3f);if(isP1){bgR=new Rect(LIFE_BAR_P1_X_POS,LIFE_BAR_Y_POS,LIFE_BAR_WIDTH_PX,LIFE_BAR_HEIGHT_PX);fgR=new Rect(bgR.x,bgR.y,fWPx,bgR.height);}else{float p2X=Screen.width-LIFE_BAR_P1_X_POS-LIFE_BAR_WIDTH_PX;bgR=new Rect(p2X,LIFE_BAR_Y_POS,LIFE_BAR_WIDTH_PX,LIFE_BAR_HEIGHT_PX);fgR=new Rect(bgR.xMax-fWPx,bgR.y,fWPx,bgR.height);}GUI.color=eC;GUI.DrawTexture(bgR,Texture2D.whiteTexture,ScaleMode.StretchToFill);GUI.color=fC;GUI.DrawTexture(fgR,Texture2D.whiteTexture,ScaleMode.StretchToFill);GUI.color=Color.black;DrawGUIRectOutline(bgR,1);GUI.color=Color.white;}
    void DrawGUIRectOutline(Rect rect,int thick){GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,thick),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax-thick,rect.width,thick),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.y,thick,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax-thick,rect.y,thick,rect.height),Texture2D.whiteTexture);}
    void SetAnimatorSpeed(FighterCharacter fc,float speed){if(fc==null)return;Animator anim=fc.GetComponentInChildren<Animator>()??fc.GetComponent<Animator>();if(anim!=null)anim.speed=speed;}
    void RewindAnimator(FighterCharacter fc){if(fc==null)return;Animator anim=fc.GetComponentInChildren<Animator>()??fc.GetComponent<Animator>();if(anim!=null&&anim.runtimeAnimatorController!=null){const string DSN="Locomotion";int lI=0;int dSH=Animator.StringToHash(DSN);if(anim.HasState(lI,dSH))anim.Play(dSH,lI,0f);else if(anim.GetCurrentAnimatorStateInfo(lI).fullPathHash!=0)anim.Play(anim.GetCurrentAnimatorStateInfo(lI).fullPathHash,lI,0f);anim.Update(0f);}}
}
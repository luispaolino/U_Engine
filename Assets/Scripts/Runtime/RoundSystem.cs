using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UMK3;

public class RoundSystem : MonoBehaviour
{
    public FighterCharacter fighterA;
    public FighterCharacter fighterB;

    public Text centerMessage;
    public Text timerText;
    public Text scoreA;
    public Text scoreB;

    public int   timeLimit    = 90;
    public int   roundsToWin  = 2;
    public float freezeIntro  = 1.5f;
    public float fightMsgDur  = 1.0f;
    public float koFreeze     = 0.5f;
    public float finishWindow = 4.0f;

    enum MatchState { Intro, Fight, TimeUp, Finish, Win, Draw }
    MatchState state;
    int        timer;
    int        winsA, winsB;
    bool       matchOver;

    void Start() => StartCoroutine(MatchRoutine());

    IEnumerator MatchRoutine()
    {
        while (!matchOver)
        {
            yield return StartCoroutine(RoundRoutine());
            if (!matchOver) yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator RoundRoutine()
    {
        // INTRO
        state = MatchState.Intro;
        timer = timeLimit;
        centerMessage.text = $"Round {winsA + winsB + 1}";
        Freeze(true);
        yield return new WaitForSeconds(freezeIntro);

        centerMessage.text = "Fight!";
        yield return new WaitForSeconds(fightMsgDur);
        centerMessage.text = "";
        Freeze(false);
        state = MatchState.Fight;

        // FIGHT LOOP
        while (state == MatchState.Fight)
        {
            yield return new WaitForSeconds(1f);
            timer--;
            timerText.text = timer.ToString();

            if (fighterA.Health <= 0 || fighterB.Health <= 0)
            {
                state = MatchState.Finish;
                break;
            }
            if (timer <= 0)
            {
                state = MatchState.TimeUp;
                break;
            }
        }

        // TIME UP
        if (state == MatchState.TimeUp)
        {
            Freeze(true);
            centerMessage.text = "TIME UP!";
            yield return new WaitForSeconds(1.5f);
            AwardTimeUp();
            centerMessage.text = "";
            yield break;
        }

        // KO → Finish Him/Her
        Freeze(true);
        yield return new WaitForSeconds(koFreeze);
        Freeze(false);

        var loser  = fighterA.Health <= 0 ? fighterA : fighterB;
        var winner = loser == fighterA ? fighterB : fighterA;

        loser.ForceState(CharacterState.Knockdown);
        winner.ForceState(CharacterState.GetUp);

        centerMessage.text = loser.gender == Gender.Female
            ? "Finish Her!" : "Finish Him!";

        yield return new WaitForSeconds(finishWindow);

        // WINNER ANNOUNCE
        centerMessage.text = winner == fighterA
            ? "Player 1 Wins" : "Player 2 Wins";

        if (winner == fighterA) winsA++;
        else winsB++;

        UpdateScoreUI();
        Freeze(true);
        yield return new WaitForSeconds(2f);
        centerMessage.text = "";

        if (winsA == roundsToWin || winsB == roundsToWin)
        {
            matchOver = true;
            centerMessage.text = winner == fighterA
                ? "Player 1 Wins Match"
                : "Player 2 Wins Match";
            yield return new WaitForSeconds(3f);

            // reset match
            winsA = winsB = 0;
            UpdateScoreUI();
            centerMessage.text = "";
            matchOver = false;
        }

        ResetRound();
    }

    void Freeze(bool b)
    {
        fighterA.ForcePaused(b);
        fighterB.ForcePaused(b);
    }

    void AwardTimeUp()
    {
        if (fighterA.Health == fighterB.Health)
            centerMessage.text = "DRAW!";
        else if (fighterA.Health > fighterB.Health)
        {
            winsA++;
            centerMessage.text = "Player 1 Wins";
        }
        else
        {
            winsB++;
            centerMessage.text = "Player 2 Wins";
        }
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        scoreA.text = new string('■', winsA);
        scoreB.text = new string('■', winsB);
    }

    void ResetRound()
    {
        fighterA.RoundReset(new Vector3(-2, 0, 0), true);
        fighterB.RoundReset(new Vector3( 2, 0, 0), false);
    }
}

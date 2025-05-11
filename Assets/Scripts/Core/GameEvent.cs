/// <summary>
/// Enumeration of every round/announcer sound used by the project.
/// The names match what DefaultUMK3DataGenerator.cs and RoundAudioBank expect.
/// </summary>
public enum GameEvent
{
    RoundOne,
    RoundTwo,
    RoundThree,
    Fight,
    Last10SecTick,
    LastHitWarning,

    FinishHim,
    FinishHer,
    Flawless,
    FlawlessVictory,
    Wins,

    // Name call-outs (extend as you add characters)
    Name_Scorpion
}

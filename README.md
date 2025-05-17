# UMK3-Style Fighting Game Engine (Unity)

This project is a 2.5D fighting game engine developed in Unity, aiming to replicate and build upon mechanics inspired by classic arcade fighters like Ultimate Mortal Kombat 3.
It features a robust character core, round management, special move data handling via ScriptableObjects, and various debug tools.

## Debugger Keys

The `Debugger.cs` script (formerly `HitboxDebugger.cs`) uses the following keys during **Play Mode** to toggle visualizations:

*   **3:** Toggle Hurtbox display (Blue)
*   **4:** Toggle Hitbox display (Red for idle, Green for active)
*   **5:** Toggle Fighter main body collider display (Yellow)
*   **6:** Toggle Distance visualization (lines and numerical distance between players, jump height)
*   **7:** Toggle Metrics display (Health, Run/Block Meter, Lockout Timer for both players via OnGUI)

## Camera Shake Key (Test)

The `CameraController.cs` script includes a test for its screen shake functionality:

*   **Z:** Trigger a test camera shake with predefined magnitude and duration.

## Player 2 Hurt Key (Test)

The `DebugDamageP2.cs` (gameplay test):**
*   **X (default):** Apply a fixed amount of damage to Player 2. *(Note: The key and damage amount can be configured in the Inspector on the GameObject with the `DebugDamageP2` script).*

  
## Core Features Integrated

*   **Character Core (`FighterCharacterCore.cs`):**
    *   Manages fundamental character states (Idle, Walking, Running, Crouching, Jumping, Attacking, Blocking, HitStun, Knockdown, BackDash, MercyReceiving, FinishHimVictim, FinishHimWinner).
    *   Handles movement logic (walk, run, jump) with parameters driven by ScriptableObjects.
    *   Time-based Stamina/Meter system for running and blocking, including depletion, recharge, and "crush" (lockout) states.
    *   Health system with damage processing.
    *   Logic for special move execution (startup, active, recovery frames based on `MoveFrameData`).
    *   Mercy system eligibility flags (`IsMercyEligibleThisRound`, `CanPerformMercyThisMatch`).
    *   "Blocked This Round" tracking for Friendship/Babality eligibility.
    *   "Friendly KO" flag support.

*   **Character MonoBehaviour (`FighterCharacter.cs`):**
    *   Acts as the bridge between `FighterCharacterCore` and Unity's components.
    *   Handles player input via `InputBuffer.cs`.
    *   Applies core logic to `Rigidbody2D` for movement.
    *   Updates Animator parameters based on core state (IsJumping, IsRunning, IsKOd, IsDizzy, etc.) and triggers attack/victory/defeated animations.
    *   Manages visual orientation (graphics child rotation for facing and initial Y-offset).
    *   Processes hit detection via `OnTriggerEnter2D` and calls `core.ReceiveHit`.
    *   Loads character-specific data (Movement Stats, Special Moves, Character Info, Reactions, Throws) from assigned ScriptableObjects.

*   **Round & Match Management (`RoundSystem.cs`):**
    *   Instantiates and configures player characters from prefabs at runtime.
    *   Manages round flow (Round Start, "FIGHT!", active combat, round end).
    *   Handles round win/loss/draw conditions (KO, Double KO, Time Up).
    *   Tracks player scores (wins).
    *   Implements a best-of-X rounds match structure.
    *   **"Finish Him/Her!" Sequence:**
        *   Triggers on match-deciding KO rounds (skips on Time Up wins).
        *   Sets appropriate states for winner (movable, can perform finishers) and loser (dizzy, unresponsive).
        *   Manages a finisher input timer.
        *   Handles default KO if timer expires or a normal hit is landed.
    *   **Mercy System (UMK3 Style - Winner Input):**
        *   Checks eligibility (Round 3, 1-1 score, winner hasn't used Mercy).
        *   (Placeholder for winner's Mercy input sequence detection).
        *   Revives loser and continues the match if Mercy is performed.
        *   Enables Animality eligibility for the match.
    *   **Finisher Placeholders:** Logic stubs for Fatalities, Friendships, Babalities (checking `BlockedThisRound`), and Animalities (checking `matchAnimalityEnabled`).
    *   **Announcements & Audio:**
        *   Displays "ROUND X", "FIGHT!", "CHARACTER WINS", "DRAW", "FLAWLESS VICTORY", "FINISH HIM/HER", "MERCY", "MATCH WINNER" using UI Text elements.
        *   Sequences Character Name audio followed by "Wins" audio.
    *   OnGUI Lifebar display for both players.
    *   Manages character distance clamping.

*   **Data-Driven Design (ScriptableObjects):**
    *   `CharacterInfoSO.cs`: Character name, name audio clip, gender.
    *   `MovementStatsSO.cs`: Walk/run/jump velocities, jump startup frames, gravity.
    *   `SpecialMovesSO.cs`: Contains `MoveFrameData[]` for specific attacks, specials, and actions like "BackDash", "DefaultHitFinisher". `MoveFrameData` includes startup/active/recovery, damage, properties like unblockable, noChip, and `canMoveDuringStartUp/Active`.
    *   `ReactionTableSO.cs` (Assumed): For hit reaction data.
    *   `ThrowTableSO.cs` (Assumed): For throw data.
    *   `PlayerControlsProfile.cs` (Assumed): For player-specific input mappings.

*   **Input Handling (`InputBuffer.cs`):**
    *   Captures raw hardware input and translates it into a game-specific `Frame` state (held directions/buttons, pressed edge triggers).
    *   Handles local directions (Forward/Back) based on character facing.
    *   Detects double-taps.

*   **Camera Control (`CameraController.cs`):**
    *   Follows the midpoint of the two players horizontally.
    *   Clamps camera position to stage boundaries.
    *   Smoothly follows the highest player vertically with configurable padding and speeds.
    *   Initialized by `RoundSystem` with player targets.
    *   Includes a test screen shake feature.

*   **Debugging Tools (`Debugger.cs` - formerly `HitboxDebugger.cs`):**
    *   Visualizes Hurtboxes, Hitboxes (idle/active), Fighter Bodies, and Distance metrics in Game and Scene views.
    *   Displays runtime metrics (Health, Meter, Lockout) via OnGUI.
    *   Automatically scans specified layers for colliders.
    *   Gets player references from `RoundSystem`.

## Folder Structure (Recommended)

A clean folder structure helps in managing the project. Here's a suggestion:

Assets/
├── _Project # Your main game scene(s) and project-wide settings
│
├── Data/ # ScriptableObject assets
│ ├── CharacterInfo/ # CharacterInfoSO assets (e.g., ScorpionInfo.asset)
│ ├── MovementStats/ # MovementStatsSO assets
│ ├── SpecialMoves/ # SpecialMovesSO assets (move lists)
│ ├── ReactionTables/ # ReactionTableSO assets
│ ├── ThrowTables/ # ThrowTableSO assets
│ └── ControlProfiles/ # PlayerControlsProfile assets (P1Controls.asset, P2Controls.asset)
│
├── Prefabs/
│ ├── Characters/ # Player character prefabs (e.g., Scorpion.prefab)
│ └── System/ # System prefabs (e.g., RoundSystemManager.prefab, Debugger.prefab)
│
├── Scripts/
│ ├── Core/ # Core gameplay logic (FighterCharacterCore.cs)
│ ├── Runtime/ # MonoBehaviours controlling runtime game objects (FighterCharacter.cs, RoundSystem.cs, CameraController.cs, InputBuffer.cs)
│ ├── ScriptableObjects/ # Definitions for SOs (CharacterInfoSO.cs, MovementStatsSO.cs, SpecialMovesSO.cs, etc.)
│ ├── UI/ # Scripts related to UI management (if any beyond RoundSystem's Text refs)
│ ├── Debug/ # Debugging utilities (Debugger.cs)
│ └── Editor/ # Custom editor scripts (e.g., DebuggerEditor.cs - if separated)
│
├── Art/
│ ├── Sprites/
│ ├── Animations/
│ ├── Models/
│ └── Materials/
│
├── Audio/
  ├── Music/
  ├── SFX/
  └── Announcer/ # Name callouts, "Wins", "Flawless Victory", etc.

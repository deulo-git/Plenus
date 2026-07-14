# Project Context: Plenus (Digital Board Game Replica)
## 1. Project Overview
* **Goal:** A digital multiplayer replica of the popular "Roll & Write" board game "Plenus".
* **Engine:** Unity version 6000.5.1f1.
* **Multiplayer Framework:** Unity Netcode for GameObjects (NGO).
* **Language Rules:** ALL code, variable names, and code comments MUST strictly be written in English.
## 2. Core Game Loop & Mechanics
* **Initiative Phase:** At the start, players roll 3 numeric dice. The player with the highest sum goes first.
* **Active Turn:** The active player rolls all 6 dice (3 numeric, 3 color), selects exactly 1 numeric die and 1 color die, and marks the board.
* **Passive Turn:** The non-active player(s) must then use the remaining 4 unselected dice to make their move.
* **Selection Rules:** * Mechanics use a "Select & Confirm" flow to prevent "fat finger" mistakes.
  * Marked cells must be adjacent to previously marked cells (unless it's the first turn, which must anchor to the middle row).
## 3. Game Board & Procedural Generation
* **Dimensions:** A strict 15 rows x 7 columns static grid (105 cells total).
* **Color Distribution:** 5 colors total. Each color has exactly 21 cells, divided into exactly one cluster of each size: 6, 5, 4, 3, 2, and 1.
* **Star Distribution:** 2 stars per color (10 stars total), distributed evenly avoiding proximity clustering.
* **Algorithm Standard (Fail-Fast Greedy):** We STRICTLY DO NOT use recursive backtracking for generation as it freezes the Unity Main Thread. We use a "Fail-Fast Greedy Random Walk" algorithm with "Weighted Growth" (to create organic, Tetris-like clusters instead of straight lines). If a generation conflict occurs, the algorithm instantly aborts and restarts a fresh board (takes < 1ms).
## 4. Key Systems & Architecture
* **Data-UI Decoupling (Observer Pattern):** UI logic and Data models are strictly separated. `PlayerData` handles stats, and `ScoreUI` listens to C# events to update visually (Low Coupling).
* **DiceManager:** Handles 3D dice rolling using `IEnumerator` (Coroutines) to ensure realistic physics/spins (4-7 minimum random spins) before settling on a result, preventing the UI from freezing.
* **Wildcards (Dynamic Color Locking):** * The 'Black' face is the color wildcard. The '6' face is the numeric wildcard (representing values 1-5).
  * Wildcards use "Dynamic Color Locking": A player can start picking any color, but upon clicking the first cell, the selection is locked to that specific color for the rest of the chain to prevent rule exploits.
* **Scoring System (`ScoreManager`):** * Completed row rewards depend on distance from the center. Array of rewards: `[5, 3, 3, 3, 2, 2, 2, 1, 2, 2, 2, 3, 3, 3, 5]`.
  * Penalties: Unmarked stars give -2 points at the end of the game.
  * Bonuses: Completing all cells of a specific color awards +5 points.
## 5. Multiplayer & Netcode Rules
* **Scene Architecture:** We use separate scenes. A `LobbyScene` with Host/Join buttons, which transitions safely via `NetworkSceneManager` to the `MainGameScene`.
* **Authority:** The Server (Host) is solely responsible for instantiating and spawning NetworkObjects (like `BoardManager`).
* **UI Network Spawning:** When spawning UI elements that belong to a Canvas, the Server must use `SetParent(canvasTransform, false)` BEFORE executing `Spawn()` to prevent RectTransform scaling/hierarchy issues on the clients.
* **Optimization:** Dice physics/rotations run locally on clients. The server only calculates and synchronizes the final logical result (Color/Number) via RPCs or NetworkVariables to save bandwidth.

# Plenus Database — Setup & Usage Guide

## What's in this folder

| File | Purpose |
|---|---|
| `plenus.db` | The actual database — a single, ready-to-use SQLite file. This IS the database; nothing else to install or run. |
| `plenus_schema.sql` | The full schema as SQL. Keep it in version control (the `.db` binary is better git-ignored). You can rebuild an empty database from it at any time. |

## Step 1 — Download a viewer (the only download you need)

SQLite has no server. To open and inspect `plenus.db` visually, install **DB Browser for SQLite** (free, open source):

- Download: https://sqlitebrowser.org/dl/ → "DB Browser for SQLite - Standard installer for 64-bit Windows"
- Install it, then File → Open Database → select `plenus.db`.
- Use the *Browse Data* tab to look at tables and the *Execute SQL* tab to run queries.

## Step 2 — Understand the schema (5-minute tour)

Enum values are stored as TEXT matching the C# enum names (`CellColor`, `CompletionOrder`), so Unity code can round-trip them with `Enum.Parse` / `ToString()`.

- **players** — persistent profiles (unique name, case-insensitive).
- **boards** + **board_cells** — one generated board per match; exactly 105 cells (15×7) with color + star. Marks are NOT stored here — they're derivable per player from the move log.
- **matches** — a game session: `in_progress` / `finished` / `abandoned`, links to its board.
- **match_players** — one row per player per match: turn order, final score, stars collected, unmarked stars, unused wildcards, winner flag.
- **match_player_rows / match_player_colors** — the ScoreBreakdown detail: which rows/colors were completed First/Second and the points earned.
- **moves** + **move_cells** — full turn log (dice chosen + cells marked). This enables resuming an interrupted match and replaying finished ones.
- **leaderboard** (view) — games played, wins, best score, avg score per player, over finished matches.
- **match_history** (view) — one row per player per finished match, newest first.
- **schema_info** — schema version (currently 1) for future migrations.

Wildcard conventions in `moves`: `numeric_die = 6` is the numeric wildcard, `color_die = 'Black'` is the color wildcard.

## Step 3 — Where the file should live for Unity

- **During development:** keep `plenus.db` in this `Database/` folder (or `Assets/StreamingAssets/` if you want it shipped inside the build as a read-only template).
- **At runtime:** the game should copy/create the database under `Application.persistentDataPath` (a writable per-user folder, e.g. `C:\Users\<you>\AppData\LocalLow\<Company>\Plenus\plenus.db`). Builds must never write inside their install folder.

## Step 4 — Rebuilding an empty database (if ever needed)

In DB Browser: File → New Database → then Execute SQL tab → paste `plenus_schema.sql` → run → save.

## Useful queries

```sql
-- Top players
SELECT * FROM leaderboard;

-- Recent games
SELECT * FROM match_history LIMIT 20;

-- Load a board's layout
SELECT row, col, color, has_star FROM board_cells WHERE board_id = ? ORDER BY row, col;

-- Find an unfinished match to resume
SELECT match_id, board_id, started_at FROM matches WHERE status = 'in_progress';
```

## Next step (Unity integration)

Add a small `DatabaseManager.cs` that opens the file with SQLite and exposes methods like `CreatePlayer`, `SaveBoard`, `RecordMove`, `FinishMatch`, `GetLeaderboard`. That requires adding a SQLite plugin to the Unity project — covered in the next step of the walkthrough.

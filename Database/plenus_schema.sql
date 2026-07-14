-- ============================================================
-- PLENUS — Full SQLite database schema (v1)
-- Digital multiplayer replica of the "Plenus" Roll & Write game
--
-- Conventions:
--  * All enum-like values are stored as TEXT matching the C# enum
--    names exactly (CellColor, CompletionOrder), so (de)serialization
--    from Unity is trivial: Enum.Parse<CellColor>(text).
--  * Timestamps are TEXT in ISO-8601 UTC (SQLite datetime('now')).
--  * Booleans are INTEGER 0/1 with CHECK constraints.
--  * Board geometry: 15 rows (0-14) x 7 columns (0-6) = 105 cells.
-- ============================================================

PRAGMA foreign_keys = ON;

-- ------------------------------------------------------------
-- Schema versioning (for future migrations)
-- ------------------------------------------------------------
CREATE TABLE schema_info (
    version     INTEGER NOT NULL,
    applied_at  TEXT    NOT NULL DEFAULT (datetime('now'))
);
INSERT INTO schema_info (version) VALUES (1);

-- ------------------------------------------------------------
-- PLAYERS — persistent player profiles
-- ------------------------------------------------------------
CREATE TABLE players (
    player_id    INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    created_at   TEXT    NOT NULL DEFAULT (datetime('now')),
    last_seen_at TEXT
);

-- ------------------------------------------------------------
-- BOARDS — one procedurally generated board per match.
-- board_cells holds exactly 105 rows per board (static layout:
-- color + star placement). Marks are per-player, per-match, and
-- are reconstructed from move_cells, not stored here.
-- ------------------------------------------------------------
CREATE TABLE boards (
    board_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE board_cells (
    board_id INTEGER NOT NULL REFERENCES boards(board_id) ON DELETE CASCADE,
    row      INTEGER NOT NULL CHECK (row BETWEEN 0 AND 14),
    col      INTEGER NOT NULL CHECK (col BETWEEN 0 AND 6),
    color    TEXT    NOT NULL CHECK (color IN ('Blue','Red','Green','Yellow','Orange')),
    has_star INTEGER NOT NULL DEFAULT 0 CHECK (has_star IN (0,1)),
    PRIMARY KEY (board_id, row, col)
) WITHOUT ROWID;

-- ------------------------------------------------------------
-- MATCHES — one game session
-- ------------------------------------------------------------
CREATE TABLE matches (
    match_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    board_id   INTEGER NOT NULL REFERENCES boards(board_id),
    status     TEXT    NOT NULL DEFAULT 'in_progress'
                       CHECK (status IN ('in_progress','finished','abandoned')),
    started_at TEXT    NOT NULL DEFAULT (datetime('now')),
    ended_at   TEXT
);

-- ------------------------------------------------------------
-- MATCH_PLAYERS — a player's participation + final results.
-- Mirrors PlayerData / ScoreBreakdown scalar fields.
-- ------------------------------------------------------------
CREATE TABLE match_players (
    match_player_id  INTEGER PRIMARY KEY AUTOINCREMENT,
    match_id         INTEGER NOT NULL REFERENCES matches(match_id) ON DELETE CASCADE,
    player_id        INTEGER NOT NULL REFERENCES players(player_id),
    turn_order       INTEGER NOT NULL,                -- 1 = went first (initiative winner)
    final_score      INTEGER,                         -- NULL while match in progress
    stars_collected  INTEGER NOT NULL DEFAULT 0,
    unmarked_stars   INTEGER NOT NULL DEFAULT 0,      -- x PenaltyPerUnmarkedStar (-2)
    unused_wildcards INTEGER NOT NULL DEFAULT 0,      -- x RewardPerUnusedWildcard (+1)
    is_winner        INTEGER NOT NULL DEFAULT 0 CHECK (is_winner IN (0,1)),
    UNIQUE (match_id, player_id),
    UNIQUE (match_id, turn_order)
);

-- ------------------------------------------------------------
-- Per-row completion scores (ScoreBreakdown.RowScore)
-- ------------------------------------------------------------
CREATE TABLE match_player_rows (
    match_player_id  INTEGER NOT NULL REFERENCES match_players(match_player_id) ON DELETE CASCADE,
    row_index        INTEGER NOT NULL CHECK (row_index BETWEEN 0 AND 14),
    completion_order TEXT    NOT NULL CHECK (completion_order IN ('First','Second')),
    points           INTEGER NOT NULL,
    PRIMARY KEY (match_player_id, row_index)
) WITHOUT ROWID;

-- ------------------------------------------------------------
-- Per-color completion scores (ScoreBreakdown.ColorScore)
-- ------------------------------------------------------------
CREATE TABLE match_player_colors (
    match_player_id  INTEGER NOT NULL REFERENCES match_players(match_player_id) ON DELETE CASCADE,
    color            TEXT    NOT NULL CHECK (color IN ('Blue','Red','Green','Yellow','Orange')),
    completion_order TEXT    NOT NULL CHECK (completion_order IN ('First','Second')),
    points           INTEGER NOT NULL,
    PRIMARY KEY (match_player_id, color)
) WITHOUT ROWID;

-- ------------------------------------------------------------
-- MOVES — full turn log, enables resume + replay.
-- numeric_die 6 = numeric wildcard; color_die 'Black' = color wildcard.
-- ------------------------------------------------------------
CREATE TABLE moves (
    move_id         INTEGER PRIMARY KEY AUTOINCREMENT,
    match_id        INTEGER NOT NULL REFERENCES matches(match_id) ON DELETE CASCADE,
    match_player_id INTEGER NOT NULL REFERENCES match_players(match_player_id),
    turn_number     INTEGER NOT NULL,                 -- global turn counter, starts at 1
    is_active_turn  INTEGER NOT NULL CHECK (is_active_turn IN (0,1)),
    numeric_die     INTEGER NOT NULL CHECK (numeric_die BETWEEN 1 AND 6),
    color_die       TEXT    NOT NULL CHECK (color_die IN ('Blue','Red','Green','Yellow','Orange','Black')),
    used_wildcard   INTEGER NOT NULL DEFAULT 0 CHECK (used_wildcard IN (0,1)),
    created_at      TEXT    NOT NULL DEFAULT (datetime('now')),
    UNIQUE (match_id, turn_number, match_player_id)
);

CREATE TABLE move_cells (
    move_id INTEGER NOT NULL REFERENCES moves(move_id) ON DELETE CASCADE,
    row     INTEGER NOT NULL CHECK (row BETWEEN 0 AND 14),
    col     INTEGER NOT NULL CHECK (col BETWEEN 0 AND 6),
    PRIMARY KEY (move_id, row, col)
) WITHOUT ROWID;

-- ------------------------------------------------------------
-- Indexes for the common lookups
-- ------------------------------------------------------------
CREATE INDEX idx_match_players_player ON match_players(player_id);
CREATE INDEX idx_matches_status       ON matches(status);
CREATE INDEX idx_moves_match          ON moves(match_id, turn_number);

-- ------------------------------------------------------------
-- LEADERBOARD — aggregated stats over finished matches
-- ------------------------------------------------------------
CREATE VIEW leaderboard AS
SELECT
    p.player_id,
    p.name,
    COUNT(mp.match_player_id)          AS games_played,
    COALESCE(SUM(mp.is_winner), 0)     AS wins,
    MAX(mp.final_score)                AS best_score,
    ROUND(AVG(mp.final_score), 1)      AS avg_score
FROM players p
JOIN match_players mp ON mp.player_id = p.player_id
JOIN matches m        ON m.match_id   = mp.match_id AND m.status = 'finished'
GROUP BY p.player_id, p.name
ORDER BY wins DESC, best_score DESC;

-- ------------------------------------------------------------
-- MATCH HISTORY — one row per player per finished match
-- ------------------------------------------------------------
CREATE VIEW match_history AS
SELECT
    m.match_id,
    m.started_at,
    m.ended_at,
    p.name           AS player_name,
    mp.turn_order,
    mp.final_score,
    mp.is_winner
FROM matches m
JOIN match_players mp ON mp.match_id  = m.match_id
JOIN players p        ON p.player_id  = mp.player_id
WHERE m.status = 'finished'
ORDER BY m.ended_at DESC, m.match_id DESC, mp.final_score DESC;

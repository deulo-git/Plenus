using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using UnityEngine;

/// <summary>
/// Central data-access layer for the Plenus SQLite database.
/// Pure C# static class (no MonoBehaviour) — call it from GameManager,
/// NetworkMenuManager, etc. Only the Host/Server should write to it in
/// a networked game (server authority).
///
/// Requires:
///  * Player Settings > Api Compatibility Level = .NET Framework
///  * sqlite3.dll in Assets/Plugins/x86_64 (for Windows builds)
/// </summary>
public static class DatabaseManager
{
    // ------------------------------------------------------------------
    // Connection handling
    // ------------------------------------------------------------------

    private static SqliteConnection _connection;

    /// <summary>
    /// In the Editor we use the repo's Database/plenus.db so you can watch
    /// data appear live in DB Browser. In a real, separately-installed build
    /// we use the per-user writable folder (persistentDataPath) and create
    /// the schema on first run if the file does not exist yet — every
    /// player's own install gets its own local file, as intended.
    ///
    /// Local-testing exception: when you run a build on the SAME PC as the
    /// Editor (e.g. Editor = Host, a built .exe = Client) and want both to
    /// show up in the one database you're watching in DB Browser, drop a
    /// plain text file named "db_path_override.txt" next to the built .exe,
    /// containing the absolute path to the repo's Database\plenus.db on one
    /// line. If that file is absent — which is the case for any real player
    /// — this has zero effect and persistentDataPath is used as normal.
    /// </summary>
    public static string DbPath
    {
        get
        {
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Database", "plenus.db");
#else
            string buildFolder = Directory.GetParent(Application.dataPath).FullName;
            string overrideFile = Path.Combine(buildFolder, "db_path_override.txt");
            if (File.Exists(overrideFile))
            {
                string overridePath = File.ReadAllText(overrideFile).Trim();
                if (!string.IsNullOrEmpty(overridePath))
                {
                    Debug.Log($"[DatabaseManager] Using db_path_override.txt -> {overridePath}");
                    return overridePath;
                }
            }
            return Path.Combine(Application.persistentDataPath, "plenus.db");
#endif
        }
    }

    private static SqliteConnection Connection
    {
        get
        {
            if (_connection == null)
            {
                bool isNew = !File.Exists(DbPath);
                Directory.CreateDirectory(Path.GetDirectoryName(DbPath));

                _connection = new SqliteConnection("URI=file:" + DbPath);
                _connection.Open();

                Execute("PRAGMA foreign_keys = ON;");

                if (isNew)
                {
                    CreateSchema();
                    Debug.Log($"[DatabaseManager] Created new database at {DbPath}");
                }
            }
            return _connection;
        }
    }

    /// <summary>Call from OnApplicationQuit to release the file handle.</summary>
    public static void Close()
    {
        _connection?.Close();
        _connection = null;
    }

    private static void CreateSchema()
    {
        // Strip SQL line comments BEFORE splitting on ';'. A comment may legally
        // contain a ';' (e.g. "...numeric wildcard; color_die 'Black'..."), and a
        // naive Split(';') would cut that comment in half and leak its tail
        // ("color_die 'Black' = color wildcard.") into the front of the next
        // statement, producing a bogus "syntax error near color_die". Removing
        // comments first makes ';' an unambiguous statement terminator (this
        // schema never puts ';' inside a string literal).
        string sql = StripSqlLineComments(DatabaseSchema.Sql);

        foreach (string statement in sql.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            Execute(statement.Trim() + ";");
        }
    }

    // Removes "-- ... (end of line)" comments from each line. Safe for this schema
    // because no string literal contains the "--" sequence.
    private static string StripSqlLineComments(string sql)
    {
        var builder = new System.Text.StringBuilder(sql.Length);
        foreach (string line in sql.Split('\n'))
        {
            int commentStart = line.IndexOf("--", StringComparison.Ordinal);
            builder.Append(commentStart >= 0 ? line.Substring(0, commentStart) : line);
            builder.Append('\n');
        }
        return builder.ToString();
    }

    // SQLite transactions are connection-level; plain BEGIN/COMMIT avoids
    // ADO.NET quirks about associating commands with transaction objects.
    private static void RunInTransaction(Action body)
    {
        Execute("BEGIN;");
        try
        {
            body();
            Execute("COMMIT;");
        }
        catch
        {
            Execute("ROLLBACK;");
            throw;
        }
    }

    // ------------------------------------------------------------------
    // Low-level helpers
    // ------------------------------------------------------------------

    private static SqliteCommand BuildCommand(string sql, params (string name, object value)[] args)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static void Execute(string sql, params (string, object)[] args)
    {
        using var cmd = BuildCommand(sql, args);
        cmd.ExecuteNonQuery();
    }

    private static long ExecuteScalarLong(string sql, params (string, object)[] args)
    {
        using var cmd = BuildCommand(sql, args);
        object result = cmd.ExecuteScalar();
        return (result == null || result == DBNull.Value) ? -1 : Convert.ToInt64(result);
    }

    private static long LastInsertId() => ExecuteScalarLong("SELECT last_insert_rowid();");

    // ------------------------------------------------------------------
    // PLAYERS
    // ------------------------------------------------------------------

    /// <summary>Returns the persistent player_id, creating the profile if needed.</summary>
    public static long GetOrCreatePlayer(string playerName)
    {
        Execute("INSERT OR IGNORE INTO players(name) VALUES (@name);", ("@name", playerName));
        Execute("UPDATE players SET last_seen_at = datetime('now') WHERE name = @name;", ("@name", playerName));
        return ExecuteScalarLong("SELECT player_id FROM players WHERE name = @name;", ("@name", playerName));
    }

    /// <summary>
    /// Deletes a player's local profile row — but ONLY if they never actually
    /// finished/played a match. match_players.player_id has no ON DELETE
    /// CASCADE on purpose: a hard delete would either be rejected outright by
    /// the foreign key (players row still referenced) or, if we cascaded it,
    /// would silently erase the OPPONENT's record of a match they both
    /// played. So real match history is intentionally preserved even after
    /// account deletion — the account itself (the login) is still fully gone,
    /// since that's deleted separately via Unity Authentication.
    /// Returns true if the local row was removed, false if it was left in
    /// place because history exists.
    /// </summary>
    public static bool DeletePlayerIfNoHistory(long playerId)
    {
        long historyCount = ExecuteScalarLong(
            "SELECT COUNT(*) FROM match_players WHERE player_id = @p;", ("@p", playerId));
        if (historyCount > 0)
            return false;

        Execute("DELETE FROM players WHERE player_id = @p;", ("@p", playerId));
        return true;
    }

    // ------------------------------------------------------------------
    // BOARDS
    // ------------------------------------------------------------------

    /// <summary>Persists a generated board layout (color + star per cell). Returns board_id.</summary>
    public static long SaveBoard(IEnumerable<CellData> cells)
    {
        long boardId = -1;
        RunInTransaction(() =>
        {
            Execute("INSERT INTO boards DEFAULT VALUES;");
            boardId = LastInsertId();

            foreach (CellData cell in cells)
            {
                Execute(
                    "INSERT INTO board_cells(board_id, row, col, color, has_star) VALUES (@b, @r, @c, @color, @star);",
                    ("@b", boardId), ("@r", cell.Row), ("@c", cell.Col),
                    ("@color", cell.Color.ToString()), ("@star", cell.HasStar ? 1 : 0));
            }
        });
        return boardId;
    }

    /// <summary>Reloads a board layout as fresh (unmarked) CellData objects.</summary>
    public static List<CellData> LoadBoard(long boardId)
    {
        var cells = new List<CellData>();
        using var cmd = BuildCommand(
            "SELECT row, col, color, has_star FROM board_cells WHERE board_id = @b ORDER BY row, col;",
            ("@b", boardId));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var color = (CellColor)Enum.Parse(typeof(CellColor), reader.GetString(2));
            var cell = new CellData(color, Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)))
            {
                HasStar = Convert.ToInt32(reader.GetValue(3)) == 1
            };
            cells.Add(cell);
        }
        return cells;
    }

    // ------------------------------------------------------------------
    // MATCHES
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a match on the given board. playerIdsInTurnOrder[0] is the
    /// initiative winner. Returns match_id and one match_player_id per player
    /// (same order as the input list).
    /// </summary>
    public static (long matchId, List<long> matchPlayerIds) CreateMatch(long boardId, IList<long> playerIdsInTurnOrder)
    {
        long matchId = -1;
        var matchPlayerIds = new List<long>();
        RunInTransaction(() =>
        {
            Execute("INSERT INTO matches(board_id) VALUES (@b);", ("@b", boardId));
            matchId = LastInsertId();

            for (int i = 0; i < playerIdsInTurnOrder.Count; i++)
            {
                Execute(
                    "INSERT INTO match_players(match_id, player_id, turn_order) VALUES (@m, @p, @t);",
                    ("@m", matchId), ("@p", playerIdsInTurnOrder[i]), ("@t", i + 1));
                matchPlayerIds.Add(LastInsertId());
            }
        });
        return (matchId, matchPlayerIds);
    }

    /// <summary>Logs one confirmed move (dice choice + marked cells). Enables resume/replay.</summary>
    public static void RecordMove(
        long matchId, long matchPlayerId, int turnNumber, bool isActiveTurn,
        int numericDie, CellColor colorDie, bool usedWildcard,
        IEnumerable<(int row, int col)> markedCells)
    {
        RunInTransaction(() =>
        {
            Execute(
                "INSERT INTO moves(match_id, match_player_id, turn_number, is_active_turn, numeric_die, color_die, used_wildcard) " +
                "VALUES (@m, @mp, @t, @active, @num, @color, @wild);",
                ("@m", matchId), ("@mp", matchPlayerId), ("@t", turnNumber),
                ("@active", isActiveTurn ? 1 : 0), ("@num", numericDie),
                ("@color", colorDie.ToString()), ("@wild", usedWildcard ? 1 : 0));
            long moveId = LastInsertId();

            foreach (var (row, col) in markedCells)
                Execute("INSERT INTO move_cells(move_id, row, col) VALUES (@mv, @r, @c);",
                    ("@mv", moveId), ("@r", row), ("@c", col));
        });
    }

    /// <summary>
    /// Writes final results for every player and marks the match finished.
    /// Winner(s) = highest final score. Call once, at game end, on the Host.
    /// </summary>
    public static void FinishMatch(long matchId, IDictionary<long, PlayerData> resultsByMatchPlayerId)
    {
        RunInTransaction(() =>
        {
            foreach (var entry in resultsByMatchPlayerId)
            {
                long matchPlayerId = entry.Key;
                PlayerData data = entry.Value;
                ScoreBreakdown breakdown = data.scoreBreakdown;

                Execute(
                    "UPDATE match_players SET final_score = @score, stars_collected = @stars, " +
                    "unmarked_stars = @unmarked, unused_wildcards = @wild WHERE match_player_id = @mp;",
                    ("@score", breakdown.Total), ("@stars", data.totalStarsCollected),
                    ("@unmarked", breakdown.unmarkedStars), ("@wild", breakdown.unusedWildcards),
                    ("@mp", matchPlayerId));

                foreach (var row in breakdown.rows)
                {
                    if (row.order == CompletionOrder.None) continue;
                    Execute(
                        "INSERT OR REPLACE INTO match_player_rows(match_player_id, row_index, completion_order, points) " +
                        "VALUES (@mp, @row, @order, @pts);",
                        ("@mp", matchPlayerId), ("@row", row.rowIndex),
                        ("@order", row.order.ToString()), ("@pts", row.points));
                }

                foreach (var color in breakdown.colors)
                {
                    if (color.order == CompletionOrder.None) continue;
                    Execute(
                        "INSERT OR REPLACE INTO match_player_colors(match_player_id, color, completion_order, points) " +
                        "VALUES (@mp, @color, @order, @pts);",
                        ("@mp", matchPlayerId), ("@color", color.color.ToString()),
                        ("@order", color.order.ToString()), ("@pts", color.points));
                }
            }

            // Winner(s): highest final score in this match (ties share the win).
            Execute(
                "UPDATE match_players SET is_winner = 1 WHERE match_id = @m AND final_score = " +
                "(SELECT MAX(final_score) FROM match_players WHERE match_id = @m);",
                ("@m", matchId));

            Execute("UPDATE matches SET status = 'finished', ended_at = datetime('now') WHERE match_id = @m;",
                ("@m", matchId));
        });
    }

    public static void AbandonMatch(long matchId)
    {
        Execute("UPDATE matches SET status = 'abandoned', ended_at = datetime('now') WHERE match_id = @m;",
            ("@m", matchId));
    }

    // ------------------------------------------------------------------
    // LEADERBOARD & HISTORY
    // ------------------------------------------------------------------

    public struct LeaderboardEntry
    {
        public long playerId;
        public string name;
        public int gamesPlayed;
        public int wins;
        public int bestScore;
        public float avgScore;
    }

    public static List<LeaderboardEntry> GetLeaderboard()
    {
        var entries = new List<LeaderboardEntry>();
        using var cmd = BuildCommand("SELECT player_id, name, games_played, wins, best_score, avg_score FROM leaderboard;");
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new LeaderboardEntry
            {
                playerId = Convert.ToInt64(reader.GetValue(0)),
                name = reader.GetString(1),
                gamesPlayed = Convert.ToInt32(reader.GetValue(2)),
                wins = Convert.ToInt32(reader.GetValue(3)),
                bestScore = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                avgScore = reader.IsDBNull(5) ? 0f : Convert.ToSingle(reader.GetValue(5))
            });
        }
        return entries;
    }

    /// <summary>Returns the id of the most recent unfinished match, or -1 if none (for resume).</summary>
    public static long GetLatestUnfinishedMatch()
    {
        return ExecuteScalarLong(
            "SELECT match_id FROM matches WHERE status = 'in_progress' ORDER BY match_id DESC LIMIT 1;");
    }
}

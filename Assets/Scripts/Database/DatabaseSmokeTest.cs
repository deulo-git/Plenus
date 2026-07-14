using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quick end-to-end test of the database layer.
/// Drop this on any GameObject in a test scene, press Play, and check the
/// Console — or right-click the component header and choose "Run Database
/// Smoke Test" without even entering Play mode.
/// Afterwards, refresh DB Browser (View > Refresh / F5) to see the data.
/// </summary>
public class DatabaseSmokeTest : MonoBehaviour
{
    [ContextMenu("Run Database Smoke Test")]
    public void RunTest()
    {
        Debug.Log($"[SmokeTest] Using database at: {DatabaseManager.DbPath}");

        // 1. Players
        long alice = DatabaseManager.GetOrCreatePlayer("TestAlice");
        long bob = DatabaseManager.GetOrCreatePlayer("TestBob");
        Debug.Log($"[SmokeTest] Players -> TestAlice id={alice}, TestBob id={bob}");

        // 2. Board (fake layout: cycles the 5 colors over 15x7)
        var colors = new[] { CellColor.Blue, CellColor.Red, CellColor.Green, CellColor.Yellow, CellColor.Orange };
        var cells = new List<CellData>();
        for (int r = 0; r < 15; r++)
            for (int c = 0; c < 7; c++)
                cells.Add(new CellData(colors[(r * 7 + c) % 5], r, c));
        long boardId = DatabaseManager.SaveBoard(cells);
        int reloaded = DatabaseManager.LoadBoard(boardId).Count;
        Debug.Log($"[SmokeTest] Board id={boardId} saved, reloaded {reloaded} cells (expected 105)");

        // 3. Match + one move
        var (matchId, matchPlayerIds) = DatabaseManager.CreateMatch(boardId, new List<long> { alice, bob });
        DatabaseManager.RecordMove(matchId, matchPlayerIds[0], 1, true, 3, CellColor.Red, false,
            new List<(int, int)> { (7, 3), (7, 4) });
        Debug.Log($"[SmokeTest] Match id={matchId} created with move logged");

        // 4. Finish with fake scores
        var aliceData = new PlayerData(0, "TestAlice");
        aliceData.scoreBreakdown.rows.Add(new ScoreBreakdown.RowScore { rowIndex = 7, order = CompletionOrder.First, points = 1 });
        aliceData.scoreBreakdown.colors.Add(new ScoreBreakdown.ColorScore { color = CellColor.Red, order = CompletionOrder.First, points = 5 });
        var bobData = new PlayerData(1, "TestBob");
        bobData.scoreBreakdown.unmarkedStars = 2; // -4 points

        DatabaseManager.FinishMatch(matchId, new Dictionary<long, PlayerData>
        {
            { matchPlayerIds[0], aliceData },
            { matchPlayerIds[1], bobData }
        });

        // 5. Leaderboard
        foreach (var entry in DatabaseManager.GetLeaderboard())
            Debug.Log($"[SmokeTest] Leaderboard: {entry.name} | games={entry.gamesPlayed} wins={entry.wins} best={entry.bestScore} avg={entry.avgScore}");

        Debug.Log("[SmokeTest] DONE — refresh DB Browser (F5) to inspect the new rows.");
    }

    private void Start()
    {
        RunTest();
    }

    private void OnApplicationQuit()
    {
        DatabaseManager.Close();
    }
}

using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Static holder for the current Unity Gaming Services multiplayer Session,
/// shared between HostLobbyManager and JoinLobbyManager (and anything else
/// that cares "are we in a lobby, and which one"). Mirrors the PlayerSession
/// pattern already used for login identity.
///
/// Important: the session created by HostLobbyManager (via
/// SessionOptions.WithRelayNetwork()) is what actually backs the whole
/// match's Netcode/Relay connection, not just the lobby screen. Nothing
/// should tear it down until the match itself ends or a player deliberately
/// backs out.
/// </summary>
public static class LobbySession
{
    /// <summary>
    /// Client-side session "Type" key (see BaseSessionOptions.Type docs).
    /// NOT used to filter QuerySessionsAsync results — ISessionInfo doesn't
    /// expose Type in browse results — it's just a stable local label
    /// instead of the SDK's default random GUID. This project only ever
    /// creates one kind of lobby, so no filtering is needed.
    /// </summary>
    public const string SessionType = "PlenusDuel";

    /// <summary>Per-player property key holding the human-readable display name (PlayerSession.PlayerName).</summary>
    public const string DisplayNamePropertyKey = "displayName";

    /// <summary>The session we are currently hosting or have joined. Null if none.</summary>
    public static ISession Current { get; private set; }

    public static bool IsActive => Current != null;
    public static bool IsHost => Current is IHostSession;

    public static void Set(ISession session) => Current = session;

    /// <summary>
    /// Leaves the current session, if any. When we are the host, the SDK's
    /// own LeaveAsync() implementation deletes the whole session (so it
    /// disappears from other players' browse list) instead of just dropping
    /// our membership. Safe to call even when there's no active session.
    /// </summary>
    public static async Task LeaveOrDeleteAsync()
    {
        ISession session = Current;
        Current = null;
        if (session == null) return;

        try
        {
            await session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbySession] Leave/delete failed (session may already be gone): {e}");
        }
    }

    /// <summary>Clears the local reference without touching the remote session (e.g. after we were kicked/disconnected).</summary>
    public static void Clear() => Current = null;
}

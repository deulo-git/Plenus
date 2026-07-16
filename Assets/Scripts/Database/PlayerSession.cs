/// <summary>
/// Holds the identity of the locally logged-in player for the duration of
/// the application run. Set by LoginManager after a successful login;
/// read anywhere else (e.g. when creating PlayerData or hosting a match).
///
/// This is intentionally auth-method-agnostic: today it is filled from a
/// Unity Authentication username/password login, later a "Sign in with
/// Google" flow can fill the exact same fields (AuthProvider becomes
/// "google") without touching any caller — Unity Authentication supports
/// linking a Google identity onto the same account via
/// AuthenticationService.Instance.LinkWithGoogleAsync once that's wired up.
/// </summary>
public static class PlayerSession
{
    /// <summary>players.player_id in the LOCAL SQLite database. -1 = not logged in.
    /// This is per-machine and only used for the local match/move log (resume
    /// support); it is NOT the same identity space as UgsPlayerId below.</summary>
    public static long PlayerId { get; private set; } = -1;

    public static string PlayerName { get; private set; } = "";

    /// <summary>"password" today; "google" once Google auth is linked.</summary>
    public static string AuthProvider { get; private set; } = "";

    /// <summary>
    /// AuthenticationService.Instance.PlayerId — the stable, cross-device Unity
    /// Gaming Services identity for this account. This is what Cloud Save and
    /// Leaderboards data gets keyed under, since (unlike the local SQLite
    /// PlayerId) it is the same value no matter which machine the player signs
    /// in from.
    /// </summary>
    public static string UgsPlayerId { get; private set; } = "";

    public static bool IsLoggedIn => PlayerId > 0;

    public static void SignIn(long playerId, string playerName, string ugsPlayerId, string authProvider = "password")
    {
        PlayerId = playerId;
        PlayerName = playerName;
        UgsPlayerId = ugsPlayerId;
        AuthProvider = authProvider;
    }

    public static void SignOut()
    {
        PlayerId = -1;
        PlayerName = "";
        UgsPlayerId = "";
        AuthProvider = "";
    }
}

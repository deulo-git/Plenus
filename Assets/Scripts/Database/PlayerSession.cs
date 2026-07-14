/// <summary>
/// Holds the identity of the locally logged-in player for the duration of
/// the application run. Set by LoginManager after a successful login;
/// read anywhere else (e.g. when creating PlayerData or hosting a match).
///
/// This is intentionally auth-method-agnostic: today it is filled from a
/// username login, later a "Sign in with Google" flow can fill the exact
/// same fields (plus AuthProvider/ExternalId) without touching any caller.
/// </summary>
public static class PlayerSession
{
    /// <summary>players.player_id in the database. -1 = not logged in.</summary>
    public static long PlayerId { get; private set; } = -1;

    public static string PlayerName { get; private set; } = "";

    /// <summary>"local" today; "google" once Google auth is added.</summary>
    public static string AuthProvider { get; private set; } = "";

    public static bool IsLoggedIn => PlayerId > 0;

    public static void SignIn(long playerId, string playerName, string authProvider = "local")
    {
        PlayerId = playerId;
        PlayerName = playerName;
        AuthProvider = authProvider;
    }

    public static void SignOut()
    {
        PlayerId = -1;
        PlayerName = "";
        AuthProvider = "";
    }
}

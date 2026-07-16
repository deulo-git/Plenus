/// <summary>
/// Carries the initiative winner from the InitialFight scene into the Main
/// scene. Statics survive scene loads on the same machine, and only the
/// server decides turn order, so this only ever needs to be set/read on the
/// host.
///
/// GameManager consumes it once on match start; if it is empty (e.g. the Main
/// scene was started directly from the editor), GameManager falls back to
/// running the initiative phase itself as before.
/// </summary>
public static class InitiativeResult
{
    public static bool HasResult { get; private set; }
    public static ulong WinnerClientId { get; private set; }

    /// <summary>Called by InitialFightManager (server only) once the roll-off is decided.</summary>
    public static void Set(ulong winnerClientId)
    {
        WinnerClientId = winnerClientId;
        HasResult = true;
    }

    /// <summary>Returns the stored winner and clears the result so it is used exactly once.</summary>
    public static ulong Consume()
    {
        HasResult = false;
        return WinnerClientId;
    }

    public static void Clear() => HasResult = false;
}

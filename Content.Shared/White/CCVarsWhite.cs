using Robust.Shared.Configuration;

namespace Content.Shared.White;

[CVarDefs]
public sealed class WhiteCCVars
{
    /// <summary>
    ///     Optionally force set an announcer
    /// </summary>
    public static readonly CVarDef<string> Announcer =
        CVarDef.Create("game.announcer", "random", CVar.SERVERONLY);

    /// <summary>
    ///     Optionally blacklist announcers
    /// </summary>
    public static readonly CVarDef<List<string>> AnnouncerBlacklist =
        CVarDef.Create("game.announcer.blacklist", new List<string>(), CVar.SERVERONLY);
}
